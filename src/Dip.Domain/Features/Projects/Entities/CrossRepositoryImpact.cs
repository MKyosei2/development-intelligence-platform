// Purpose: Defines C ro ss Re po si to ry Im pa ct domain behavior and invariants for p ro je ct s.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class CrossRepositoryImpact
{
    private CrossRepositoryImpact() { }
    public CrossRepositoryImpact(Guid id, Guid projectId, string commitSha, Guid sourceRepositoryId, CrossRepositoryImpactLevel level, string affectedRepositoryIdsJson, string reasonsJson, DateTimeOffset analyzedAtUtc)
    {
        if (projectId == Guid.Empty || sourceRepositoryId == Guid.Empty) throw new ArgumentException("Project and source repository IDs are required.");
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        CommitSha = Required(commitSha, 64, nameof(commitSha));
        SourceRepositoryId = sourceRepositoryId;
        Level = level;
        AffectedRepositoryIdsJson = Required(affectedRepositoryIdsJson, 16000, nameof(affectedRepositoryIdsJson));
        ReasonsJson = Required(reasonsJson, 16000, nameof(reasonsJson));
        AnalyzedAtUtc = analyzedAtUtc;
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
    public string CommitSha
    {
        get;
        private set;
    } = string.Empty;
    public Guid SourceRepositoryId
    {
        get;
        private set;
    }
    public CrossRepositoryImpactLevel Level
    {
        get;
        private set;
    }
    public string AffectedRepositoryIdsJson
    {
        get;
        private set;
    } = "[]";
    public string ReasonsJson
    {
        get;
        private set;
    } = "[]";
    public DateTimeOffset AnalyzedAtUtc
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
