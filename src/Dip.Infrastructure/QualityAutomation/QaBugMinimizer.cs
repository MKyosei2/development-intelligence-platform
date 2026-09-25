// Purpose: Implements QA Bug Minimizer infrastructure behavior for quality automation.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class QaBugMinimizer(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer, IQaWorkspaceResolver workspaceResolver, IQaFindingRepository findings, IQaGovernanceRepository governance, IUnitOfWork uow, ISystemClock clock) : IQaBugMinimizer
{
    public async Task<BugMinimizationDto> MinimizeAsync(BugMinimizationCommand command, CancellationToken ct)
    {
        var finding = await findings.GetAsync(command.FindingId, ct) ?? throw new KeyNotFoundException("QA finding not found.");
        if (finding.ProjectId != command.ProjectId) throw new InvalidOperationException("Finding belongs to another project.");
        var workspace = workspaceResolver.Resolve(command.WorkspaceRelativePath);
        var exe = ResolveExecutable(workspace, command.Executable);
        var original = command.Arguments.ToList();
        var current = original.ToList();
        var temp = Path.Combine(Path.GetTempPath(), "dip-qa-minimize", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var attempts = 0;
        try
        {
            if (!await ReproducesAsync(exe, current, workspace, temp, finding, Math.Clamp(command.TimeoutSeconds, 5, 120), ct)) return await StoreAsync(QaMinimizationStatus.NoReduction, original, current, $"Original reproduction command did not reproduce fingerprint {finding.Fingerprint}; no unsafe reduction was attempted.", command, finding, ct);
            var changed = true;
            while (changed && current.Count > 0 && attempts < 50)
            {
                changed = false;
                for (var i = 0; i < current.Count && attempts < 50; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var trial = current.Where((_, index) => index != i).ToList();
                    attempts++;
                    if (await ReproducesAsync(exe, trial, workspace, temp, finding, Math.Clamp(command.TimeoutSeconds, 5, 120), ct))
                    {
                        current = trial;
                        changed = true;
                        break;
                    }
                }
            }
            var status = current.Count < original.Count ? QaMinimizationStatus.Succeeded : QaMinimizationStatus.NoReduction;
            return await StoreAsync(status, original, current, $"Delta-debugging evaluated {attempts} argument subset(s) in the QA sandbox; target fingerprint {(status == QaMinimizationStatus.Succeeded ? "remained reproducible after reduction" : "required the original argument set")}.", command, finding, ct);
        }
        catch (OperationCanceledException) { return await StoreAsync(QaMinimizationStatus.Cancelled, original, current, "Minimization was cancelled; no source file was modified.", command, finding, CancellationToken.None); }
        catch (Exception ex) { return await StoreAsync(QaMinimizationStatus.InfrastructureFailure, original, current, $"Minimizer infrastructure failure: {ex.Message}", command, finding, CancellationToken.None); }
        finally { try { Directory.Delete(temp, true); } catch {
            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
        } }
    }
    private async Task<bool> ReproducesAsync(string exe, IReadOnlyList<string> args, string workspace, string temp, QaFinding target, int timeout, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var result = await executor.RunQaAsync(exe, args, workspace, null, TimeSpan.FromSeconds(timeout), Path.Combine(temp, id + ".out.log"), Path.Combine(temp, id + ".err.log"), ct);
        var candidates = normalizer.NormalizeText(target.Language, "bug-minimizer", result.StandardOutput, result.StandardError, result.TimedOut);
        return candidates.Any(x => string.Equals(QaFingerprint.Create(target.Language, x), target.Fingerprint, StringComparison.OrdinalIgnoreCase));
    }
    private async Task<BugMinimizationDto> StoreAsync(QaMinimizationStatus status, IReadOnlyList<string> original, IReadOnlyList<string> minimized, string detail, BugMinimizationCommand command, QaFinding finding, CancellationToken ct)
    {
        var entity = new QaBugMinimizationRecord(Guid.NewGuid(), command.ProjectId, finding.Id, JsonSerializer.Serialize(original), JsonSerializer.Serialize(minimized), original.Count, minimized.Count, status, detail, clock.UtcNow);
        await governance.AddMinimizationAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return new(entity.Id, status.ToString(), entity.OriginalUnits, entity.MinimizedUnits, minimized, detail);
    }
    private static string ResolveExecutable(string workspace, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Executable is required.");
        if (Path.IsPathRooted(value)) throw new InvalidOperationException("Minimizer executable must be a workspace-relative runner or a trusted PATH tool name.");
        if (value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar))
        {
            var full = Path.GetFullPath(Path.Combine(workspace, value));
            var prefix = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) throw new InvalidOperationException("Minimizer executable escapes the workspace or is missing.");
            return full;
        }
        return ProcessExecutor.FindExecutable(value) ?? throw new FileNotFoundException($"Executable '{value}' was not found.");
    }
}
