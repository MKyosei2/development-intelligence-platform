// Purpose: Implements Mutation Runner infrastructure behavior for this repository area.
using System.Text.Json;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.Intelligence;

public sealed class MutationRunner : IMutationRunner
{
    private readonly IQaWorkspaceResolver _workspace;
    private readonly IntelligenceExecutionSafety _safety;

    public MutationRunner(
        IQaWorkspaceResolver workspace,
        IntelligenceExecutionSafety safety)
    {
        _workspace = workspace;
        _safety = safety;
    }

    public async Task<MutationResult> RunAsync(
        MutationRequest request,
        string artifactRoot,
        CancellationToken cancellationToken)
    {
        var workspace = _workspace.Resolve(request.WorkspaceRelativePath);
        Directory.CreateDirectory(artifactRoot);

        await using var sandbox = await _safety.CreateDetachedWorktreeAsync(
            workspace,
            "HEAD",
            cancellationToken);

        var source = _safety.ResolveWorkspaceRelativePath(
            sandbox.Path,
            request.SourceRelativePath);
        if (!File.Exists(source))
            throw new InvalidOperationException("Mutation source is missing in the isolated worktree.");

        var testTimeout = TimeSpan.FromMinutes(5);
        var baseline = await _safety.RunAllowedCommandAsync(
            sandbox.Path,
            request.TestCommand,
            request.TestArguments,
            testTimeout,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(artifactRoot, "baseline-test.log"),
            baseline.StandardOutput + Environment.NewLine + baseline.StandardError,
            cancellationToken);

        if (baseline.TimedOut || baseline.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Mutation testing requires a passing baseline test command.");
        }

        var original = await File.ReadAllTextAsync(source, cancellationToken);
        var maxMutants = Math.Clamp(request.MaxMutants, 1, 50);
        var replacements = new (string Old, string New, string Name)[]
        {
            (">=", ">", "gte-to-gt"),
            ("<=", "<", "lte-to-lt"),
            ("==", "!=", "eq-to-ne"),
            ("!=", "==", "ne-to-eq"),
            ("true", "false", "true-to-false"),
            ("false", "true", "false-to-true"),
            (" + ", " - ", "plus-to-minus"),
            (" - ", " + ", "minus-to-plus")
        };

        var mutants = BuildMutants(original, replacements, maxMutants);
        var killed = 0;
        var survived = new List<string>();
        var records = new List<object>();

        try
        {
            for (var index = 0; index < mutants.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var mutant = mutants[index];
                await File.WriteAllTextAsync(source, mutant.Text, cancellationToken);

                var result = await _safety.RunAllowedCommandAsync(
                    sandbox.Path,
                    request.TestCommand,
                    request.TestArguments,
                    testTimeout,
                    cancellationToken);

                var mutantKilled = result.TimedOut || result.ExitCode != 0;
                if (mutantKilled)
                    killed++;
                else
                    survived.Add(mutant.Name);

                var logPath = Path.Combine(artifactRoot, $"mutant-{index + 1:000}-{mutant.Name}.log");
                await File.WriteAllTextAsync(
                    logPath,
                    result.StandardOutput + Environment.NewLine + result.StandardError,
                    cancellationToken);

                records.Add(new
                {
                    mutant = mutant.Name,
                    mutant.Offset,
                    mutantKilled,
                    result.ExitCode,
                    result.TimedOut,
                    logPath
                });

                await File.WriteAllTextAsync(source, original, cancellationToken);
            }
        }
        finally
        {
            try
            {
                await File.WriteAllTextAsync(source, original, CancellationToken.None);
            }
            catch
            {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }

        var score = mutants.Count == 0
            ? 100m
            : decimal.Round(100m * killed / mutants.Count, 2);

        var json = JsonSerializer.Serialize(
            new
            {
                mutants = mutants.Count,
                killed,
                survived = survived.Count,
                mutationScore = score,
                survivedMutants = survived,
                source = request.SourceRelativePath,
                isolatedWorktree = true,
                originalCheckoutModified = false,
                baselinePassed = true,
                records
            },
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(
            Path.Combine(artifactRoot, "mutation.json"),
            json,
            cancellationToken);

        return new MutationResult(
            mutants.Count,
            killed,
            survived.Count,
            score,
            survived,
            json);
    }

    private static List<Mutant> BuildMutants(
        string original,
        IReadOnlyList<(string Old, string New, string Name)> replacements,
        int maxMutants)
    {
        var mutants = new List<Mutant>();

        foreach (var replacement in replacements)
        {
            var searchFrom = 0;
            while (searchFrom < original.Length && mutants.Count < maxMutants)
            {
                var offset = original.IndexOf(
                    replacement.Old,
                    searchFrom,
                    StringComparison.Ordinal);
                if (offset < 0) break;

                var text = original[..offset]
                    + replacement.New
                    + original[(offset + replacement.Old.Length)..];
                var name = $"{replacement.Name}-{offset}";
                mutants.Add(new Mutant(text, name, offset));
                searchFrom = offset + replacement.Old.Length;
            }

            if (mutants.Count >= maxMutants)
                break;
        }

        return mutants;
    }

    private sealed record Mutant(string Text, string Name, int Offset);
}
