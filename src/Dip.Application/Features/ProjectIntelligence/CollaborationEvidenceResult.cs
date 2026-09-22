// Purpose: Defines the CollaborationEvidenceResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record CollaborationEvidenceResult(string ObservationKey, string SubjectKey, string Confidence, bool EvidenceBacked, IReadOnlyList<string> References, int AutoResolved, DateTimeOffset IngestedAtUtc);
