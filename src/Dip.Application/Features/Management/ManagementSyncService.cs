using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed class ManagementSyncService
{
    private const string Absent = "{\"absent\":true}";
    private readonly IEnumerable<IManagementSyncAdapter> _adapters;
    private readonly ManagementAnalyticsService _analytics;
    private readonly WorkManagementService _work;
    private readonly ManagementOperationsService _operations;
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ISystemClock _clock;

    public ManagementSyncService(
        IEnumerable<IManagementSyncAdapter> adapters,
        ManagementAnalyticsService analytics,
        WorkManagementService work,
        ManagementOperationsService operations,
        DevelopmentGraphService graph,
        IDevelopmentGraphRepository repo,
        ISystemClock clock)
    {
        _adapters = adapters;
        _analytics = analytics;
        _work = work;
        _operations = operations;
        _graph = graph;
        _repo = repo;
        _clock = clock;
    }

    public async Task<ManagementSyncResult> SyncAsync(ManagementSyncCommand command, CancellationToken ct)
    {
        if (command.ProjectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(command));
        var adapter = _adapters.FirstOrDefault(x => x.ProviderName.Equals(command.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException($"Management sync provider '{command.Provider}' is not configured.");

        var direction = command.Direction;
        var external = direction is ManagementSyncDirection.Pull or ManagementSyncDirection.Bidirectional
            ? await adapter.PullAsync(command.ProjectId, ct)
            : Array.Empty<ManagementExternalItem>();
        var actions = (await _analytics.GetDecisionsAsync(command.ProjectId, ct)).Actions.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var externalByKey = external.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var links = await ReadLinksAsync(command.ProjectId, adapter.ProviderName, ct);
        var authority = await _operations.AuthorityForAsync(command.ProjectId, "Action", ct);
        var authorityName = authority?.Authority ?? "Manual";
        var preferAuthority = authority?.ConflictPolicy.Equals("PreferAuthority", StringComparison.OrdinalIgnoreCase) == true;
        var syncState = await _operations.GetSyncStateAsync(command.ProjectId, ct);
        var changes = new List<ManagementSyncChange>();
        var pulled = 0;
        var pushed = 0;
        var conflicts = 0;

        if (direction is ManagementSyncDirection.Pull or ManagementSyncDirection.Bidirectional)
        {
            foreach (var ext in external)
            {
                ct.ThrowIfCancellationRequested();
                var externalValue = Canonical(ext);
                if (!actions.TryGetValue(ext.Key, out var local))
                {
                    var prior = FindConflict(syncState, adapter.ProviderName, ext.Key);
                    if (direction == ManagementSyncDirection.Pull || (preferAuthority && IsExternalAuthority(authorityName, adapter.ProviderName)) || IsResolved(prior, "External"))
                    {
                        changes.Add(new("PullCreate", ext.Key, adapter.ProviderName, ext.ExternalId, IsResolved(prior, "External") ? "Applied resolved conflict: keep external item." : "External action does not exist in DIP.", Absent, externalValue));
                        if (!command.DryRun)
                        {
                            await _work.UpsertActionItemAsync(new(command.ProjectId, ext.Key, ext.Title, NormalizeStatus(ext.Status), "Medium", ext.OwnerRole, ext.DueAtUtc, 0m, null, null, ext.Url), ct);
                            await RecordLinkAsync(command.ProjectId, adapter.ProviderName, ext, ct);
                            if (IsResolved(prior, "External")) await _operations.MarkConflictAppliedAsync(command.ProjectId, prior!.Key, ct);
                        }
                        pulled++;
                    }
                    else if (IsResolved(prior, "DIP"))
                    {
                        changes.Add(new("IgnoreExternal", ext.Key, adapter.ProviderName, ext.ExternalId, "Applied resolved conflict: DIP keeps this external-only item out of the project. The remote item is not deleted automatically.", Absent, externalValue));
                        if (!command.DryRun) await _operations.MarkConflictAppliedAsync(command.ProjectId, prior!.Key, ct);
                    }
                    else if (IsAppliedSame(prior, "DIP", Absent, externalValue))
                    {
                        changes.Add(new("IgnoredExternal", ext.Key, adapter.ProviderName, ext.ExternalId, "Previously resolved to DIP; unchanged external-only item remains ignored.", Absent, externalValue));
                    }
                    else
                    {
                        changes.Add(new("Conflict", ext.Key, adapter.ProviderName, ext.ExternalId, "External-only item requires an explicit source-authority or conflict decision.", Absent, externalValue));
                        if (!command.DryRun) await _operations.RecordSyncConflictAsync(command.ProjectId, adapter.ProviderName, "Action", ext.Key, Absent, externalValue, ct);
                        conflicts++;
                    }
                    continue;
                }

                var localValue = Canonical(local);
                if (localValue == externalValue)
                {
                    if (!command.DryRun) await RecordLinkAsync(command.ProjectId, adapter.ProviderName, ext, ct);
                    continue;
                }

                var resolved = FindConflict(syncState, adapter.ProviderName, local.Key, "Resolved");
                var effectiveAuthority = IsResolved(resolved, "DIP")
                    ? "DIP"
                    : IsResolved(resolved, "External")
                        ? adapter.ProviderName
                        : preferAuthority
                            ? authorityName
                            : "Manual";

                if (IsExternalAuthority(effectiveAuthority, adapter.ProviderName))
                {
                    changes.Add(new("PullUpdate", ext.Key, adapter.ProviderName, ext.ExternalId, resolved is null ? $"{adapter.ProviderName} is authoritative under PreferAuthority." : "Applied resolved conflict: keep external value.", localValue, externalValue));
                    if (!command.DryRun)
                    {
                        await _work.UpsertActionItemAsync(new(command.ProjectId, local.Key, ext.Title, NormalizeStatus(ext.Status), local.Priority, ext.OwnerRole, ext.DueAtUtc, local.CompletionPercent, local.DecisionKey, TargetFrom(local.Target), ext.Url), ct);
                        await RecordLinkAsync(command.ProjectId, adapter.ProviderName, ext, ct);
                        if (resolved is not null) await _operations.MarkConflictAppliedAsync(command.ProjectId, resolved.Key, ct);
                    }
                    pulled++;
                }
                else if (effectiveAuthority.Equals("DIP", StringComparison.OrdinalIgnoreCase) && direction == ManagementSyncDirection.Bidirectional)
                {
                    var remote = await PushAsync(adapter, command.ProjectId, local, ext.ExternalId, command.DryRun, ct);
                    changes.Add(new("PushUpdate", local.Key, adapter.ProviderName, ext.ExternalId, resolved is null ? "DIP is authoritative under PreferAuthority." : "Applied resolved conflict: keep DIP value.", localValue, externalValue));
                    if (remote is not null) await RecordLinkAsync(command.ProjectId, adapter.ProviderName, remote, ct);
                    if (!command.DryRun && resolved is not null) await _operations.MarkConflictAppliedAsync(command.ProjectId, resolved.Key, ct);
                    pushed++;
                }
                else
                {
                    changes.Add(new("Conflict", local.Key, adapter.ProviderName, ext.ExternalId, direction == ManagementSyncDirection.Pull && effectiveAuthority.Equals("DIP", StringComparison.OrdinalIgnoreCase) ? "DIP resolution cannot be pushed during a Pull-only run." : "Values differ and conflict policy requires manual resolution.", localValue, externalValue));
                    if (!command.DryRun) await _operations.RecordSyncConflictAsync(command.ProjectId, adapter.ProviderName, "Action", local.Key, localValue, externalValue, ct);
                    conflicts++;
                }
            }
        }

        if (direction is ManagementSyncDirection.Push or ManagementSyncDirection.Bidirectional)
        {
            foreach (var local in actions.Values.Where(x => !externalByKey.ContainsKey(x.Key)))
            {
                ct.ThrowIfCancellationRequested();
                var localValue = Canonical(local);
                links.TryGetValue(local.Key, out var externalId);

                if (direction == ManagementSyncDirection.Push)
                {
                    changes.Add(new("PushCreate", local.Key, adapter.ProviderName, externalId, "Explicit Push run: DIP action is not present in the external set.", localValue, Absent));
                    var remote = await PushAsync(adapter, command.ProjectId, local, externalId, command.DryRun, ct);
                    if (remote is not null) await RecordLinkAsync(command.ProjectId, adapter.ProviderName, remote, ct);
                    pushed++;
                    continue;
                }

                var prior = FindConflict(syncState, adapter.ProviderName, local.Key);
                if ((preferAuthority && authorityName.Equals("DIP", StringComparison.OrdinalIgnoreCase)) || IsResolved(prior, "DIP"))
                {
                    changes.Add(new("PushCreate", local.Key, adapter.ProviderName, externalId, IsResolved(prior, "DIP") ? "Applied resolved conflict: keep DIP item." : "DIP is authoritative under PreferAuthority.", localValue, Absent));
                    var remote = await PushAsync(adapter, command.ProjectId, local, externalId, command.DryRun, ct);
                    if (remote is not null) await RecordLinkAsync(command.ProjectId, adapter.ProviderName, remote, ct);
                    if (!command.DryRun && IsResolved(prior, "DIP")) await _operations.MarkConflictAppliedAsync(command.ProjectId, prior!.Key, ct);
                    pushed++;
                }
                else if (IsResolved(prior, "External"))
                {
                    changes.Add(new("ArchiveLocal", local.Key, adapter.ProviderName, externalId, "Applied resolved conflict: keep external absence. The DIP action is soft-archived, not deleted.", localValue, Absent));
                    if (!command.DryRun)
                    {
                        await _operations.ArchiveAsync(new(command.ProjectId, "action", local.Key, $"management-sync:{adapter.ProviderName}", "External absence selected during sync conflict resolution."), ct);
                        await _operations.MarkConflictAppliedAsync(command.ProjectId, prior!.Key, ct);
                    }
                    pulled++;
                }
                else
                {
                    changes.Add(new("Conflict", local.Key, adapter.ProviderName, externalId, "DIP-only item requires an explicit source-authority or conflict decision.", localValue, Absent));
                    if (!command.DryRun) await _operations.RecordSyncConflictAsync(command.ProjectId, adapter.ProviderName, "Action", local.Key, localValue, Absent, ct);
                    conflicts++;
                }
            }
        }

        return new(command.ProjectId, adapter.ProviderName, pulled, pushed, conflicts, command.DryRun, changes, _clock.UtcNow);
    }

    private static SyncConflictDto? FindConflict(SyncStateOverviewDto state, string provider, string itemKey, string? status = null)
        => state.Conflicts
            .Where(x => x.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase)
                        && x.DataType.Equals("Action", StringComparison.OrdinalIgnoreCase)
                        && x.ItemKey.Equals(itemKey, StringComparison.OrdinalIgnoreCase)
                        && (status is null || x.Status.Equals(status, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.ResolvedAtUtc ?? x.DetectedAtUtc)
            .FirstOrDefault();

    private static bool IsResolved(SyncConflictDto? conflict, string resolution)
        => conflict is not null && conflict.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) && conflict.Resolution.Equals(resolution, StringComparison.OrdinalIgnoreCase);

    private static bool IsAppliedSame(SyncConflictDto? conflict, string resolution, string dipValue, string externalValue)
        => conflict is not null
           && conflict.Status.Equals("Applied", StringComparison.OrdinalIgnoreCase)
           && conflict.Resolution.Equals(resolution, StringComparison.OrdinalIgnoreCase)
           && conflict.DipValue == dipValue
           && conflict.ExternalValue == externalValue;

    private static async Task<ManagementExternalItem?> PushAsync(IManagementSyncAdapter adapter, Guid projectId, ManagementActionDto action, string? externalId, bool dryRun, CancellationToken ct)
        => dryRun ? null : await adapter.UpsertAsync(projectId, action, externalId, ct);

    private async Task<Dictionary<string, string>> ReadLinksAsync(Guid projectId, string provider, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.ExternalSyncState, ct);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in nodes.Where(x => x.Status != "Archived" && WorkManagementService.MetaString(x, "Kind") == "Link" && WorkManagementService.MetaString(x, "Provider").Equals(provider, StringComparison.OrdinalIgnoreCase)))
        {
            var key = WorkManagementService.MetaString(n, "ItemKey");
            var id = WorkManagementService.MetaString(n, "ExternalId");
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(id)) result[key] = id;
        }
        return result;
    }

    private async Task RecordLinkAsync(Guid projectId, string provider, ManagementExternalItem item, CancellationToken ct)
    {
        var safe = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(provider + ":" + item.Key))).ToLowerInvariant()[..20];
        var meta = JsonSerializer.Serialize(new { Kind = "Link", Provider = provider, DataType = "Action", ItemKey = item.Key, item.ExternalId, item.Url, item.Revision, item.UpdatedAtUtc });
        await _graph.UpsertNodeAsync(new(projectId, DevelopmentGraphNodeType.ExternalSyncState, $"sync-link:{safe}", $"{provider} {item.Key}", "Active", provider, item.Url, meta), ct);
    }

    private static bool IsExternalAuthority(string authority, string provider)
        => authority.Equals(provider, StringComparison.OrdinalIgnoreCase) || authority.Equals("External", StringComparison.OrdinalIgnoreCase);

    private static string Canonical(ManagementActionDto a)
        => JsonSerializer.Serialize(new { a.Title, Status = NormalizeStatus(a.Status), Owner = a.OwnerRole ?? "", Due = a.DueAtUtc?.UtcDateTime.Date.ToString("yyyy-MM-dd") ?? "" });

    private static string Canonical(ManagementExternalItem a)
        => JsonSerializer.Serialize(new { a.Title, Status = NormalizeStatus(a.Status), Owner = a.OwnerRole ?? "", Due = a.DueAtUtc?.UtcDateTime.Date.ToString("yyyy-MM-dd") ?? "" });

    private static string NormalizeStatus(string status)
        => status.Trim().ToLowerInvariant() switch
        {
            "done" or "completed" or "closed" or "resolved" or "canceled" or "cancelled" => "Completed",
            "in progress" or "inprogress" or "started" or "active" => "InProgress",
            _ => "Open"
        };

    private static ManagementReference? TargetFrom(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        var i = target.IndexOf(':');
        return i <= 0 || i >= target.Length - 1 ? null : new(target[..i], target[(i + 1)..]);
    }
}
