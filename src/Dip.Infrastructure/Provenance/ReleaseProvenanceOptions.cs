namespace Dip.Infrastructure.Provenance;

public sealed class ReleaseProvenanceOptions
{
    public const string SectionName = "ReleaseProvenance";
    public string AllowedRoot { get; set; } = ".";
}
