// Purpose: Implements R el ea se Pr ov en an ce infrastructure integration for p ro ve na nc e.
namespace Dip.Infrastructure.Provenance;

public sealed class ReleaseProvenanceOptions
{
    public const string SectionName = "ReleaseProvenance";
    public string AllowedRoot { get; set; } = ".";
}
