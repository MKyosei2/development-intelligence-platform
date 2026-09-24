// Purpose: Implements E xt er na lE ve nt In ge st io n infrastructure integration for e xt er na le ve nt s.
namespace Dip.Infrastructure.ExternalEvents;
public sealed class ExternalEventIngestionOptions
{
    public const string SectionName = "ExternalEventIngestion";
    public string IngestKey { get; set; } = string.Empty;
    public int MaximumPayloadBytes { get; set; } = 1_048_576;
}
