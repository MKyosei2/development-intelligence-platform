namespace Dip.Infrastructure.Artifacts;

public sealed class ArtifactStoreOptions
{
    public const string SectionName = "Artifacts";

    public string Provider { get; init; } = "Local";
    public string LocalRoot { get; init; } = "artifacts/durable";
    public int RetentionCleanupMinutes { get; init; } = 60;
    public int RetentionBatchSize { get; init; } = 100;
}