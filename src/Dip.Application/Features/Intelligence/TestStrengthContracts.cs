namespace Dip.Application.Features.Intelligence;
public sealed record FuzzRequest(Guid ProjectId,string SubjectKey,string WorkspaceRelativePath,string Executable,IReadOnlyList<string> BaseArguments,int Cases=50,int TimeoutSeconds=20);
public sealed record FuzzResult(int Cases,int Crashes,int Timeouts,int NonZeroExits,string ArtifactPath,string EvidenceJson);
public sealed record MutationRequest(Guid ProjectId,string SubjectKey,string WorkspaceRelativePath,string SourceRelativePath,string TestCommand,IReadOnlyList<string> TestArguments,int MaxMutants=12);
public sealed record MutationResult(int Mutants,int Killed,int Survived,decimal MutationScore,IReadOnlyList<string> SurvivingMutants,string EvidenceJson);
public interface IFuzzRunner{Task<FuzzResult> RunAsync(FuzzRequest request,string artifactRoot,CancellationToken ct);}
public interface IMutationRunner{Task<MutationResult> RunAsync(MutationRequest request,string artifactRoot,CancellationToken ct);}
