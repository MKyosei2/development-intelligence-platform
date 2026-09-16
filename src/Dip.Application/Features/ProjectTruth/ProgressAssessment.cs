// Purpose: Defines the ProgressAssessment application contract.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record ProgressAssessment(
    decimal VerifiedProgress,
    decimal? ReportedProgress);
