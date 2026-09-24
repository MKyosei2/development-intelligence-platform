// Purpose: Coordinates A ge nt Fl ee t application operations for f le et.
using System.Security.Cryptography;
using System.Text;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Features.Fleet;

public sealed record CreateEnrollmentResult(Guid EnrollmentId, string EnrollmentCode, DateTimeOffset ExpiresAtUtc);
public sealed record EnrollAgentCommand(string EnrollmentCode, string AgentId, string MachineId, string PublicKeyPem, string Version, int PolicyVersion);
public sealed record AgentHeartbeatCommand(string AgentId, string Version, int PolicyVersion, string? HealthJson);
public sealed record AgentUpdateDecision(bool UpdateAvailable, string? Version, string? PackageUri, string? Sha256, string? Signature, string? MinimumServerVersion);

public sealed class AgentFleetService(IEnterpriseFleetRepository repo, IUnitOfWork uow, ISystemClock clock)
{
    public async Task<CreateEnrollmentResult> CreateEnrollmentAsync(Guid organizationId, Guid projectId, TimeSpan ttl, CancellationToken ct)
    {
        if (ttl < TimeSpan.FromMinutes(5) || ttl > TimeSpan.FromDays(7)) throw new ArgumentOutOfRangeException(nameof(ttl));
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var token = new AgentEnrollmentToken(Guid.NewGuid(), organizationId, projectId, Hash(raw), clock.UtcNow.Add(ttl), clock.UtcNow);
        await repo.AddEnrollmentAsync(token, ct); await uow.SaveChangesAsync(ct); return new(token.Id, raw, token.ExpiresAtUtc);
    }
    public async Task<AgentRegistration> EnrollAsync(EnrollAgentCommand c, CancellationToken ct)
    {
        var token = await repo.FindEnrollmentByHashAsync(Hash(c.EnrollmentCode), ct) ?? throw new UnauthorizedAccessException("Invalid enrollment code.");
        if (token.IsConsumed || token.ExpiresAtUtc <= clock.UtcNow) throw new UnauthorizedAccessException("Enrollment code expired or consumed.");
        if (await repo.FindAgentAsync(c.AgentId, ct) is not null) throw new InvalidOperationException("AgentId already registered.");
        token.Consume(clock.UtcNow); var agent = new AgentRegistration(Guid.NewGuid(), token.OrganizationId, token.ProjectId, c.AgentId, c.MachineId, c.PublicKeyPem, c.Version, c.PolicyVersion, clock.UtcNow);
        await repo.AddAgentAsync(agent, ct); await uow.SaveChangesAsync(ct); return agent;
    }
    public async Task HeartbeatAsync(AgentHeartbeatCommand c, CancellationToken ct)
    {
        var agent = await repo.FindAgentAsync(c.AgentId, ct) ?? throw new KeyNotFoundException("Agent not registered.");
        if (agent.Status == AgentFleetStatus.Revoked) throw new UnauthorizedAccessException("Agent revoked.");
         agent.Heartbeat(c.Version, c.PolicyVersion, c.HealthJson, clock.UtcNow);
         await uow.SaveChangesAsync(ct);
    }
    public Task<IReadOnlyList<AgentRegistration>> ListAsync(Guid organizationId, CancellationToken ct) => repo.ListAgentsAsync(organizationId, ct);
    public async Task<AgentRelease> CreateReleaseAsync(Guid organizationId, string version, AgentReleaseChannel channel, string packageUri, string sha256, string signature, string minimumServerVersion, int rolloutPercent, CancellationToken ct)
    { var item = new AgentRelease(Guid.NewGuid(), organizationId, version, channel, packageUri, sha256, signature, minimumServerVersion, rolloutPercent, clock.UtcNow);
     await repo.AddReleaseAsync(item, ct);
     await uow.SaveChangesAsync(ct);
     return item;
     }
    public async Task ActivateReleaseAsync(Guid releaseId, int rolloutPercent, CancellationToken ct) { var item = await repo.GetReleaseAsync(releaseId, ct) ?? throw new KeyNotFoundException("Release not found.");
     item.Activate(rolloutPercent, clock.UtcNow);
     await uow.SaveChangesAsync(ct);
     }
    public async Task<AgentUpdateDecision> GetUpdateAsync(string agentId, AgentReleaseChannel channel, CancellationToken ct)
    {
        var agent = await repo.FindAgentAsync(agentId, ct) ?? throw new KeyNotFoundException("Agent not found."); var releases = (await repo.ListReleasesAsync(agent.OrganizationId, ct)).Where(x => x.Channel == channel && x.State == AgentRolloutState.Active).OrderByDescending(x => x.CreatedAtUtc).ToArray();
        foreach (var release in releases) { var bucket = StableBucket(agent.AgentId, release.Id);
        if (bucket >= release.RolloutPercent) continue;
        if (string.Equals(agent.Version, release.Version, StringComparison.OrdinalIgnoreCase)) continue;
         return new(true, release.Version, release.PackageUri, release.Sha256, release.Signature, release.MinimumServerVersion);
         }
        return new(false, null, null, null, null, null);
    }
    private static int StableBucket(string agentId, Guid releaseId) { var b = SHA256.HashData(Encoding.UTF8.GetBytes(agentId + releaseId.ToString("N"))); return BitConverter.ToUInt16(b, 0) % 100; }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();
}
