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
    private readonly IIncidentRepository _incidents;private readonly IProjectRepository _projects;private readonly IExternalWorkItemRepository _repo;private readonly IExternalWorkItemConnector _connector;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public WorkItemService(IIncidentRepository incidents,IProjectRepository projects,IExternalWorkItemRepository repo,IExternalWorkItemConnector connector,IUnitOfWork uow,ISystemClock clock){_incidents=incidents;_projects=projects;_repo=repo;_connector=connector;_uow=uow;_clock=clock;}
    public async Task<WorkItemPreview> PreviewAsync(Guid incidentId,CancellationToken ct){var incident=await _incidents.GetAsync(incidentId,ct)??throw new KeyNotFoundException($"Incident {incidentId} was not found.");var project=await _projects.GetByIdAsync(incident.ProjectId,ct)??throw new KeyNotFoundException("Project was not found.");var events=await _incidents.ListEventsAsync(incident.Id,ct);var repo=NormalizeRepository(project.RepositoryUrl);var title=$"[{incident.Severity}] {incident.Title}";var body=$"DIP Incident: {incident.Id}\n\nStatus: {incident.Status}\nFirst seen: {incident.FirstSeenAtUtc:O}\nLast seen: {incident.LastSeenAtUtc:O}\nOccurrences: {incident.OccurrenceCount}\nReproduced: {incident.ReproducedCount}\nRegression commit: {incident.SuspectedRegressionCommit??"not isolated"}\nRoot cause: {incident.RootCauseSummary??"not established"}\n\nTimeline events: {events.Count}\n\nThis issue was prepared from evidence in Development Intelligence Platform. Verify the evidence before applying a fix.";return new(incident.Id,title,body,repo);}
    public async Task<WorkItemLinkDto> CreateAsync(Guid incidentId,CancellationToken ct){var existing=await _repo.FindByIncidentAsync(incidentId,ExternalWorkItemProvider.GitHub,ct);if(existing is not null)return Map(existing);var preview=await PreviewAsync(incidentId,ct);var incident=await _incidents.GetAsync(incidentId,ct)??throw new KeyNotFoundException();var created=await _connector.CreateAsync(new WorkItemDraft(preview.Title,preview.Body,preview.Repository),ct);var link=new ExternalWorkItemLink(Guid.NewGuid(),incident.ProjectId,incident.Id,ExternalWorkItemProvider.GitHub,created.ExternalId,created.Url,created.Title,ExternalWorkItemStatus.Open,_clock.UtcNow);await _repo.AddAsync(link,ct);await _incidents.AddEventAsync(new IncidentEvent(Guid.NewGuid(),incident.Id,IncidentEventType.WorkItemLinked,$"GitHub issue linked: {created.Title}",System.Text.Json.JsonSerializer.Serialize(new{created.ExternalId,created.Url}),_clock.UtcNow),ct);await _uow.SaveChangesAsync(ct);return Map(link);}
    private static WorkItemLinkDto Map(ExternalWorkItemLink x)=>new(x.Id,x.IncidentId,x.Provider.ToString(),x.ExternalId,x.Url,x.Title,x.Status.ToString(),x.CreatedAtUtc);
    private static string NormalizeRepository(string url){var uri=new Uri(url);return uri.AbsolutePath.Trim('/').EndsWith(".git",StringComparison.OrdinalIgnoreCase)?uri.AbsolutePath.Trim('/')[..^4]:uri.AbsolutePath.Trim('/');}
}
