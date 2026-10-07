// Purpose: Generates evidence-grounded hypotheses, validates experiment applicability/regression tests, and verifies candidates with the same required plan after patching.
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

/// <summary>
/// Bounded hypothesis -> executable experiment plan -> baseline -> candidate ->
/// isolated post-patch verification loop. AI reasoning is never accepted as proof:
/// only deterministic runner evidence can move a candidate to ReadyForHumanReview.
/// </summary>
public sealed class AutonomousDebugService(
    IAutonomousDebugAgent hypothesisAgent,
    IFixProposalAgent fixAgent,
    IVerifiedFixService verifier,
    DevelopmentGraphService graph,
    ISystemClock clock,
    IAutonomousQaExperimentDispatcher? experimentDispatcher = null)
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
        var sessionId = request.RequestedSessionId ?? Guid.NewGuid();
        var attempts = new List<AutonomousDebugAttemptDto>();
        var hypotheses = Array.Empty<DebugHypothesis>();
        var terminalStatus = "Failed";
        var aiCalls = 0;
        var hypothesisAttempts = 0;

        using var activity = ActivitySource.StartActivity("autonomous-debug.run", ActivityKind.Internal);
        activity?.SetTag("dip.project.id", request.ProjectId.ToString("N"));
        activity?.SetTag("dip.debug.session_id", sessionId.ToString("N"));
        activity?.SetTag("dip.debug.domain", request.DomainId);
        activity?.SetTag("dip.debug.max_hypotheses", budget.MaxHypotheses);
        activity?.SetTag("dip.debug.max_experiments", budget.MaxExperiments);
        activity?.SetTag("dip.debug.max_ai_calls", budget.MaxAiCalls);

        SessionCounter.Add(1, new KeyValuePair<string, object?>("domain", request.DomainId));

        var running = new AutonomousDebugSessionDto(
            sessionId,
            request.ProjectId,
            request.SubjectKey,
            request.DomainId,
            "Running",
            "Collecting evidence-grounded root-cause hypotheses and executable experiment plans.",
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
                        "2",
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

                if (hypothesisAttempts >= budget.MaxExperiments)
                {
                    budgetReason = "Maximum hypothesis experiment count reached.";
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

                hypothesisAttempts++;
                ExperimentCounter.Add(1, new KeyValuePair<string, object?>("domain", request.DomainId));

                var plan = experimentDispatcher is null
                    ? null
                    : BuildExperimentPlan(sessionId, request, hypothesis, budget);
                IReadOnlyList<AutonomousQaExperimentResult> baseline = Array.Empty<AutonomousQaExperimentResult>();

                if (plan is not null && experimentDispatcher is not null)
                {
                    baseline = await experimentDispatcher.ExecutePlanAsync(
                        plan,
                        new AutonomousQaExperimentExecutionContext(
                            request.ProjectId,
                            request.SubjectKey,
                            request.WorkspaceRelativePath,
                            request.BaseCommit,
                            request.Language,
                            AutonomousQaExperimentPhase.Baseline,
                            request.ExperimentConfigurationJson),
                        ct);

                    if (HasUnverifiableRequiredStep(plan, baseline))
                    {
                        var blocked = new AutonomousDebugAttemptDto(
                            hypothesis.Id,
                            hypothesis.Title,
                            hypothesis.Confidence,
                            "ExperimentPreconditionFailed",
                            false,
                            "One or more required baseline experiments were unavailable, unconfigured, skipped, or failed at the infrastructure boundary. No patch candidate was generated.",
                            null,
                            hypothesis.EvidenceIds,
                            plan,
                            baseline,
                            null);
                        attempts.Add(blocked);
                        await PersistAttemptAsync(
                            request,
                            sessionId,
                            hypothesisAttempts,
                            hypothesis,
                            blocked,
                            hypothesisNodes[hypothesis.Id],
                            ct);
                        continue;
                    }

                    if (RequiredStepsAllPassed(plan, baseline))
                    {
                        var notReproduced = new AutonomousDebugAttemptDto(
                            hypothesis.Id,
                            hypothesis.Title,
                            hypothesis.Confidence,
                            "HypothesisNotReproduced",
                            false,
                            "All required baseline experiments passed; the proposed defect hypothesis was not mechanically reproduced, so DIP did not ask AI to patch the source.",
                            null,
                            hypothesis.EvidenceIds,
                            plan,
                            baseline,
                            null);
                        attempts.Add(notReproduced);
                        await PersistAttemptAsync(
                            request,
                            sessionId,
                            hypothesisAttempts,
                            hypothesis,
                            notReproduced,
                            hypothesisNodes[hypothesis.Id],
                            ct);
                        continue;
                    }
                }

                var prompt = $"""
Evaluate this root-cause hypothesis and propose the smallest safe fix candidate.
Hypothesis ID: {hypothesis.Id}
Title: {hypothesis.Title}
Explanation: {hypothesis.Explanation}
Suggested experiments: {string.Join(", ", hypothesis.SuggestedExperiments)}
Baseline experiment evidence:
{FormatExperimentResults(baseline)}
The candidate will be applied only to a disposable worktree. DIP will rerun the same required experiment plan after patching. Include at least one focused regression test in a new test file; the verifier must observe that same test fail before the patch and pass after it. Do not change source merely to silence a diagnostic or test. Do not claim success based on this proposal; executable verification is authoritative.
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
                        hypothesis.EvidenceIds,
                        plan,
                        baseline,
                        null);
                    attempts.Add(noProposal);
                    await PersistAttemptAsync(
                        request,
                        sessionId,
                        hypothesisAttempts,
                        hypothesis,
                        noProposal,
                        hypothesisNodes[hypothesis.Id],
                        ct);
                    continue;
                }

                var regressionValidation = ValidateRegressionTests(fix.RegressionTests, fix.UnifiedDiff);
                if (regressionValidation is not null)
                {
                    var rejected = new AutonomousDebugAttemptDto(
                        hypothesis.Id,
                        hypothesis.Title,
                        hypothesis.Confidence,
                        "RejectedCandidate",
                        false,
                        "The AI proposal did not satisfy the regression-test contract: " + regressionValidation,
                        null,
                        fix.EvidenceIds,
                        plan,
                        baseline,
                        null);
                    attempts.Add(rejected);
                    await PersistAttemptAsync(
                        request,
                        sessionId,
                        hypothesisAttempts,
                        hypothesis,
                        rejected,
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
                        budget.TimeoutSecondsPerExperiment,
                        plan,
                        baseline,
                        request.ExperimentConfigurationJson,
                        request.SubjectKey),
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
                    fix.EvidenceIds,
                    plan,
                    baseline,
                    verification.ExperimentResults);
                attempts.Add(attempt);

                await PersistAttemptAsync(
                    request,
                    sessionId,
                    hypothesisAttempts,
                    hypothesis,
                    attempt,
                    hypothesisNodes[hypothesis.Id],
                    ct);

                if (verification.Verified)
                {
                    verifiedAny = true;
                    VerifiedCandidateCounter.Add(1, new KeyValuePair<string, object?>("domain", request.DomainId));
                    break;
                }
            }

            var finalStatus = verifiedAny
                ? "ReadyForHumanReview"
                : string.IsNullOrWhiteSpace(budgetReason)
                    ? "NotVerified"
                    : "BudgetExhausted";

            var summary = verifiedAny
                ? "A candidate passed Red->Green regression-test proof, isolated patch application, adapter QA, and every required post-patch experiment. The source workspace was not modified; human review is still required."
                : string.IsNullOrWhiteSpace(budgetReason)
                    ? "No candidate satisfied the complete executable verification boundary."
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
            activity?.SetTag("dip.debug.experiments", hypothesisAttempts);
            TerminalStatusCounter.Add(1, new KeyValuePair<string, object?>("status", terminalStatus));
            DurationHistogram.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("status", terminalStatus));
        }
    }

    public async Task<IReadOnlyList<AutonomousDebugSessionDto>> ListAsync(
        Guid projectId,
        int take,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty)
            return Array.Empty<AutonomousDebugSessionDto>();

        var graphSnapshot = await graph.GetUniversalAsync(projectId, null, "dip.debug.session", ct);
        var sessions = new List<AutonomousDebugSessionDto>();
        foreach (var node in graphSnapshot.Nodes)
        {
            try
            {
                using var document = JsonDocument.Parse(node.Node.MetadataJson);
                if (!document.RootElement.TryGetProperty("snapshot", out var snapshot))
                    continue;
                var session = snapshot.Deserialize<AutonomousDebugSessionDto>();
                if (session is not null && session.ProjectId == projectId)
                    sessions.Add(session);
            }
            catch (JsonException)
            {
                // One corrupt legacy snapshot must not make the whole history unavailable.
            }
        }
        return sessions
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.SessionId)
            .Take(Math.Clamp(take, 1, 100))
            .ToArray();
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
            // Corrupt or incompatible legacy snapshots are treated as unavailable.
        }
        return null;
    }

    private static AutonomousQaExperimentPlan BuildExperimentPlan(
        Guid sessionId,
        AutonomousDebugRequest request,
        DebugHypothesis hypothesis,
        AutonomousDebugBudget budget)
    {
        var kinds = hypothesis.SuggestedExperiments
            .Where(x => x != DebugExperimentKind.VerifyPatch)
            .Distinct()
            .ToList();
        AugmentExperimentsFromHypothesis(hypothesis, kinds);

        kinds = kinds
            .Where(kind => IsExperimentApplicable(kind, request, hypothesis))
            .Distinct()
            .Take(12)
            .ToList();

        if (kinds.Count == 0)
            kinds.Add(DebugExperimentKind.Reproduce);
        if (!kinds.Contains(DebugExperimentKind.RegressionTest))
            kinds.Add(DebugExperimentKind.RegressionTest);

        var steps = kinds
            .Select((kind, index) => AutonomousQaExperimentDefaults.Create(
                $"{hypothesis.Id}:{index + 1}:{kind}",
                kind,
                $"Hypothesis '{hypothesis.Title}' requires {kind} evidence before a candidate can be accepted.",
                true,
                budget.TimeoutSecondsPerExperiment))
            .ToArray();

        return new(
            Guid.NewGuid(),
            hypothesis.Id,
            request.SubjectKey,
            request.BaseCommit,
            steps);
    }

    private static bool IsExperimentApplicable(
        DebugExperimentKind kind,
        AutonomousDebugRequest request,
        DebugHypothesis hypothesis)
    {
        if (kind is DebugExperimentKind.Compile
            or DebugExperimentKind.StaticAnalysis
            or DebugExperimentKind.UnitTest
            or DebugExperimentKind.RegressionTest
            or DebugExperimentKind.Reproduce
            or DebugExperimentKind.VerifyPatch)
        {
            return true;
        }

        if (HasExperimentConfiguration(request.ExperimentConfigurationJson, kind))
            return true;

        var text = $"{request.DomainId} {request.SubjectKey} {request.Question} {request.RepositoryContext} {hypothesis.Title} {hypothesis.Explanation}"
            .ToLowerInvariant();

        return kind switch
        {
            DebugExperimentKind.AssetValidation => Has(text, "asset", "maya", "unity", "unreal", "godot", "prefab", "mesh", "texture", "scene", "content"),
            DebugExperimentKind.ShaderValidation => Has(text, "shader", "hlsl", "glsl", "render", "material", "lighting"),
            DebugExperimentKind.VisualRegression or DebugExperimentKind.VideoRegression => Has(text, "visual", "render", "pixel", "ui", "camera", "scene", "framebuffer", "video"),
            DebugExperimentKind.AudioRegression => Has(text, "audio", "sound", "voice", "music", "bgm"),
            DebugExperimentKind.MemorySafety => request.Language.Value.Equals("cpp", StringComparison.OrdinalIgnoreCase)
                                                || request.Language.Value.Equals("c", StringComparison.OrdinalIgnoreCase)
                                                || Has(text, "memory", "asan", "ubsan", "use-after-free", "overflow", "double free"),
            DebugExperimentKind.RaceDetection => Has(text, "race", "thread", "concurrency", "tsan"),
            DebugExperimentKind.DeadlockDetection => Has(text, "deadlock", "hang", "lock", "mutex", "thread"),
            DebugExperimentKind.Security => Has(text, "security", "secret", "injection", "auth", "token", "vulnerability", "dependency"),
            DebugExperimentKind.Fuzz => Has(text, "fuzz", "parser", "protocol", "packet", "serialization", "input", "network", "api"),
            DebugExperimentKind.LoadTest => Has(text, "load", "throughput", "concurrency", "server", "api", "network", "traffic"),
            DebugExperimentKind.Benchmark => Has(text, "performance", "latency", "fps", "frame time", "frametime", "gpu", "cpu", "throughput"),
            DebugExperimentKind.Stress or DebugExperimentKind.Soak => Has(text, "stress", "soak", "memory leak", "memory growth", "thermal", "long-running", "reliability"),
            DebugExperimentKind.RuntimeReplay => Has(text, "runtime", "game", "unity", "unreal", "replay", "scene", "crash", "render", "network"),
            DebugExperimentKind.Bisect => !string.IsNullOrWhiteSpace(request.BaseCommit) && Has(text, "regression", "commit", "bisect", "worked before", "previous"),
            DebugExperimentKind.Mutation => Has(text, "mutation", "test quality", "coverage", "weak test"),
            _ => true
        };
    }

    private static bool HasExperimentConfiguration(string? json, DebugExperimentKind kind)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            var root = document.RootElement;
            if (TryGetProperty(root, "experiments", out var experiments) && experiments.ValueKind == JsonValueKind.Object)
                root = experiments;
            return root.EnumerateObject().Any(property =>
                property.Name.Equals(kind.ToString(), StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals(kind.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
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

    private static void AugmentExperimentsFromHypothesis(
        DebugHypothesis hypothesis,
        ICollection<DebugExperimentKind> kinds)
    {
        var text = $"{hypothesis.Title} {hypothesis.Explanation}".ToLowerInvariant();
        void Add(DebugExperimentKind kind)
        {
            if (!kinds.Contains(kind))
                kinds.Add(kind);
        }

        if (Has(text, "shader", "render", "visual", "pixel", "material", "texture", "framebuffer", "lighting"))
        {
            Add(DebugExperimentKind.RuntimeReplay);
            Add(DebugExperimentKind.VisualRegression);
            Add(DebugExperimentKind.Benchmark);
            if (Has(text, "shader", "hlsl", "glsl"))
                Add(DebugExperimentKind.ShaderValidation);
        }
        if (Has(text, "fps", "frame time", "frametime", "latency", "performance", "gpu", "cpu"))
            Add(DebugExperimentKind.Benchmark);
        if (Has(text, "memory leak", "memory growth", "out of memory", "retained memory"))
        {
            Add(DebugExperimentKind.Soak);
            Add(DebugExperimentKind.MemorySafety);
        }
        if (Has(text, "use-after-free", "buffer overflow", "double free", "asan", "ubsan", "memory-safety"))
            Add(DebugExperimentKind.MemorySafety);
        if (Has(text, "race", "thread sanitizer", "threadsanitizer"))
        {
            Add(DebugExperimentKind.RaceDetection);
            Add(DebugExperimentKind.Stress);
        }
        if (Has(text, "deadlock", "hang", "lock inversion"))
        {
            Add(DebugExperimentKind.DeadlockDetection);
            Add(DebugExperimentKind.Stress);
        }
        if (Has(text, "network", "packet", "socket", "protocol", "serialization", "api"))
        {
            Add(DebugExperimentKind.Fuzz);
            Add(DebugExperimentKind.LoadTest);
        }
        if (Has(text, "asset", "maya", "unity", "unreal", "prefab", "mesh", "missing reference"))
            Add(DebugExperimentKind.AssetValidation);
    }

    private static string? ValidateRegressionTests(
        IReadOnlyList<RegressionTestDraft> tests,
        string unifiedDiff)
    {
        if (tests.Count is < 1 or > 20)
            return "1-20 regression-test drafts are required.";

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var test in tests)
        {
            if (string.IsNullOrWhiteSpace(test.RelativePath) || Path.IsPathRooted(test.RelativePath))
                return "Every regression-test path must be a non-empty workspace-relative path.";
            var normalized = test.RelativePath.Replace('\\', '/').Trim();
            if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(x => x == ".."))
                return $"Regression-test path escapes the workspace: {test.RelativePath}";
            if (!seen.Add(normalized))
                return $"Duplicate regression-test path: {test.RelativePath}";
            if (!LooksLikeTestPath(normalized))
                return $"Regression-test draft must target an explicit test/spec path: {test.RelativePath}";
            if (normalized.StartsWith(".github/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("secrets", StringComparison.OrdinalIgnoreCase))
                return $"Regression-test path is protected: {test.RelativePath}";
            if (string.IsNullOrWhiteSpace(test.Content) || test.Content.Length > 256_000)
                return $"Regression test '{test.RelativePath}' must contain 1-256 KiB of source text.";
            if (string.IsNullOrWhiteSpace(test.Purpose) || test.Purpose.Length > 1000)
                return $"Regression test '{test.RelativePath}' requires a concise Purpose.";
            if (ContainsSuppression(test.Content))
                return $"Regression test '{test.RelativePath}' contains a skip/suppression construct.";

            // Keep Red->Green evidence unambiguous: generated regression tests are
            // separate artifacts, not additional edits hidden inside the proposed patch.
            if (DiffTouchesPath(unifiedDiff, normalized))
                return $"Regression-test draft path is also modified by the patch: {test.RelativePath}";
        }

        return null;
    }

    private static bool LooksLikeTestPath(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.StartsWith("test")
            || lower.Contains("/test")
            || lower.Contains("/spec")
            || lower.EndsWith(".spec.ts")
            || lower.EndsWith(".test.ts")
            || lower.EndsWith("_test.go")
            || lower.EndsWith("_test.py")
            || lower.Contains("tests.")
            || lower.Contains("test.");
    }

    private static bool ContainsSuppression(string content)
    {
        var tokens = new[]
        {
            "pytest.skip", "unittest.skip", "test.skip(", "describe.skip(", "Assert.Ignore",
            "@Ignore", "@Disabled", "#pragma warning disable", "eslint-disable", "noinspection"
        };
        return tokens.Any(token => content.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool DiffTouchesPath(string diff, string normalizedPath) =>
        diff.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("+++ b/", StringComparison.Ordinal))
            .Select(line => line[6..].Replace('\\', '/').Trim())
            .Any(path => path.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

    private static bool Has(string text, params string[] tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool HasUnverifiableRequiredStep(
        AutonomousQaExperimentPlan plan,
        IReadOnlyList<AutonomousQaExperimentResult> results)
    {
        var byId = results.ToDictionary(x => x.StepId, StringComparer.OrdinalIgnoreCase);
        return plan.Steps
            .Where(x => x.Required)
            .Any(step =>
                !byId.TryGetValue(step.Id, out var result)
                || result.Status is AutonomousQaExperimentStatus.Unavailable
                    or AutonomousQaExperimentStatus.NotConfigured
                    or AutonomousQaExperimentStatus.InfrastructureFailure
                    or AutonomousQaExperimentStatus.Skipped);
    }

    private static bool RequiredStepsAllPassed(
        AutonomousQaExperimentPlan plan,
        IReadOnlyList<AutonomousQaExperimentResult> results)
    {
        var byId = results.ToDictionary(x => x.StepId, StringComparer.OrdinalIgnoreCase);
        var required = plan.Steps.Where(x => x.Required).ToArray();
        return required.Length > 0
            && required.All(step =>
                byId.TryGetValue(step.Id, out var result)
                && result.Status == AutonomousQaExperimentStatus.Passed);
    }

    private static string FormatExperimentResults(IReadOnlyList<AutonomousQaExperimentResult> results)
    {
        if (results.Count == 0)
            return "No formal baseline experiment result is available.";
        return string.Join(
            Environment.NewLine,
            results.Select(x =>
                $"- {x.StepId} [{x.Kind}] {x.Status}: {x.Summary}"));
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
            hasExperimentConfiguration = !string.IsNullOrWhiteSpace(request.ExperimentConfigurationJson),
            safety = new
            {
                sourceWorkspaceModified = false,
                automaticMerge = false,
                arbitraryShellFromAi = false,
                aiCanDeclarePass = false,
                requiredExperimentsFailClosed = true,
                regressionTestRedGreenRequired = true,
                experimentApplicabilityChecked = true
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
                "2",
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
                "2",
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
                    plan = attempt.ExperimentPlan,
                    baseline = attempt.BaselineExperiments,
                    postPatch = attempt.PostPatchExperiments
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
        if (request.ExperimentConfigurationJson?.Length > 262144)
            throw new ArgumentException("ExperimentConfigurationJson exceeds the 256 KiB autonomous-debug input limit.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.WorkspaceRelativePath))
            throw new ArgumentException("WorkspaceRelativePath is required.", nameof(request));
        if (request.WorkspaceRelativePath.Length > 2000)
            throw new ArgumentException("WorkspaceRelativePath is too long.", nameof(request));
        if (Path.IsPathRooted(request.WorkspaceRelativePath))
            throw new ArgumentException("WorkspaceRelativePath must remain relative to the configured QA root.", nameof(request));
        if (request.WorkspaceRelativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Any(x => x == ".."))
            throw new ArgumentException("WorkspaceRelativePath cannot escape the configured QA root.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DomainId))
            throw new ArgumentException("DomainId is required.", nameof(request));
        if (request.DomainId.Length > 120)
            throw new ArgumentException("DomainId is too long.", nameof(request));
        if (request.RequestedSessionId.HasValue && request.RequestedSessionId.Value == Guid.Empty)
            throw new ArgumentException("RequestedSessionId cannot be Guid.Empty.", nameof(request));
    }
}
