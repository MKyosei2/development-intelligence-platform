using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class QaAdapterRegistry(IEnumerable<IQaAdapter> adapters):IQaAdapterRegistry
{
    private readonly IReadOnlyDictionary<string,IQaAdapter> _adapters=adapters
        .GroupBy(x=>x.Language.Value,StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x=>x.Key,x=>x.OrderByDescending(a=>a.Priority).First(),StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LanguageId> SupportedLanguages=>_adapters.Values.Where(x=>!x.Language.Is("universal")).Select(x=>x.Language).OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray();

    public IQaAdapter Resolve(LanguageId language)=>TryResolve(language,out var adapter)?adapter!:throw new NotSupportedException($"No QA adapter is registered for '{language.Value}'.");

    public bool TryResolve(LanguageId language,out IQaAdapter? adapter)
    {
        if(_adapters.TryGetValue(language.Value,out adapter))return true;
        return _adapters.TryGetValue("universal",out adapter);
    }
}
