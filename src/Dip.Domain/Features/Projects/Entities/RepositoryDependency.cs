// Purpose: Defines Repository Dependency domain behavior and invariants for projects.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class RepositoryDependency
{
    private RepositoryDependency() { }
    public RepositoryDependency(Guid id, Guid projectId, Guid sourceRepositoryId, Guid targetRepositoryId, RepositoryDependencyKind kind, string contractKey, bool critical, DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty || sourceRepositoryId == Guid.Empty || targetRepositoryId == Guid.Empty || sourceRepositoryId == targetRepositoryId) throw new ArgumentException("Valid distinct repository IDs are required.");
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        SourceRepositoryId = sourceRepositoryId;
        TargetRepositoryId = targetRepositoryId;
        Kind = kind;
        ContractKey = Required(contractKey, 200, nameof(contractKey));
        Critical = critical;
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
    public Guid SourceRepositoryId
    {
        get;
        private set;
    }
    public Guid TargetRepositoryId
    {
        get;
        private set;
    }
    public RepositoryDependencyKind Kind
    {
        get;
        private set;
    }
    public string ContractKey
    {
        get;
        private set;
    } = string.Empty;
    public bool Critical
    {
        get;
        private set;
    }
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
