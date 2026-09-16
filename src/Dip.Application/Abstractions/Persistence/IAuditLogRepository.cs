// Purpose: Defines the IAuditLogRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry,CancellationToken ct);
    Task<IReadOnlyList<AuditLog>> ListByProjectAsync(Guid projectId,int take,CancellationToken ct);
    async Task<IReadOnlyList<AuditLog>> SearchAsync(Guid projectId,string? search,string? category,string? correlationId,DateTimeOffset? fromUtc,DateTimeOffset? toUtc,int take,CancellationToken ct)
    {
        var items=await ListByProjectAsync(projectId,Math.Clamp(Math.Max(take,500),1,5000),ct);
        return items.Where(x=>(string.IsNullOrWhiteSpace(search)||x.Action.Contains(search,StringComparison.OrdinalIgnoreCase)||x.Actor.Contains(search,StringComparison.OrdinalIgnoreCase)||(x.SubjectKey?.Contains(search,StringComparison.OrdinalIgnoreCase)??false))
            &&(string.IsNullOrWhiteSpace(category)||x.Category.Contains(category,StringComparison.OrdinalIgnoreCase))
            &&(string.IsNullOrWhiteSpace(correlationId)||(x.CorrelationId?.Contains(correlationId,StringComparison.OrdinalIgnoreCase)??false))
            &&(!fromUtc.HasValue||x.CreatedAtUtc>=fromUtc.Value)
            &&(!toUtc.HasValue||x.CreatedAtUtc<=toUtc.Value))
            .Take(Math.Clamp(take,1,5000)).ToArray();
    }
    async Task<AuditLogPage> SearchPageAsync(Guid projectId,string? search,string? category,string? correlationId,DateTimeOffset? fromUtc,DateTimeOffset? toUtc,DateTimeOffset? beforeUtc,Guid? beforeId,int take,CancellationToken ct)
    {
        var limit=Math.Clamp(take,1,500);
        var items=(await SearchAsync(projectId,search,category,correlationId,fromUtc,toUtc,5000,ct))
            .Where(x=>!beforeUtc.HasValue||x.CreatedAtUtc<beforeUtc.Value||(x.CreatedAtUtc==beforeUtc.Value&&(!beforeId.HasValue||x.Id.CompareTo(beforeId.Value)<0)))
            .OrderByDescending(x=>x.CreatedAtUtc).ThenByDescending(x=>x.Id)
            .Take(limit+1).ToArray();
        var page=items.Take(limit).ToArray();
        var last=items.Length>limit?page.LastOrDefault():null;
        return new AuditLogPage(page,last?.CreatedAtUtc,last?.Id);
    }
    async Task<AuditLog?> GetLatestByCategoryAsync(Guid projectId,string category,CancellationToken ct)
        =>(await ListByProjectAsync(projectId,5000,ct)).FirstOrDefault(x=>x.Category.Equals(category,StringComparison.OrdinalIgnoreCase));
}
