// Purpose: Defines the DiagnosticSchemaDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record DiagnosticSchemaDto(string SchemaVersion, string NormalizerVersion, IReadOnlyList<string> SupportedFormats, string CompatibilityPolicy);
