// Purpose: Defines the BenchmarkDefinitionDto application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record BenchmarkDefinitionDto(Guid Id,string Key,string Name,string Kind,bool Enabled,BenchmarkExecutionSpec Configuration);
