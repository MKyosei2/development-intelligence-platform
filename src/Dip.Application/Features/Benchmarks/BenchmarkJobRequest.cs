// Purpose: Defines the BenchmarkJobRequest application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record BenchmarkJobRequest(Guid ProjectId,Guid DefinitionId,string SubjectKey,string CommitSha,string? EnvironmentFingerprint=null,Guid? PerformanceBudgetProfileId=null);
