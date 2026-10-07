// Purpose: Evaluates performance-budget evidence only when it is bound to the exact release-candidate revision.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;

internal sealed class ReleaseReadinessPerformanceEvaluator(IPerformanceBudgetRepository budgets)
{
    public async Task EvaluateAsync(ReleaseReadinessEvaluationContext context, CancellationToken ct)
    {
        var evaluations = await budgets.ListEvaluationsAsync(context.Request.ProjectId, 100, ct);
        var candidate = evaluations
            .Where(x => IsBoundToCommit(x, context.Request.CommitSha))
            .ToArray();

        context.PerformanceEvaluationCount = candidate.Length;

        if (candidate.Length == 0)
        {
            // Unbound PASS evidence is never used to prove candidate quality. An
            // unbound failure remains conservatively blocking because DIP cannot prove
            // that it belongs to a different revision; this preserves fail-closed
            // behavior for legacy evaluations while preventing historical PASS reuse.
            if (evaluations.Any(x => x.Verdict == PerformanceBudgetVerdict.Failed))
            {
                context.Blockers.Add(
                    "A failed performance budget evaluation exists without exact-revision provenance, so DIP cannot prove that the failure is unrelated to this candidate.");
            }
            if (evaluations.Count > 0)
            {
                context.Actions.Add(
                    "Rerun benchmark/replay performance QA for the exact candidate revision and persist commit/build provenance with the evaluation.");
            }
            return;
        }

        if (candidate.Any(x => x.Verdict == PerformanceBudgetVerdict.Failed))
        {
            context.Blockers.Add("At least one exact-revision platform performance budget evaluation failed.");
            context.Actions.Add("Restore the failed platform performance budget and rerun the affected benchmark/replay on the same candidate revision.");
        }
        else if (candidate.Any(x => x.Verdict == PerformanceBudgetVerdict.Warning))
        {
            context.Warnings.Add("An exact-revision performance budget is near its configured boundary.");
        }
    }

    private static bool IsBoundToCommit(PerformanceBudgetEvaluation evaluation, string commitSha)
    {
        if (string.IsNullOrWhiteSpace(commitSha) || string.IsNullOrWhiteSpace(evaluation.DetailJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(evaluation.DetailJson);
            var root = document.RootElement;
            if (!ContainsCommit(root, commitSha.Trim()))
                return false;

            // Replay-backed budgets must also carry an explicit build identity. The
            // public release request currently identifies the candidate by commit SHA,
            // so this validates build provenance presence without guessing a BuildId.
            if (evaluation.ReplayRunId.HasValue && !ContainsNonEmptyString(root, "buildId"))
                return false;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }


    private static bool ContainsNonEmptyString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    return true;
                }

                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                    && ContainsNonEmptyString(property.Value, name))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (ContainsNonEmptyString(item, name))
                    return true;
        }

        return false;
    }

    private static bool ContainsCommit(JsonElement element, string commitSha)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var name in new[] { "commitSha", "sourceCommit", "targetCommit", "revision", "headSha", "dipSourceCommit" })
        {
            if (TryGetProperty(element, name, out var value)
                && value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString()?.Trim(), commitSha, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var container in new[] { "provenance", "source", "run", "replay", "benchmark", "environment" })
        {
            if (TryGetProperty(element, container, out var nested)
                && nested.ValueKind == JsonValueKind.Object
                && ContainsCommit(nested, commitSha))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
