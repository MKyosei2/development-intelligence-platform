// Purpose: Defines ProjectBlocker.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record ProjectBlocker(
    string Code,
    string Severity,
    string Title,
    string Detail,
    string SubjectKey,
    Guid? EvidenceId);
