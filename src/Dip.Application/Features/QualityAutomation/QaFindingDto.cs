// Purpose: Defines the QaFindingDto application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaFindingDto(Guid Id, string Category, string Severity, string Tool, string Title, string Message, string Fingerprint, string? FilePath, int? Line, string ReproductionStatus, int ReproducedAttempts, int TotalAttempts);
