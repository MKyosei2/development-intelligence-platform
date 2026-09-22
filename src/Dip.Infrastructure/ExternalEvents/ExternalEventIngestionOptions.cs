// Purpose: Provides infrastructure behavior for this application boundary.
namespace Dip.Infrastructure.ExternalEvents;
public sealed class ExternalEventIngestionOptions
{
    public const string SectionName = "ExternalEventIngestion";
    public string IngestKey { get; set; } = string.Empty;
    public int MaximumPayloadBytes { get; set; } = 1_048_576;
}
