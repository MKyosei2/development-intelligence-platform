// Purpose: Represents P ro je ct Cl ai m in the Evidence domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>
/// A status reported by a project member. A claim is intentionally stored
/// separately from machine-observed evidence so the two can be compared.
/// </summary>
public sealed class ProjectClaim
{
    private ProjectClaim() { }

    public ProjectClaim(
        Guid id,
        Guid projectId,
        string subjectKey,
        string claimedStatus,
        decimal? claimedProgress,
        string claimedBy,
        string? description,
        DateTimeOffset claimedAtUtc)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        if (claimedProgress is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(claimedProgress), "Progress must be between 0 and 100.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        SubjectKey = Required(subjectKey, 200, nameof(subjectKey));
        ClaimedStatus = Required(claimedStatus, 120, nameof(claimedStatus));
        ClaimedProgress = claimedProgress;
        ClaimedBy = Required(claimedBy, 200, nameof(claimedBy));
        Description = Optional(description, 4000, nameof(description));
        ClaimedAtUtc = claimedAtUtc;
        VerificationStatus = ClaimVerificationStatus.Unverified;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public string ClaimedStatus { get; private set; } = string.Empty;
    public decimal? ClaimedProgress { get; private set; }
    public string ClaimedBy { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset ClaimedAtUtc { get; private set; }
    public ClaimVerificationStatus VerificationStatus { get; private set; }
    public DateTimeOffset? LastVerifiedAtUtc { get; private set; }

    public void ApplyVerification(
        ClaimVerificationStatus status,
        DateTimeOffset verifiedAtUtc)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        VerificationStatus = status;
        LastVerifiedAtUtc = verifiedAtUtc;
    }

    private static string Required(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} must be 1-{maxLength} characters.", parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} must be at most {maxLength} characters.", parameterName);
        return normalized;
    }
}
