// Purpose: Parses and validates structured generative-QA output without trusting model-supplied evidence or patch paths.
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.P2Intelligence;

public sealed class GenerativeQaResponseParser(IOptions<GenerativeQaOptions> configuredOptions)
{
    private readonly GenerativeQaOptions _options = configuredOptions.Value;

    public DebugHypothesisResponse? ParseHypotheses(
        string content,
        IReadOnlySet<Guid> allowedEvidenceIds,
        string provider,
        string model,
        AiExecutionMetadata metadata)
    {
        using var document = ParseObject(content);
        if (!document.RootElement.TryGetProperty("hypotheses", out var array) || array.ValueKind != JsonValueKind.Array)
            return null;

        var hypotheses = new List<DebugHypothesis>();
        foreach (var item in array.EnumerateArray().Take(10))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = RequiredString(item, "id", 120);
            var title = RequiredString(item, "title", 300);
            var explanation = RequiredString(item, "explanation", 4000);
            if (id is null || title is null || explanation is null)
                continue;

            if (!item.TryGetProperty("confidence", out var confidenceNode) || !confidenceNode.TryGetDecimal(out var confidence))
                continue;
            if (confidence is < 0m or > 1m)
                continue;

            var evidenceIds = ParseEvidenceIds(item, allowedEvidenceIds);
            if (evidenceIds.Count == 0)
                continue;

            var experiments = new List<DebugExperimentKind>();
            if (item.TryGetProperty("suggestedExperiments", out var experimentsNode) && experimentsNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var experiment in experimentsNode.EnumerateArray())
                {
                    if (experiment.ValueKind == JsonValueKind.String
                        && Enum.TryParse<DebugExperimentKind>(experiment.GetString(), true, out var parsed)
                        && !experiments.Contains(parsed))
                        experiments.Add(parsed);
                }
            }
            if (experiments.Count == 0)
                experiments.Add(DebugExperimentKind.Reproduce);

            hypotheses.Add(new DebugHypothesis(id, title, explanation, confidence, evidenceIds, experiments));
        }

        return hypotheses.Count == 0
            ? null
            : new DebugHypothesisResponse(provider, model, hypotheses, metadata);
    }

    public FixProposalResponse? ParseFix(
        string content,
        IReadOnlySet<Guid> allowedEvidenceIds,
        string provider,
        string model,
        AiExecutionMetadata metadata)
    {
        using var document = ParseObject(content);
        var root = document.RootElement;
        var summary = RequiredString(root, "summary", 4000);
        var diff = RequiredString(root, "unifiedDiff", 2_000_000);
        if (summary is null || diff is null || !diff.StartsWith("diff --git ", StringComparison.Ordinal))
            return null;

        if (!PatchWithinBudget(diff))
            return null;

        var evidenceIds = ParseEvidenceIds(root, allowedEvidenceIds);
        if (evidenceIds.Count == 0)
            return null;

        if (!root.TryGetProperty("regressionTests", out var testsNode) || testsNode.ValueKind != JsonValueKind.Array)
            return null;

        var tests = new List<RegressionTestDraft>();
        foreach (var test in testsNode.EnumerateArray().Take(20))
        {
            var relativePath = RequiredString(test, "relativePath", 500);
            var body = RequiredString(test, "content", 256_000);
            var purpose = RequiredString(test, "purpose", 1000);
            if (relativePath is null || body is null || purpose is null || !IsSafeRelativePath(relativePath))
                continue;
            tests.Add(new RegressionTestDraft(relativePath.Replace('\\', '/'), body, purpose));
        }

        if (tests.Count == 0)
            return null;

        return new FixProposalResponse(provider, model, summary, diff, tests, evidenceIds, metadata);
    }

    private bool PatchWithinBudget(string diff)
    {
        var files = diff.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.StartsWith("diff --git ", StringComparison.Ordinal));
        if (files is < 1 || files > _options.MaxPatchFiles)
            return false;

        var changedLines = diff.Split(['\r', '\n'])
            .Count(line => (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal))
                           || (line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal)));
        return changedLines <= _options.MaxPatchChangedLines;
    }

    private static JsonDocument ParseObject(string content)
    {
        var normalized = StripCodeFence(content).Trim();
        var document = JsonDocument.Parse(normalized, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new JsonException("Generative QA output must be a JSON object.");
        }
        return document;
    }

    private static string StripCodeFence(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline >= 0 && lastFence > firstNewline
            ? trimmed[(firstNewline + 1)..lastFence]
            : trimmed;
    }

    private static string? RequiredString(JsonElement element, string property, int maxLength)
    {
        if (!element.TryGetProperty(property, out var node) || node.ValueKind != JsonValueKind.String)
            return null;
        var value = node.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) || value.Length > maxLength ? null : value;
    }

    private static IReadOnlyList<Guid> ParseEvidenceIds(JsonElement element, IReadOnlySet<Guid> allowed)
    {
        if (!element.TryGetProperty("evidenceIds", out var ids) || ids.ValueKind != JsonValueKind.Array)
            return Array.Empty<Guid>();
        var result = new List<Guid>();
        foreach (var node in ids.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.String || !Guid.TryParse(node.GetString(), out var id))
                return Array.Empty<Guid>();
            if (!allowed.Contains(id))
                return Array.Empty<Guid>();
            if (!result.Contains(id))
                result.Add(id);
        }
        return result;
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            return false;
        var normalized = path.Replace('\\', '/');
        return !normalized.StartsWith("../", StringComparison.Ordinal)
               && !normalized.Contains("/../", StringComparison.Ordinal)
               && !normalized.Equals("..", StringComparison.Ordinal)
               && !normalized.Contains('\0');
    }
}
