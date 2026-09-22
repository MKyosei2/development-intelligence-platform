// Purpose: Defines QaToolExecution.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaToolExecution(string Tool, bool WasAvailable, bool Succeeded, int? ExitCode, long DurationMs, string StandardOutputPath, string StandardErrorPath, string Summary, string? Executable = null, string? Version = null, string? SandboxJson = null, long CpuTimeMs = 0);
