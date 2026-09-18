// Purpose: Defines configuration options for E xt er na lE ve nt In ge st io nO pt io ns.
namespace Dip.Infrastructure.ExternalEvents;
public sealed class ExternalEventIngestionOptions
{
    public const string SectionName="ExternalEventIngestion";
    public string IngestKey{get;set;}=string.Empty;
    public int MaximumPayloadBytes{get;set;}=1_048_576;
}
