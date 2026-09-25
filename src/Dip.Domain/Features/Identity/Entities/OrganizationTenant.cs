// Purpose: Defines Organization Tenant domain behavior and invariants for identity.
namespace Dip.Domain.Entities;

public sealed class OrganizationTenant
{
    private OrganizationTenant() { }
    public OrganizationTenant(Guid id, string name, string slug, DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id; Name = Req(name, 200); Slug = Req(slug, 100).ToLowerInvariant(); CreatedAtUtc = now; UpdatedAtUtc = now; IsEnabled = true;
    }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void SetEnabled(bool enabled, DateTimeOffset now) { IsEnabled = enabled; UpdatedAtUtc = now; }
    private static string Req(string? v, int max) { var x = v?.Trim() ?? "";
    if (x.Length is < 1 || x.Length > max) throw new ArgumentException($"Value must be 1-{max} characters.");
     return x;
     }
}

public sealed class ProjectTenantBinding
{
    private ProjectTenantBinding() { }
    public ProjectTenantBinding(Guid id, Guid organizationId, Guid projectId, string? portfolioKey, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || projectId == Guid.Empty) throw new ArgumentException("OrganizationId and ProjectId are required.");
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        OrganizationId = organizationId;
        ProjectId = projectId;
        PortfolioKey = Clean(portfolioKey, 120);
        CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string? PortfolioKey { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var x = v.Trim();
        if (x.Length > max) throw new ArgumentException($"Value must be <= {max} characters.");
        return x;
    }
}
