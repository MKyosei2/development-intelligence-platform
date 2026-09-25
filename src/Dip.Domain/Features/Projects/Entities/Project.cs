// Purpose: Defines Project domain behavior and invariants.
using Dip.Domain.ValueObjects;

namespace Dip.Domain.Entities;

public sealed class Project
{
    private Project() { }

    public Project(Guid id, string name, string repositoryUrl, string defaultBranch, DateTimeOffset createdAtUtc)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Rename(name);
        UpdateRepository(repositoryUrl, defaultBranch);
        CreatedAtUtc = UpdatedAtUtc = createdAtUtc;
        HealthScore = new HealthScore(100);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string RepositoryUrl { get; private set; } = string.Empty;
    public string DefaultBranch { get; private set; } = "main";
    public DateTimeOffset? ReleaseDateUtc { get; private set; }
    public HealthScore HealthScore { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Rename(string name)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
            throw new ArgumentException("Project name must be 2-120 characters.");
        Name = name;
    }

    public void UpdateRepository(string url, string branch)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Repository URL must be absolute HTTP(S).");

        branch = branch?.Trim() ?? string.Empty;
        if (branch.Length is < 1 or > 200)
            throw new ArgumentException("Invalid default branch.");

        RepositoryUrl = uri.ToString();
        DefaultBranch = branch;
    }

    public void SetReleaseDate(DateTimeOffset? value) => ReleaseDateUtc = value;

    public void UpdateDetails(
        string name,
        string repositoryUrl,
        string defaultBranch,
        DateTimeOffset? releaseDateUtc,
        DateTimeOffset at)
    {
        Rename(name);
        UpdateRepository(repositoryUrl, defaultBranch);
        SetReleaseDate(releaseDateUtc);
        UpdatedAtUtc = at;
    }

    public void UpdateHealth(HealthScore score, DateTimeOffset at)
    {
        HealthScore = score;
        UpdatedAtUtc = at;
    }
}
