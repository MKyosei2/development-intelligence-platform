using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

/// <summary>
/// Bounded hypothesis -> candidate -> isolated verification loop.
/// The source workspace is never edited by this service. A mechanically verified
/// candidate is returned as ReadyForHumanReview so a human remains responsible
/// for applying and merging the change. Session snapshots and attempts are
/// persisted in the existing Development Graph instead of creating parallel
/// persistence tables.
/// </summary>
public sealed class AutonomousDebugService(
    IAutonomousDebugAgent hypothesisAgent,
    IFixProposalAgent fixAgent,
    IVerifiedFixService verifier,
    DevelopmentGraphService graph,
    ISystemClock clock)
{
    private static readonly ActivitySource ActivitySource = new("DIP.DevelopmentIntelligence");
    private static readonly Meter Meter = new("DIP.DevelopmentIntelligence");
    private static readonly Counter<long> SessionCounter = Meter.CreateCounter<long>("dip.autonomous_debug.sessions");
    private static readonly Counter<long> AiCallCounter = Meter.CreateCounter<long>("dip.autonomous_debug.ai_calls");
    private static readonly Counter<long> ExperimentCounter = Meter.CreateCounter<long>("dip.autonomous_debug.experiments");
    private static readonly Counter<long> VerifiedCandidateCounter = Meter.CreateCounter<long>("dip.autonomous_debug.verified_candidates");
    private static readonly Counter<long> TerminalStatusCounter = Meter.CreateCounter<long>("dip.autonomous_debug.terminal_status");
    private static readonly Histogram<double> DurationHistogram = Meter.CreateHistogram<double>("dip.autonomous_debug.duration_ms", "ms");

    public async Task<AutonomousDebugSessionDto> RunAsync(
        AutonomousDebugRequest request,
        CancellationToken ct)
    {
        Validate(request);
        var budget = (request.Budget ?? new AutonomousDebugBudget()).Validate();
        var started = clock.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var sessionId = Guid.NewGuid();
        var attempts = new List<AutonomousDebugAttemptDto>();
        var hypotheses = Array.Empty<DebugHypothesis>();
        var terminalStatus = "Failed";
        var aiCalls = 0;
        var experimentNumber = 0;

        using var activity = ActivitySource.StartActivity("autonomous-debug.run", ActivityKind.Internal);
        activity?.SetTag("dip.project.id", request.ProjectId.ToString("N"));
        activity?.SetTag("dip.debug.session_id", sessionId.ToString("N"));
        activity?.SetTag("dip.debug.domain", request.DomainId);
        activity?.SetTag("dip.debug.max_hypotheses", budget.MaxHypotheses);
        activity?.SetTag("dip.debug.max_experiments", budget.MaxExperiments);
        activity?.SetTag("dip.debug.max_ai_calls", budget.MaxAiCalls);

        SessionCounter.Add(
            1,
            new KeyValuePair<string, object?>("domain", request.DomainId));


        var running = new AutonomousDebugSessionDto(
            sessionId,
            request.ProjectId,
            request.SubjectKey,
            request.DomainId,
            "Running",
            "Collecting evidence-grounded root-cause hypotheses.",
            hypotheses,
            attempts,
            started,
            null);

        var sessionNode = await PersistSessionAsync(running, request, ct);

        try
        {
            var generated = await hypothesisAgent.GenerateHypothesesAsync(
                new DebugHypothesisRequest(
                    request.ProjectId,
                    request.SubjectKey,
                    request.Question,
                    request.EvidenceJson,
                    request.RepositoryContext,
                    budget.MaxHypotheses),
                ct);
            aiCalls++;
            AiCallCounter.Add(1, new KeyValuePair<string, object?>("stage", "hypothesis"));

            if (generated is null || generated.Hypotheses.Count == 0)
            {
                var empty = running with
                {
                    Status = "NoGroundedHypothesis",
                    Summary = "The configured AI provider did not return a hypothesis grounded in supplied Evidence IDs.",
                    CompletedAtUtc = clock.UtcNow
                };
                terminalStatus = empty.Status;
                await PersistSessionAsync(empty, request, ct);
                return empty;
            }

            hypotheses = generated.Hypotheses
                .OrderByDescending(x => x.Confidence)
                .Take(budget.MaxHypotheses)
                .ToArray();

            activity?.SetTag("dip.debug.hypothesis_count", hypotheses.Length);

            var hypothesisNodes = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var hypothesis in hypotheses)
            {
                var node = await graph.UpsertUniversalNodeAsync(
                    new UpsertUniversalGraphNodeCommand(
                        request.ProjectId,
                        request.DomainId,
                        "dip.debug.hypothesis",
                        $"debug-hypothesis:{sessionId:N}:{hypothesis.Id}",
                        hypothesis.Title,
                        "Proposed",
                        "DIP-AI",
                        null,
                        hypothesis.Id,
                        "1",
                        DevelopmentGraphNodeType.ExternalObservation,
                        JsonSerializer.Serialize(new
                        {
                            sessionId,
                            hypothesis.Id,
                            hypothesis.Title,
                            hypothesis.Explanation,
                            hypothesis.Confidence,
                            hypothesis.EvidenceIds,
                            hypothesis.SuggestedExperiments
                        })),
                    ct);

                hypothesisNodes[hypothesis.Id] = node.Node.Id;
                await graph.LinkUniversalAsync(
                    new LinkUniversalGraphNodesCommand(
                        request.ProjectId,
                        sessionNode.Node.Id,
                        node.Node.Id,
                        "contains",
                        100m,
                        false,
                        DevelopmentGraphEdgeType.Contains,
                        "{}"),
                    ct);
            }

            var budgetReason = string.Empty;
            var verifiedAny = false;

            foreach (var hypothesis in hypotheses)
            {
                ct.ThrowIfCancellationRequested();

                if (experimentNumber >= budget.MaxExperiments)
                {
                    budgetReason = "Maximum experiment count reached.";
                    break;
                }

                if (aiCalls >= budget.MaxAiCalls)
                {
                    budgetReason = "Maximum AI call count reached.";
                    break;
                }

                if (clock.UtcNow - started > TimeSpan.FromMinutes(budget.MaxWallClockMinutes))
                {
                    budgetReason = "Maximum wall-clock budget reached.";
                    break;
                }

                experimentNumber++;
                ExperimentCounter.Add(
                    1,
                    new KeyValuePair<string, object?>("domain", request.DomainId));

                var prompt = $"""
Evaluate this root-cause hypothesis and propose the smallest safe fix candidate that would falsify or support it through regression testing and existing QA.
Hypothesis ID: {hypothesis.Id}
Title: {hypothesis.Title}
Explanation: {hypothesis.Explanation}
Suggested experiments: {string.Join(", ", hypothesis.SuggestedExperiments)}
The candidate will be applied only to a disposable worktree and mechanically verified. Include at least one regression test. Do not change source merely to silence a diagnostic or test.
Original question: {request.Question}
""";

                var fix = await fixAgent.ProposeAsync(
                    new FixProposalRequest(
                        request.ProjectId,
                        request.SubjectKey,
                        prompt,
                        request.EvidenceJson,
                        request.RepositoryContext),
                    ct);
                aiCalls++;
                AiCallCounter.Add(1, new KeyValuePair<string, object?>("stage", "fix-proposal"));

                if (fix is null)
                {
                    var noProposal = new AutonomousDebugAttemptDto(
                        hypothesis.Id,
                        hypothesis.Title,
                        hypothesis.Confidence,
                        "NoGroundedProposal",
                        false,
                        "No evidence-grounded patch candidate was returned for this hypothesis.",
                        null,
                        hypothesis.EvidenceIds);
                    attempts.Add(noProposal);
                    await PersistAttemptAsync(
                        request,
                        sessionId,
                        experimentNumber,
                        hypothesis,
                        noProposal,
                        hypothesisNodes[hypothesis.Id],
                        ct);
                    continue;
                }

                var verification = await verifier.VerifyAsync(
                    new VerifiedFixRequest(
                        request.ProjectId,
                        request.WorkspaceRelativePath,
                        request.BaseCommit,
                        fix.UnifiedDiff,
                        fix.RegressionTests,
                        request.Language,
                        budget.TimeoutSecondsPerExperiment),
                    ct);

                var status = verification.Verified
                    ? "VerifiedCandidate"
                    : "RejectedCandidate";

                var attempt = new AutonomousDebugAttemptDto(
                    hypothesis.Id,
                    hypothesis.Title,
                    hypothesis.Confidence,
                    status,
                    verification.Verified,
                    verification.Summary,
                    verification.ArtifactRoot,
                    fix.EvidenceIds);
                attempts.Add(attempt);

                await PersistAttemptAsync(
                    request,
                    sessionId,
                    experimentNumber,
                    hypothesis,
                    attempt,
                    hypothesisNodes[hypothesis.Id],
                    ct);

                if (verification.Verified)
                {
                    verifiedAny = true;
                    VerifiedCandidateCounter.Add(
                        1,
                        new KeyValuePair<string, object?>("domain", request.DomainId));
                    break;
                }
            }

            var finalStatus = verifiedAny
                ? "ReadyForHumanReview"
                : string.IsNullOrWhiteSpace(budgetReason)
                    ? "NotVerified"
                    : "BudgetExhausted";

            var summary = verifiedAny
                ? "A candidate passed isolated patch application, generated regression tests and all available QA checks. The source workspace was not modified; human review is still required."
                : string.IsNullOrWhiteSpace(budgetReason)
                    ? "No candidate passed the isolated verification boundary."
                    : $"No verified candidate was found before the bounded search stopped: {budgetReason}";

            var completed = new AutonomousDebugSessionDto(
                sessionId,
                request.ProjectId,
                request.SubjectKey,
                request.DomainId,
                finalStatus,
                summary,
                hypotheses,
                attempts,
                started,
                clock.UtcNow);

            terminalStatus = finalStatus;
            await PersistSessionAsync(completed, request, ct);
            return completed;
        }
        catch (OperationCanceledException)
        {
            terminalStatus = "Cancelled";
            var cancelled = new AutonomousDebugSessionDto(
                sessionId,
                request.ProjectId,
                request.SubjectKey,
                request.DomainId,
                "Cancelled",
                "The autonomous debugging request was cancelled. No source workspace changes were made.",
                hypotheses,
                attempts,
                started,
                clock.UtcNow);

            try
            {
                await PersistSessionAsync(cancelled, request, CancellationToken.None);
            }
            catch
            {
                // Best effort only; cancellation must still propagate.
            }
            throw;
        }
        catch (Exception ex)
        {
            terminalStatus = "Failed";
            var failed = new AutonomousDebugSessionDto(
                sessionId,
                request.ProjectId,
                request.SubjectKey,
                request.DomainId,
                "Failed",
                $"Autonomous debugging stopped before verification completed: {ex.GetType().Name}.",
                hypotheses,
                attempts,
                started,
                clock.UtcNow);

            try
            {
                await PersistSessionAsync(failed, request, CancellationToken.None);
            }
            catch
            {
                // Best effort only; preserve the original failure.
            }

            throw;
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("dip.debug.status", terminalStatus);
            activity?.SetTag("dip.debug.ai_calls", aiCalls);
            activity?.SetTag("dip.debug.experiments", experimentNumber);

            TerminalStatusCounter.Add(
                1,
                new KeyValuePair<string, object?>("status", terminalStatus));
            DurationHistogram.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("status", terminalStatus));

        }
    }

    public async Task<AutonomousDebugSessionDto?> GetAsync(
        Guid projectId,
        Guid sessionId,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty || sessionId == Guid.Empty)
            return null;

        var node = await graph.FindNodeAsync(
            projectId,
            DevelopmentGraphNodeType.ExternalObservation,
            $"debug-session:{sessionId:N}",
            ct);
        if (node is null)
            return null;

        try
        {
            using var document = JsonDocument.Parse(node.MetadataJson);
            if (document.RootElement.TryGetProperty("snapshot", out var snapshot))
                return snapshot.Deserialize<AutonomousDebugSessionDto>();
        }
        catch (JsonException)
        {
            // Corrupt or incompatible graph metadata is treated as unavailable.
        }

        return null;
    }

    private async Task<UniversalGraphNodeDto> PersistSessionAsync(
        AutonomousDebugSessionDto session,
        AutonomousDebugRequest request,
        CancellationToken ct)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            snapshot = session,
            baseCommit = request.BaseCommit,
            workspace = request.WorkspaceRelativePath,
            budget = request.Budget ?? new AutonomousDebugBudget(),
            safety = new
            {
                sourceWorkspaceModified = false,
                automaticMerge = false,
                arbitraryShellFromAi = false
            }
        });

        return await graph.UpsertUniversalNodeAsync(
            new UpsertUniversalGraphNodeCommand(
                request.ProjectId,
                request.DomainId,
                "dip.debug.session",
                $"debug-session:{session.SessionId:N}",
                $"Autonomous debug: {request.SubjectKey}",
                session.Status,
                "DIP",
                null,
                session.SessionId.ToString("N"),
                "1",
                DevelopmentGraphNodeType.ExternalObservation,
                metadata),
            ct);
    }

    private async Task PersistAttemptAsync(
        AutonomousDebugRequest request,
        Guid sessionId,
        int number,
        DebugHypothesis hypothesis,
        AutonomousDebugAttemptDto attempt,
        Guid hypothesisNodeId,
        CancellationToken ct)
    {
        var node = await graph.UpsertUniversalNodeAsync(
            new UpsertUniversalGraphNodeCommand(
                request.ProjectId,
                request.DomainId,
                "dip.debug.experiment",
                $"debug-experiment:{sessionId:N}:{number}",
                $"Experiment {number}: {hypothesis.Title}",
                attempt.Status,
                "DIP",
                null,
                $"{sessionId:N}:{number}",
                "1",
                DevelopmentGraphNodeType.ExternalObservation,
                JsonSerializer.Serialize(new
                {
                    sessionId,
                    number,
                    hypothesisId = hypothesis.Id,
                    attempt.Status,
                    attempt.Verified,
                    attempt.Summary,
                    attempt.EvidenceIds,
                    suggested = hypothesis.SuggestedExperiments,
                    executed = "VerifyPatch+RegressionTest+AvailableQA"
                })),
            ct);

        await graph.LinkUniversalAsync(
            new LinkUniversalGraphNodesCommand(
                request.ProjectId,
                hypothesisNodeId,
                node.Node.Id,
                "tested-by",
                100m,
                false,
                DevelopmentGraphEdgeType.TestedBy,
                "{}"),
            ct);
    }

    private static void Validate(AutonomousDebugRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SubjectKey))
            throw new ArgumentException("SubjectKey is required.", nameof(request));
        if (request.SubjectKey.Length > 200)
            throw new ArgumentException("SubjectKey must be at most 200 characters.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new ArgumentException("Question is required.", nameof(request));
        if (request.Question.Length > 16000)
            throw new ArgumentException("Question is too large.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.EvidenceJson))
            throw new ArgumentException("EvidenceJson is required.", nameof(request));
        if (request.EvidenceJson.Length > 262144)
            throw new ArgumentException("EvidenceJson exceeds the 256 KiB autonomous-debug input limit.", nameof(request));
        if (request.RepositoryContext?.Length > 262144)
            throw new ArgumentException("RepositoryContext exceeds the 256 KiB autonomous-debug input limit.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.WorkspaceRelativePath))
            throw new ArgumentException("WorkspaceRelativePath is required.", nameof(request));
        if (request.WorkspaceRelativePath.Length > 2000)
            throw new ArgumentException("WorkspaceRelativePath is too long.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DomainId))
            throw new ArgumentException("DomainId is required.", nameof(request));
        if (request.DomainId.Length > 120)
            throw new ArgumentException("DomainId is too long.", nameof(request));
    }
}
