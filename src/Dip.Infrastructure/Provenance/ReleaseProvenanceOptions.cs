// Purpose: Implements Release Provenance Options infrastructure behavior for this repository area.
namespace Dip.Infrastructure.Provenance;

public sealed class ReleaseProvenanceOptions
{
    public const string SectionName = "ReleaseProvenance";
    public string AllowedRoot { get; set; } = ".";
}
