// Purpose: Exposes A ut hC on tr ol le r HTTP endpoints.
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const string OAuthStateCookieName = "dip.oauth.state";

    private readonly GitHubOAuthClient _githubOAuthClient;
    private readonly JwtTokenIssuer _tokenIssuer;
    private readonly IDataProtector _stateProtector;
    private readonly AuthOptions _options;

    public AuthController(
        GitHubOAuthClient githubOAuthClient,
        JwtTokenIssuer tokenIssuer,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<AuthOptions> options)
    {
        _githubOAuthClient = githubOAuthClient;
        _tokenIssuer = tokenIssuer;
        _stateProtector = dataProtectionProvider.CreateProtector(
            "dip.github-oauth.state.v1");
        _options = options.Value;
    }

    [AllowAnonymous]
    [HttpGet("github/login")]
    public IActionResult GitHubLogin()
    {
        var nonceBytes = RandomNumberGenerator.GetBytes(32);
        var nonce = Convert.ToHexString(nonceBytes);
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var protectedState = _stateProtector.Protect(
            $"{issuedAt.ToString(CultureInfo.InvariantCulture)}:{nonce}");
        var callbackUrl = BuildCallbackUrl();

        Response.Cookies.Append(
            OAuthStateCookieName,
            nonce,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10)
            });

        return Redirect(
            _githubOAuthClient.BuildAuthorizeUrl(
                protectedState,
                callbackUrl));
    }

    [AllowAnonymous]
    [HttpGet("github/callback")]
    public async Task<ActionResult<object>> GitHubCallback(
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(new { error = "missing_oauth_parameters" });
        }

        string payload;
        try
        {
            payload = _stateProtector.Unprotect(state);
        }
        catch (CryptographicException)
        {
            return BadRequest(new { error = "invalid_oauth_state" });
        }

        var separator = payload.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0
            || !long.TryParse(
                payload[..separator],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var issuedAt))
        {
            return BadRequest(new { error = "invalid_oauth_state" });
        }

        if (DateTimeOffset.UtcNow
                - DateTimeOffset.FromUnixTimeSeconds(issuedAt)
            > TimeSpan.FromMinutes(10))
        {
            return BadRequest(new { error = "expired_oauth_state" });
        }

        var expectedNonce = payload[(separator + 1)..];
        if (!Request.Cookies.TryGetValue(
                OAuthStateCookieName,
                out var cookieNonce)
            || !FixedTimeEquals(expectedNonce, cookieNonce))
        {
            return BadRequest(new { error = "oauth_state_cookie_mismatch" });
        }

        Response.Cookies.Delete(OAuthStateCookieName);

        var callbackUrl = BuildCallbackUrl();
        var user = await _githubOAuthClient.ExchangeCodeAsync(
            code,
            callbackUrl,
            cancellationToken);
        var token = _tokenIssuer.CreateToken(user);
        var role = _tokenIssuer.ResolveRole(user.Login);

        if (!string.IsNullOrWhiteSpace(_options.WebCallbackUrl))
        {
            if (!Uri.TryCreate(_options.WebCallbackUrl, UriKind.Absolute, out var webCallback)
                || (webCallback.Scheme != Uri.UriSchemeHttp
                    && webCallback.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "Authentication:WebCallbackUrl must be an absolute HTTP(S) URL.");
            }

            return Content(
                BuildWebCallbackPage(
                    webCallback.ToString(),
                    token,
                    user.Login,
                    role),
                "text/html",
                Encoding.UTF8);
        }

        return Ok(new
        {
            accessToken = token,
            tokenType = "Bearer",
            expiresInSeconds = _tokenIssuer.LifetimeSeconds,
            githubLogin = user.Login,
            role
        });
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<object> Me() => Ok(new
    {
        id = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        login = User.FindFirst("name")?.Value ?? User.Identity?.Name,
        roles = User.FindAll("role")
            .Select(claim => claim.Value)
            .ToArray()
    });

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpGet("member-check")]
    public IActionResult MemberCheck() => Ok(new { authorized = true });

    [Authorize(Policy = Policies.Admin)]
    [HttpGet("admin-check")]
    public IActionResult AdminCheck() => Ok(new { authorized = true });

    private static string BuildWebCallbackPage(
        string callbackUrl,
        string token,
        string login,
        string role)
    {
        var action = System.Net.WebUtility.HtmlEncode(callbackUrl);
        var encodedToken = System.Net.WebUtility.HtmlEncode(token);
        var encodedLogin = System.Net.WebUtility.HtmlEncode(login);
        var encodedRole = System.Net.WebUtility.HtmlEncode(role);

        return $"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><title>DIP sign-in</title></head>
            <body>
              <form id="dip-auth" method="post" action="{action}">
                <input type="hidden" name="accessToken" value="{encodedToken}">
                <input type="hidden" name="githubLogin" value="{encodedLogin}">
                <input type="hidden" name="role" value="{encodedRole}">
              </form>
              <script>document.getElementById('dip-auth').submit();</script>
            </body>
            </html>
            """;
    }

    private string BuildCallbackUrl()
    {
        var callback = Url.ActionLink(
            nameof(GitHubCallback),
            "Auth",
            values: null,
            protocol: Request.Scheme,
            host: Request.Host.Value);

        return callback
            ?? throw new InvalidOperationException(
                "Unable to build the GitHub OAuth callback URL.");
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(
                leftBytes,
                rightBytes);
    }
}

public sealed class AuthOptions
{
    public const string SectionName = "Authentication";

    public bool RequireAuthenticatedUser { get; init; }
    public string WebCallbackUrl { get; init; } = string.Empty;
    public GitHubAuthOptions GitHub { get; init; } = new();
    public JwtAuthOptions Jwt { get; init; } = new();
}

public sealed class GitHubAuthOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string[] AdminLogins { get; init; } = [];
}

public sealed class JwtAuthOptions
{
    public string Issuer { get; init; } =
        "development-intelligence-platform";
    public string Audience { get; init; } =
        "development-intelligence-platform";
    public string SigningKey { get; init; } = string.Empty;
    public int LifetimeMinutes { get; init; } = 60;
}

public sealed record AuthenticatedGitHubUser(
    long Id,
    string Login,
    string? Name,
    string? AvatarUrl);

public sealed class GitHubOAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthOptions _options;

    public GitHubOAuthClient(
        HttpClient httpClient,
        IOptions<AuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string BuildAuthorizeUrl(
        string protectedState,
        string callbackUrl)
    {
        EnsureConfigured();

        return "https://github.com/login/oauth/authorize"
            + $"?client_id={Uri.EscapeDataString(_options.GitHub.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}"
            + $"&scope={Uri.EscapeDataString("read:user")}"
            + $"&state={Uri.EscapeDataString(protectedState)}";
    }

    public async Task<AuthenticatedGitHubUser> ExchangeCodeAsync(
        string code,
        string callbackUrl,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://github.com/login/oauth/access_token")
        {
            Content = JsonContent.Create(new
            {
                client_id = _options.GitHub.ClientId,
                client_secret = _options.GitHub.ClientSecret,
                code,
                redirect_uri = callbackUrl
            })
        };
        tokenRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        using var tokenResponse = await _httpClient.SendAsync(
            tokenRequest,
            cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        await using var tokenStream =
            await tokenResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var tokenDocument = await JsonDocument.ParseAsync(
            tokenStream,
            cancellationToken: cancellationToken);

        if (!tokenDocument.RootElement.TryGetProperty(
                "access_token",
                out var accessTokenElement))
        {
            throw new InvalidOperationException(
                "GitHub OAuth token response did not contain access_token.");
        }

        var accessToken = accessTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "GitHub OAuth returned an empty access token.");
        }

        using var userRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/user");
        userRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        userRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));
        userRequest.Headers.UserAgent.ParseAdd(
            "development-intelligence-platform/1.0");
        userRequest.Headers.Add(
            "X-GitHub-Api-Version",
            "2022-11-28");

        using var userResponse = await _httpClient.SendAsync(
            userRequest,
            cancellationToken);
        userResponse.EnsureSuccessStatusCode();

        await using var userStream =
            await userResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var userDocument = await JsonDocument.ParseAsync(
            userStream,
            cancellationToken: cancellationToken);
        var root = userDocument.RootElement;

        return new AuthenticatedGitHubUser(
            root.GetProperty("id").GetInt64(),
            root.GetProperty("login").GetString()
                ?? throw new InvalidOperationException(
                    "GitHub login is missing."),
            root.TryGetProperty("name", out var name)
                && name.ValueKind != JsonValueKind.Null
                    ? name.GetString()
                    : null,
            root.TryGetProperty("avatar_url", out var avatar)
                && avatar.ValueKind != JsonValueKind.Null
                    ? avatar.GetString()
                    : null);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.GitHub.ClientId)
            || string.IsNullOrWhiteSpace(_options.GitHub.ClientSecret))
        {
            throw new InvalidOperationException(
                "GitHub OAuth is not configured. Set "
                + "Authentication:GitHub:ClientId and "
                + "Authentication:GitHub:ClientSecret.");
        }
    }
}

public sealed class JwtTokenIssuer
{
    private readonly AuthOptions _options;

    public JwtTokenIssuer(IOptions<AuthOptions> options)
    {
        _options = options.Value;
    }

    public int LifetimeSeconds =>
        Math.Clamp(_options.Jwt.LifetimeMinutes, 5, 1440) * 60;

    public string ResolveRole(string login) =>
        _options.GitHub.AdminLogins.Contains(
            login,
            StringComparer.OrdinalIgnoreCase)
            ? Roles.Admin
            : Roles.Member;

    public string CreateToken(AuthenticatedGitHubUser user)
    {
        if (string.IsNullOrWhiteSpace(_options.Jwt.SigningKey)
            || _options.Jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:SigningKey must contain at least "
                + "32 characters when JWT issuance is used.");
        }

        var now = DateTime.UtcNow;
        var role = ResolveRole(user.Login);
        var id = user.Id.ToString(CultureInfo.InvariantCulture);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new("name", user.Login),
            new("role", role),
            new("github_login", user.Login)
        };

        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            claims.Add(new Claim("display_name", user.Name));
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            claims.Add(new Claim("avatar_url", user.AvatarUrl));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_options.Jwt.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Jwt.Issuer,
            audience: _options.Jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddSeconds(LifetimeSeconds),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
