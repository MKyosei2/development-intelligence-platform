// Purpose: Implements Production Security Guard responsibilities for authorization.
using Dip.Api.Controllers;

namespace Dip.Api.Authorization;

/// <summary>Fail closed on dangerous production defaults. Development/Testing remain convenient.</summary>
public static class ProductionSecurityGuard
{
    private static readonly string[] UnsafeFragments = { "dip-local-password", "local-development-webhook-secret", "changeme", "replace-me", "example-secret" };
    public static void Validate(IWebHostEnvironment environment, IConfiguration configuration, AuthOptions auth)
    {
        if (!environment.IsProduction()) return;
        var failures = new List<string>();
        if (!auth.RequireAuthenticatedUser) failures.Add("Authentication:RequireAuthenticatedUser must be true in Production.");
        if (string.IsNullOrWhiteSpace(auth.Jwt.SigningKey) || auth.Jwt.SigningKey.Length < 64) failures.Add("Authentication:Jwt:SigningKey must be an explicit secret of at least 64 characters in Production.");
        if (string.IsNullOrWhiteSpace(auth.GitHub.ClientId) || string.IsNullOrWhiteSpace(auth.GitHub.ClientSecret)) failures.Add("Authentication:GitHub ClientId and ClientSecret must be configured in Production.");
        if (auth.GitHub.AdminLogins is null || auth.GitHub.AdminLogins.Length == 0) failures.Add("Authentication:GitHub:AdminLogins must include at least one recovery administrator in Production.");
        if (!configuration.GetValue("Qa:SandboxEnabled", true)) failures.Add("Qa:SandboxEnabled must be true in Production.");
        if (!configuration.GetValue("Qa:RequireExternalNetworkIsolation", false)) failures.Add("Qa:RequireExternalNetworkIsolation must be true in Production.");
        if (!configuration.GetValue("Qa:RequireExternalFilesystemIsolation", false)) failures.Add("Qa:RequireExternalFilesystemIsolation must be true in Production.");
        var connection = configuration.GetConnectionString("PostgreSql") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connection)) failures.Add("ConnectionStrings:PostgreSql must be configured in Production.");
        foreach (var fragment in UnsafeFragments)
            if (connection.Contains(fragment, StringComparison.OrdinalIgnoreCase)) failures.Add("Production PostgreSQL connection string contains a known development/default credential.");
        var analysisApiKey = configuration["AnalysisService:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(analysisApiKey)) failures.Add("AnalysisService:ApiKey must be configured in Production.");
        var provider = configuration["Artifacts:Provider"] ?? "Local";
        if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            var artifactRoot = configuration["Artifacts:LocalRoot"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(artifactRoot) || !Path.IsPathFullyQualified(artifactRoot)) failures.Add("Artifacts:LocalRoot must be an explicit absolute path in Production.");
        }
        else if (provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = configuration["Artifacts:Minio:Endpoint"] ?? string.Empty;
            var secret = configuration["Artifacts:Minio:SecretKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(endpoint)) failures.Add("Artifacts:Minio:Endpoint must be configured when the Minio provider is selected in Production.");
            if (string.IsNullOrWhiteSpace(secret)) failures.Add("Artifacts:Minio:SecretKey must be configured when the Minio provider is selected in Production.");
            foreach (var fragment in UnsafeFragments)
                if (secret.Contains(fragment, StringComparison.OrdinalIgnoreCase)) failures.Add("Production Minio secret contains a known development/default credential.");
        }
        else failures.Add($"Unsupported Artifacts:Provider '{provider}' in Production.");
        var workspaceRoot = configuration["Qa:WorkspaceRoot"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Path.IsPathFullyQualified(workspaceRoot)) failures.Add("Qa:WorkspaceRoot must be an explicit absolute path in Production.");
        if (failures.Count > 0) throw new InvalidOperationException("Unsafe production configuration:\n - " + string.Join("\n - ", failures.Distinct(StringComparer.Ordinal)));
    }
}
