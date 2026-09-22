// Purpose: Defines the QaFindingCandidate application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaFindingCandidate(string Category, Severity Severity, string Tool, string Title, string Message, string? FilePath = null, int? Line = null, int? Column = null, string? Function = null, string? ExceptionType = null, string? StackTrace = null, string? RuleId = null, string DiagnosticSchemaVersion = "dip.finding.v2", string? SourceFormat = null, string? SourceFormatVersion = null);
