// Purpose: Coordinates R oo tC au se In te ll ig en ce Se rv ic e application behavior for Intelligence.
using System.Text.RegularExpressions;
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Intelligence;

public sealed class RootCauseIntelligenceService
{
    private readonly IEvidenceRepository _evidence;

    public RootCauseIntelligenceService(IEvidenceRepository evidence) => _evidence = evidence;

    public async Task<RootCauseResult> AnalyzeAsync(RootCauseRequest request, CancellationToken cancellationToken)
    {
        var corpus = string.Join('\n', new[]
        {
            request.FindingText,
            request.StackTrace,
            request.DiffText,
            string.Join('\n', request.LogLines)
        });
        if (string.IsNullOrWhiteSpace(corpus))
            return new(Array.Empty<RootCauseCandidate>(), "Insufficient Evidence");

        var candidates = new List<RootCauseCandidate>();
        void Add(string cause, decimal confidence, string marker, string action)
        {
            var hits = Regex.Matches(corpus, marker, RegexOptions.IgnoreCase).Count;
            if (hits > 0)
                candidates.Add(new(
                    cause,
                    Math.Min(95, confidence + hits * 3),
                    new[] { $"Matched evidence pattern: {marker}", $"Observed {hits} time(s) in supplied evidence." },
                    request.IncludeSuggestedAction ? action : null));
        }

        Add("Object lifetime / memory ownership defect", 72, "use-after-free|access violation|double free|heap corruption", "Review ownership and lifetime around the top application frame; reproduce under sanitizer.");
        Add("Out-of-bounds memory access", 75, "buffer overflow|out.?of.?range|IndexOutOfRange", "Validate bounds and container sizes around the first failing application frame.");
        Add("Null or missing reference", 70, "NullReference|NoneType|null pointer|missing reference", "Trace the value back to its producer and add a precondition at the boundary.");
        Add("Concurrency / wait-cycle defect", 65, "deadlock|timeout|hang|TaskCanceled|lock contention", "Capture thread stacks during the hang and inspect lock ordering / awaited dependencies.");
        Add("Resource or memory pressure", 68, "out of memory|OOM|memory growth|leak|allocation", "Compare allocation growth and target hardware memory budget; inspect retained resources.");

        var recent = await _evidence.ListForProjectAsync(request.ProjectId, DateTimeOffset.UtcNow.AddDays(-120), cancellationToken);
        var similar = recent.Where(x => x.SubjectKey == request.SubjectKey && x.Outcome == EvidenceOutcome.Failed).Take(5).ToArray();
        if (similar.Length > 0)
        {
            candidates.Add(new(
                "Recurring failure in the same subsystem",
                60 + Math.Min(25, similar.Length * 4),
                similar.Select(x => $"Prior evidence: {x.Title} ({x.ObservedAtUtc:u})").ToArray(),
                request.IncludeSuggestedAction ? "Compare the current fingerprint/stack with prior incidents before changing unrelated code." : null));
        }

        return new(
            candidates.OrderByDescending(x => x.ConfidencePercent).Take(5).ToArray(),
            candidates.Count > 0 ? "Evidence Grounded" : "Insufficient Evidence");
    }
}
