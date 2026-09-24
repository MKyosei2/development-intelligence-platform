// Purpose: Defines QA waiver request, approval, rejection, revocation, and expiry invariants.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class QaWaiver
{
    private QaWaiver() { }

    public QaWaiver(
        Guid id,
        Guid projectId,
        string name,
        string reason,
        string actor,
        string? fingerprint,
        string? ruleId,
        string? pathPattern,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException(nameof(projectId));
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Waiver expiry must be after creation.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        Name = Text(name, 200);
        Reason = Text(reason, 2000);
        RequestedBy = Text(actor, 200);
        Fingerprint = Optional(fingerprint, 64);
        RuleId = Optional(ruleId, 300);
        PathPattern = Optional(pathPattern, 1000);
        CreatedAtUtc = UpdatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = WaiverStatus.Requested;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string RequestedBy { get; private set; } = string.Empty;
    public string? ApprovedBy { get; private set; }
    public string? DecisionReason { get; private set; }
    public string? Fingerprint { get; private set; }
    public string? RuleId { get; private set; }
    public string? PathPattern { get; private set; }
    public WaiverStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public long Version { get; private set; }

    public bool IsActive(DateTimeOffset now) =>
        Status == WaiverStatus.Active && now < ExpiresAtUtc;

    public void Approve(string actor, string reason, DateTimeOffset now)
    {
        if (Status != WaiverStatus.Requested)
            throw new InvalidOperationException("Only requested waivers can be approved.");
        if (string.Equals(RequestedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Waiver requester cannot approve the same waiver.");

        ApprovedBy = Text(actor, 200);
        DecisionReason = Text(reason, 2000);
        ApprovedAtUtc = UpdatedAtUtc = now;
        Status = WaiverStatus.Active;
        Version++;
    }

    public void Reject(string actor, string reason, DateTimeOffset now)
    {
        if (Status != WaiverStatus.Requested)
            throw new InvalidOperationException("Only requested waivers can be rejected.");

        ApprovedBy = Text(actor, 200);
        DecisionReason = Text(reason, 2000);
        UpdatedAtUtc = now;
        Status = WaiverStatus.Rejected;
        Version++;
    }

    public void Revoke(string actor, string reason, DateTimeOffset now)
    {
        if (Status != WaiverStatus.Active)
            throw new InvalidOperationException("Only active waivers can be revoked.");

        ApprovedBy = Text(actor, 200);
        DecisionReason = Text(reason, 2000);
        Status = WaiverStatus.Revoked;
        RevokedAtUtc = UpdatedAtUtc = now;
        Version++;
    }

    public void Revoke(DateTimeOffset now) =>
        Revoke(ApprovedBy ?? RequestedBy, "Revoked", now);

    public void Expire(DateTimeOffset now)
    {
        if (Status != WaiverStatus.Active || now < ExpiresAtUtc)
            return;

        Status = WaiverStatus.Expired;
        UpdatedAtUtc = now;
        Version++;
    }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}
