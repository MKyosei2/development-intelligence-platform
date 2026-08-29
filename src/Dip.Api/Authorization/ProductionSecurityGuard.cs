using Dip.Api.Controllers;

namespace Dip.Api.Authorization;

/// <summary>Fail closed on dangerous production defaults. Development/Testing remain convenient.</summary>
public static class ProductionSecurityGuard
{
    private static readonly string[] UnsafeFragments={"dip-local-password","local-development-webhook-secret","changeme","replace-me","example-secret"};
    public static void Validate(IWebHostEnvironment environment,IConfiguration configuration,AuthOptions auth)
    {
        if(!environment.IsProduction())return;
        var failures=new List<string>();
        if(!auth.RequireAuthenticatedUser)failures.Add("Authentication:RequireAuthenticatedUser must be true in Production.");
        if(string.IsNullOrWhiteSpace(auth.Jwt.SigningKey)||auth.Jwt.SigningKey.Length<64)failures.Add("Authentication:Jwt:SigningKey must be an explicit secret of at least 64 characters in Production.");
        if(string.IsNullOrWhiteSpace(auth.GitHub.ClientId)||string.IsNullOrWhiteSpace(auth.GitHub.ClientSecret))failures.Add("Authentication:GitHub ClientId and ClientSecret must be configured in Production.");
        if(auth.GitHub.AdminLogins is null||auth.GitHub.AdminLogins.Length==0)failures.Add("Authentication:GitHub:AdminLogins must include at least one recovery administrator in Production.");
        if(!configuration.GetValue("Qa:SandboxEnabled",true))failures.Add("Qa:SandboxEnabled must be true in Production.");
        if(!configuration.GetValue("Qa:RequireExternalNetworkIsolation",false))failures.Add("Qa:RequireExternalNetworkIsolation must be true in Production so untrusted QA execution fails closed unless the runtime attests network isolation.");
        if(!configuration.GetValue("Qa:RequireExternalFilesystemIsolation",false))failures.Add("Qa:RequireExternalFilesystemIsolation must be true in Production so untrusted QA execution fails closed unless the runtime provides a separate filesystem boundary.");
        var connection=configuration.GetConnectionString("PostgreSql")??string.Empty;
        if(string.IsNullOrWhiteSpace(connection))failures.Add("ConnectionStrings:PostgreSql must be configured in Production.");
        foreach(var fragment in UnsafeFragments)
            if(connection.Contains(fragment,StringComparison.OrdinalIgnoreCase))failures.Add("Production PostgreSQL connection string contains a known development/default credential.");
        var analysisApiKey=configuration["AnalysisService:ApiKey"]??string.Empty;
        if(string.IsNullOrWhiteSpace(analysisApiKey))failures.Add("AnalysisService:ApiKey must be configured in Production.");
        var redisConnection=configuration["Redis:ConnectionString"]??string.Empty;
        var redisPasswordParameter=string.Concat("pass","word=");
        if(string.IsNullOrWhiteSpace(redisConnection)||!redisConnection.Contains(redisPasswordParameter,StringComparison.OrdinalIgnoreCase))
            failures.Add("Redis:ConnectionString must use authenticated access in Production.");
        var storageSecret=configuration["Artifacts:Minio:SecretKey"]??configuration["Storage:SecretKey"]??string.Empty;
        foreach(var fragment in UnsafeFragments)
            if(storageSecret.Contains(fragment,StringComparison.OrdinalIgnoreCase))failures.Add("Production artifact storage secret contains a known development/default credential.");
        if(failures.Count>0)throw new InvalidOperationException("Unsafe production configuration:\n - "+string.Join("\n - ",failures.Distinct(StringComparer.Ordinal)));
    }
}
