// Purpose: Isolates regression revisions using bounded Git bisect execution.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

internal sealed class GitBisectRunner(ProcessExecutor executor, IQaWorkspaceResolver workspaceResolver)
    : ExperimentExecutionSupport(executor, workspaceResolver)
{
    public async Task<GitBisectResult> FindRegressionCommitAsync(GitBisectCommand command, CancellationToken ct)
        {
            var workspace = workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
            var git = ProcessExecutor.FindExecutable("git");
            var root = ArtifactRoot(command.Target.ProjectId, "regression-bisect");
            if (git is null)
                return new("InfrastructureFailure", null, null, 0, [], RelativeArtifact(root));
    
            var revision = await RunGitAsync(git, workspace, root, ["rev-list", "--ancestry-path", "--reverse", $"{command.GoodCommit}..{command.BadCommit}"], ct);
            if (revision.ExitCode != 0)
                return new("InfrastructureFailure", null, null, 0, [], RelativeArtifact(root));
            var commits = revision.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).ToList();
            if (commits.Count == 0)
                return new("Unknown", null, command.GoodCommit, 0, [], RelativeArtifact(root));
            if (!commits.Contains(command.BadCommit, StringComparer.OrdinalIgnoreCase))
                commits.Add(command.BadCommit);
    
            var history = new List<GitBisectEvaluation>();
            var low = -1;
            var high = commits.Count - 1;
            var evaluatedCount = 0;
            string? lastGood = command.GoodCommit;
            while (high - low > 1 && evaluatedCount < Math.Clamp(command.MaxEvaluations, 2, 100))
            {
                var middle = (low + high) / 2;
                var commit = commits[middle];
                var evaluated = await EvaluateCommitAsync(git, workspace, root, commit, command.Target, ct);
                if (evaluated.Result is null)
                {
                    history.Add(new(commit, "InfrastructureFailure", false, evaluated.DurationMs));
                    return new("InfrastructureFailure", null, lastGood, evaluatedCount + 1, history, RelativeArtifact(root));
                }
                var passed = evaluated.Result.Value;
                history.Add(new(commit, passed ? "Passed" : "Failed", passed, evaluated.DurationMs));
                evaluatedCount++;
                if (passed)
                {
                    low = middle;
                    lastGood = commit;
                }
                else
                {
                    high = middle;
                }
            }
            var firstBad = commits[Math.Clamp(high, 0, commits.Count - 1)];
            return new("Found", firstBad, lastGood, evaluatedCount, history, RelativeArtifact(root));
        }
}
