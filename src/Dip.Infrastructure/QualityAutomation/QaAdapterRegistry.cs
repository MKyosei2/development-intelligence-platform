// Purpose: Implements Q aA da pt er Re gi st ry infrastructure integration for q ua li ty au to ma ti on.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class QaAdapterRegistry : IQaAdapterRegistry
{
    private readonly IReadOnlyList<IQaAdapter> _adapters;
    public QaAdapterRegistry(IEnumerable<IQaAdapter> adapters) => _adapters = adapters.OrderByDescending(x => x.Priority).ThenBy(x => x.AdapterId, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> AdapterIds => _adapters.Select(x => x.AdapterId).ToArray();
    public IReadOnlyList<QaAdapterSelection> Resolve(LanguageId requested, QaTechnologyProfile profile, string workspace)
    {
        var languages = requested.IsAuto ? profile.Languages : new[] { requested }; var result = new List<QaAdapterSelection>();
        foreach (var language in languages.Where(x => !x.IsAuto && !x.IsMulti && !x.IsUnknown).Distinct())
        {
            var adapter = _adapters.FirstOrDefault(x => x.Supports(language, profile, workspace));
            if (adapter is not null) result.Add(new(language, adapter));
        }
        return result;
    }
}
