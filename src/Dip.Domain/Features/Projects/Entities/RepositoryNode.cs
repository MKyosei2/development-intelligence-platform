// Purpose: Defines Repository Node domain behavior and invariants for projects.
namespace Dip.Domain.Entities;
public sealed class RepositoryNode
{
    private RepositoryNode() { }
    public RepositoryNode(Guid id, Guid projectId, string key, string repositoryUrl, string defaultBranch, string role, DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        Key = Required(key, 160, nameof(key));
        RepositoryUrl = Required(repositoryUrl, 1000, nameof(repositoryUrl));
        DefaultBranch = Required(defaultBranch, 200, nameof(defaultBranch));
        Role = Required(role, 120, nameof(role));
        CreatedAtUtc = createdAtUtc;
    }
    public Guid Id
    {
        get;
        private set;
    }
    public Guid ProjectId
    {
        get;
        private set;
    }
    public string Key
    {
        get;
        private set;
    } = string.Empty;
    public string RepositoryUrl
    {
        get;
        private set;
    } = string.Empty;
    public string DefaultBranch
    {
        get;
        private set;
    } = string.Empty;
    public string Role
    {
        get;
        private set;
    } = string.Empty;
    public DateTimeOffset CreatedAtUtc
    {
        get;
        private set;
    }
    private static string Required(string? value, int max, string name)
    {
        var v = value?.Trim() ?? string.Empty;
        if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return v;
    }
}
