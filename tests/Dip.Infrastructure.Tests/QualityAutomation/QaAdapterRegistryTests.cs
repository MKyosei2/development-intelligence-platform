using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class QaAdapterRegistryTests
{
    [Fact]
    public void Resolve_PrefersEngineAwareUnityAndDoesNotForceCMakeForOtherCppBuilds()
    {
        var executor=new ProcessExecutor();var normalizer=new UniversalDiagnosticNormalizer();
        var adapters=new IQaAdapter[]{new UnityProjectQaAdapter(executor,normalizer),new CSharpQaEngine(executor,normalizer),new CppQaEngine(executor,normalizer),new GenericCommandQaAdapter(executor,normalizer)};
        var registry=new QaAdapterRegistry(adapters);
        var unity=new QaTechnologyProfile(".",[LanguageId.CSharp],["MSBuild/.NET"],["Unity"],["Unity"],Array.Empty<string>());
        registry.Resolve(LanguageId.CSharp,unity,".").Single().Adapter.AdapterId.Should().Be("unity");
        var native=new QaTechnologyProfile(".",[LanguageId.Cpp],["MSBuild/.NET"],Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>());
        registry.Resolve(LanguageId.Cpp,native,".").Single().Adapter.AdapterId.Should().Be("builtin-generic");
    }
}
