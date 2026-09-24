// Purpose: Implements A rt if ac tS to re infrastructure integration for a rt if ac ts.
namespace Dip.Infrastructure.Artifacts;
public sealed class ArtifactStoreOptions
{
    public const string SectionName = "Artifacts";
    public string Provider { get; init; } = "Local";
    public string LocalRoot { get; init; } = "artifacts/durable";
    public int RetentionCleanupMinutes { get; init; } = 60;
    public int RetentionBatchSize { get; init; } = 100;
    public MinioOptions Minio { get; init; } = new();
    public sealed class MinioOptions
    {
        public string Endpoint { get; init; } = "http://localhost:9000";
        public string Bucket { get; init; } = "dip-artifacts";
        public string AccessKey { get; init; } = "dipadmin";
        public string SecretKey { get; init; } = "dip-local-password";
        public string Region { get; init; } = "us-east-1";
    }
}
