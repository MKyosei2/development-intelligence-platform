// Purpose: Defines the BenchmarkSample application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record BenchmarkSample(int Iteration,bool Passed,bool TimedOut,int? StatusCode,long DurationMicroseconds,string? Error);
