// Purpose: Provides infrastructure behavior for this application boundary.
using System.Text.Json;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.Intelligence;

public sealed class RegressionRunner : IRegressionRunner
{
    private readonly IQaWorkspaceResolver _workspace;
    private readonly IntelligenceExecutionSafety _safety;

    public RegressionRunner(
        IQaWorkspaceResolver workspace,
        IntelligenceExecutionSafety safety)
    {
        _workspace = workspace;
        _safety = safety;
    }

    public async Task<RegressionResult> FindOriginAsync(
        RegressionRequest request,
        string artifactRoot,
        CancellationToken cancellationToken)
    {
        var workspace = _workspace.Resolve(request.WorkspaceRelativePath);
        Directory.CreateDirectory(artifactRoot);

        var ancestry = await _safety.RunGitAsync(
            workspace,
            new[] { "merge-base", "--is-ancestor", request.GoodCommit, request.BadCommit },
            TimeSpan.FromSeconds(30),
            cancellationToken);
        if (ancestry.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "GoodCommit must be an ancestor of BadCommit for bounded regression search.");
        }

        var list = await _safety.RunGitAsync(
            workspace,
            new[] { "rev-list", "--ancestry-path", "--reverse", $"{request.GoodCommit}..{request.BadCommit}" },
            TimeSpan.FromSeconds(30),
            cancellationToken);
        if (list.ExitCode != 0)
            throw new InvalidOperationException(list.StandardError.Trim());

        var commits = list.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (commits.Length == 0)
        {
            var noRange = JsonSerializer.Serialize(new
            {
                status = "NoRange",
                tested = Array.Empty<string>(),
                isolatedWorktree = true
            });
            await File.WriteAllTextAsync(
                Path.Combine(artifactRoot, "regression-origin.json"),
                noRange,
                cancellationToken);
            return new RegressionResult(null, Array.Empty<string>(), "NoRange", noRange);
        }

        await using var sandbox = await _safety.CreateDetachedWorktreeAsync(
            workspace,
            request.BadCommit,
            cancellationToken);

        var tested = new List<string>();
        var testRecords = new List<object>();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 5, 1800));

        async Task<bool> VerifyAsync(string commit)
        {
            var checkout = await _safety.RunGitAsync(
                sandbox.Path,
                new[] { "checkout", "--detach", "--force", commit },
                TimeSpan.FromMinutes(2),
                cancellationToken);
            if (checkout.ExitCode != 0)
                throw new InvalidOperationException(checkout.StandardError.Trim());

            tested.Add(commit);
            var result = await _safety.RunAllowedCommandAsync(
                sandbox.Path,
                request.VerificationCommand,
                request.VerificationArguments,
                timeout,
                cancellationToken);

            var shortSha = commit.Length > 12 ? commit[..12] : commit;
            var logPath = Path.Combine(artifactRoot, $"verify-{tested.Count:000}-{shortSha}.log");
            await File.WriteAllTextAsync(
                logPath,
                result.StandardOutput + Environment.NewLine + result.StandardError,
                cancellationToken);

            testRecords.Add(new
            {
                commit,
                result.ExitCode,
                result.TimedOut,
                logPath
            });

            return !result.TimedOut && result.ExitCode == 0;
        }

        // Validate the declared endpoints before binary search. A bad baseline
        // makes any first-bad result meaningless.
        if (!await VerifyAsync(request.GoodCommit))
            throw new InvalidOperationException("GoodCommit did not pass the verification command.");
        if (await VerifyAsync(request.BadCommit))
        {
            var unresolvedPayload = JsonSerializer.Serialize(
                new
                {
                    status = "BadCommitPassed",
                    firstBadCommit = (string?)null,
                    tested,
                    isolatedWorktree = true,
                    originalCheckoutModified = false,
                    testRecords
                },
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(
                Path.Combine(artifactRoot, "regression-origin.json"),
                unresolvedPayload,
                cancellationToken);
            return new RegressionResult(null, tested, "BadCommitPassed", unresolvedPayload);
        }

        var low = 0;
        var high = commits.Length - 1;
        string? firstBad = request.BadCommit;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var commit = commits[mid];
            var passed = await VerifyAsync(commit);
            if (passed)
            {
                low = mid + 1;
            }
            else
            {
                firstBad = commit;
                high = mid - 1;
            }
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                status = firstBad is null ? "Unresolved" : "Resolved",
                firstBadCommit = firstBad,
                tested,
                isolatedWorktree = true,
                originalCheckoutModified = false,
                verificationTool = request.VerificationCommand,
                testRecords
            },
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(
            Path.Combine(artifactRoot, "regression-origin.json"),
            payload,
            cancellationToken);

        return new RegressionResult(
            firstBad,
            tested,
            firstBad is null ? "Unresolved" : "Resolved",
            payload);
    }
}
