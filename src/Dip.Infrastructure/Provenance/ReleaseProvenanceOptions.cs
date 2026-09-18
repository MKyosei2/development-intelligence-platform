// Purpose: Defines configuration options for R el ea se Pr ov en an ce Op ti on s.
namespace Dip.Infrastructure.Provenance;

public sealed class ReleaseProvenanceOptions
{
    public const string SectionName = "ReleaseProvenance";
    public string AllowedRoot { get; set; } = ".";
}
