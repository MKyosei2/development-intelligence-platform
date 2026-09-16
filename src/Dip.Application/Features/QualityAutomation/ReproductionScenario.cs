// Purpose: Defines ReproductionScenario.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record ReproductionScenario(Guid ProjectId,Guid FindingId,LanguageId Language,string WorkspacePath,string QaRunArtifactRoot,string Executable,IReadOnlyList<string> Arguments,IReadOnlyDictionary<string,string> EnvironmentVariables,string ExpectedFingerprint,int TimeoutSeconds,int AttemptCount,bool RecordScreen,bool RecordSystemAudio);
