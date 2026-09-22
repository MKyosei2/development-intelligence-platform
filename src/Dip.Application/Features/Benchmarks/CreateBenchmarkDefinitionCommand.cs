// Purpose: Defines the CreateBenchmarkDefinitionCommand application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record CreateBenchmarkDefinitionCommand(string Key, string Name, string Kind, BenchmarkExecutionSpec Configuration, bool Enabled = true);
