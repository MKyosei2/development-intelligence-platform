using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Dip.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "dip.web.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.LoginPath = "/auth/login";
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<HttpClient>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var authenticationState = services.GetRequiredService<AuthenticationStateProvider>();
    var handler = new ApiAuthenticationHandler(authenticationState)
    {
        InnerHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        }
    };
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/")
    };
});
builder.Services.AddScoped<ApiClient>();
builder.Services.AddHttpClient("dip-api-proxy", client =>
{
    client.BaseAddress = new Uri(EnsureTrailingSlash(
        builder.Configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/"));
    client.Timeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

var requireAuthentication =
    app.Configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser") ?? false;
var publicApiBaseUrl =
    app.Configuration["Api:PublicBaseUrl"]
    ?? app.Configuration["Api:BaseUrl"]
    ?? "http://127.0.0.1:5080/";
var internalApiBaseUrl =
    app.Configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/";

app.MapGet("/auth/login", () =>
{
    if (!requireAuthentication)
        return Results.Redirect("/");

    var target = new Uri(
        new Uri(EnsureTrailingSlash(publicApiBaseUrl)),
        "api/v1/auth/github/login");
    return Results.Redirect(target.ToString());
});

app.MapPost("/auth/callback", async (HttpContext context) =>
{
    if (!requireAuthentication)
        return Results.Redirect("/");

    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var token = form["accessToken"].ToString();
    if (string.IsNullOrWhiteSpace(token))
        return Results.BadRequest("GitHub sign-in did not return an API token.");

    using var validationClient = new HttpClient
    {
        BaseAddress = new Uri(EnsureTrailingSlash(internalApiBaseUrl)),
        Timeout = TimeSpan.FromSeconds(10)
    };
    using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/me");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    using var response = await validationClient.SendAsync(request, context.RequestAborted);
    if (!response.IsSuccessStatusCode)
        return Results.Unauthorized();

    var me = await response.Content.ReadFromJsonAsync<ApiMeResponse>(
        cancellationToken: context.RequestAborted);
    if (me is null || string.IsNullOrWhiteSpace(me.Id) || string.IsNullOrWhiteSpace(me.Login))
        return Results.BadRequest("API identity response was incomplete.");

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, me.Id),
        new(ClaimTypes.Name, me.Login),
        new("github_login", me.Login),
        new("dip_api_token", token)
    };
    foreach (var role in me.Roles ?? Array.Empty<string>())
        claims.Add(new Claim(ClaimTypes.Role, role));

    var identity = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme,
        ClaimTypes.Name,
        ClaimTypes.Role);
    var principal = new ClaimsPrincipal(identity);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
        });

    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapGet("/downloads/qa/{runId:guid}/artifacts/{**relativePath}", async (
    Guid runId,
    string relativePath,
    HttpContext context,
    IHttpClientFactory clients) =>
{
    var encoded = EncodeRelativePath(relativePath);
    if (encoded is null)
        return Results.BadRequest("Invalid artifact path.");

    return await ProxyApiDownloadAsync(
        $"api/v1/qa/runs/{runId}/artifacts/{encoded}",
        Path.GetFileName(relativePath),
        context,
        clients,
        requireAuthentication);
});

app.MapGet("/downloads/projects/{projectId:guid}/management.xlsx", async (
    Guid projectId,
    HttpContext context,
    IHttpClientFactory clients) =>
{
    return await ProxyApiDownloadAsync(
        $"api/v1/project-control/projects/{projectId}/management.xlsx",
        $"dip-management-{projectId:N}.xlsx",
        context,
        clients,
        requireAuthentication);
});

app.MapRazorComponents<Dip.Web.App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string EnsureTrailingSlash(string value) =>
    value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";

static string? EncodeRelativePath(string value)
{
    if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
        return null;

    var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        return null;

    return string.Join('/', segments.Select(Uri.EscapeDataString));
}

static async Task<IResult> ProxyApiDownloadAsync(
    string apiPath,
    string fallbackFileName,
    HttpContext context,
    IHttpClientFactory clients,
    bool requireAuthentication)
{
    var token = context.User.FindFirst("dip_api_token")?.Value;
    if (requireAuthentication && string.IsNullOrWhiteSpace(token))
        return Results.Unauthorized();

    var client = clients.CreateClient("dip-api-proxy");
    var request = new HttpRequestMessage(HttpMethod.Get, apiPath);
    if (!string.IsNullOrWhiteSpace(token))
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await client.SendAsync(
        request,
        HttpCompletionOption.ResponseHeadersRead,
        context.RequestAborted);
    request.Dispose();

    if (!response.IsSuccessStatusCode)
    {
        var status = (int)response.StatusCode;
        response.Dispose();
        return Results.StatusCode(status);
    }

    var stream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
    context.Response.OnCompleted(() =>
    {
        stream.Dispose();
        response.Dispose();
        return Task.CompletedTask;
    });

    var contentType = response.Content.Headers.ContentType?.ToString()
        ?? "application/octet-stream";
    var suppliedName = response.Content.Headers.ContentDisposition?.FileNameStar
        ?? response.Content.Headers.ContentDisposition?.FileName;
    var fileName = string.IsNullOrWhiteSpace(suppliedName)
        ? fallbackFileName
        : suppliedName.Trim('"');

    return Results.Stream(
        stream,
        contentType,
        fileDownloadName: fileName,
        enableRangeProcessing: true);
}

file sealed record ApiMeResponse(string Id, string Login, string[] Roles);
