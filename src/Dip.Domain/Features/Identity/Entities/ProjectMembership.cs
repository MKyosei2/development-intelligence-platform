// Purpose: Represents P ro je ct Me mb er sh ip in the DIP domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>Project-scoped authorization assignment. SubjectId is the immutable identity provider subject (GitHub id for OAuth users).</summary>
public sealed class ProjectMembership
{
    private ProjectMembership() { }
    public ProjectMembership(Guid id, Guid projectId, string subjectId, string login, ProjectMembershipRole role, string grantedBy, DateTimeOffset now)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        SubjectId = Required(subjectId, 200);
        Login = Required(login, 200);
        Role = role;
        GrantedBy = Required(grantedBy, 200);
        CreatedAtUtc = UpdatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string SubjectId { get; private set; } = string.Empty;
    public string Login { get; private set; } = string.Empty;
    public ProjectMembershipRole Role { get; private set; }
    public string GrantedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void ChangeRole(ProjectMembershipRole role, string actor, DateTimeOffset now) { Role = role; GrantedBy = Required(actor, 200); UpdatedAtUtc = now; }
    private static string Required(string? value, int max) { var v = value?.Trim() ?? string.Empty; if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"Text must be 1-{max} characters."); return v; }
}
