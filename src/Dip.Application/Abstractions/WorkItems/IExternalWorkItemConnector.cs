namespace Dip.Application.Abstractions.WorkItems;
public sealed record WorkItemDraft(string Title,string Body,string? Repository);
public sealed record CreatedWorkItem(string ExternalId,string Url,string Title);
public interface IExternalWorkItemConnector
{
    string ProviderName{get;}
    Task<CreatedWorkItem> CreateAsync(WorkItemDraft draft,CancellationToken ct);
}
