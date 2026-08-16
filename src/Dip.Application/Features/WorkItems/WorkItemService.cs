using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Abstractions.WorkItems;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.WorkItems;
public sealed record WorkItemPreview(Guid IncidentId,string Title,string Body,string Repository);
public sealed record WorkItemLinkDto(Guid Id,Guid IncidentId,string Provider,string ExternalId,string Url,string Title,string Status,DateTimeOffset CreatedAtUtc);

public sealed class WorkItemService
{
    private readonly IIncidentRepository _incidents;private readonly IProjectRepository _projects;private readonly IExternalWorkItemRepository _repo;private readonly IReadOnlyDictionary<string,IExternalWorkItemConnector> _connectors;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public WorkItemService(IIncidentRepository incidents,IProjectRepository projects,IExternalWorkItemRepository repo,IEnumerable<IExternalWorkItemConnector> connectors,IUnitOfWork uow,ISystemClock clock){_incidents=incidents;_projects=projects;_repo=repo;_connectors=connectors.GroupBy(x=>x.ProviderName,StringComparer.OrdinalIgnoreCase).ToDictionary(x=>x.Key,x=>x.First(),StringComparer.OrdinalIgnoreCase);_uow=uow;_clock=clock;}
    public async Task<WorkItemPreview> PreviewAsync(Guid incidentId,CancellationToken ct){var incident=await _incidents.GetAsync(incidentId,ct)??throw new KeyNotFoundException($"Incident {incidentId} was not found.");var project=await _projects.GetByIdAsync(incident.ProjectId,ct)??throw new KeyNotFoundException("Project was not found.");var events=await _incidents.ListEventsAsync(incident.Id,ct);var repo=NormalizeRepository(project.RepositoryUrl);var title=$"[{incident.Severity}] {incident.Title}";var body=$"DIP Incident: {incident.Id}\n\nStatus: {incident.Status}\nFirst seen: {incident.FirstSeenAtUtc:O}\nLast seen: {incident.LastSeenAtUtc:O}\nOccurrences: {incident.OccurrenceCount}\nReproduced: {incident.ReproducedCount}\nRegression commit: {incident.SuspectedRegressionCommit??"not isolated"}\nRoot cause: {incident.RootCauseSummary??"not established"}\n\nTimeline events: {events.Count}\n\nThis issue was prepared from evidence in Development Intelligence Platform. Verify the evidence before applying a fix.";return new(incident.Id,title,body,repo);}
    public Task<WorkItemLinkDto> CreateAsync(Guid incidentId,CancellationToken ct)=>CreateAsync(incidentId,ExternalWorkItemProvider.GitHub,ct);
    public async Task<WorkItemLinkDto> CreateAsync(Guid incidentId,ExternalWorkItemProvider provider,CancellationToken ct)
    {
        var existing=await _repo.FindByIncidentAsync(incidentId,provider,ct);if(existing is not null)return Map(existing);var preview=await PreviewAsync(incidentId,ct);var incident=await _incidents.GetAsync(incidentId,ct)??throw new KeyNotFoundException();var key=provider.ToString();if(!_connectors.TryGetValue(key,out var connector))throw new InvalidOperationException($"{key} work-item connector is not registered.");var created=await connector.CreateAsync(new WorkItemDraft(preview.Title,preview.Body,preview.Repository),ct);var link=new ExternalWorkItemLink(Guid.NewGuid(),incident.ProjectId,incident.Id,provider,created.ExternalId,created.Url,created.Title,ExternalWorkItemStatus.Open,_clock.UtcNow);await _repo.AddAsync(link,ct);await _incidents.AddEventAsync(new IncidentEvent(Guid.NewGuid(),incident.Id,IncidentEventType.WorkItemLinked,$"{key} work item linked: {created.Title}",System.Text.Json.JsonSerializer.Serialize(new{provider=key,created.ExternalId,created.Url}),_clock.UtcNow),ct);await _uow.SaveChangesAsync(ct);return Map(link);
    }
    public IReadOnlyList<string> Providers=>_connectors.Keys.OrderBy(x=>x).ToArray();
    private static WorkItemLinkDto Map(ExternalWorkItemLink x)=>new(x.Id,x.IncidentId,x.Provider.ToString(),x.ExternalId,x.Url,x.Title,x.Status.ToString(),x.CreatedAtUtc);
    private static string NormalizeRepository(string url){var uri=new Uri(url);return uri.AbsolutePath.Trim('/').EndsWith(".git",StringComparison.OrdinalIgnoreCase)?uri.AbsolutePath.Trim('/')[..^4]:uri.AbsolutePath.Trim('/');}
}
