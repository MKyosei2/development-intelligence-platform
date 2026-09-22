// Purpose: Defines the QaAdapterResult application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaAdapterResult(LanguageId Language, IReadOnlyList<QaToolExecution> Tools, IReadOnlyList<QaFindingCandidate> Findings, string SummaryJson);
