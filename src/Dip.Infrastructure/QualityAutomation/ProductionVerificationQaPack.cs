// Purpose: Executes fail-closed production QA for localization, accessibility, input/save/device/certification, GPU capture, graphics semantics, distributed networking, and 24/365 evidence.
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class ProductionVerificationQaPack(ProcessExecutor executor)
    : ICrossCuttingQaPack
{
    private const string ManifestRelativePath =
        ".dip/production-verification.json";

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

    public string PackId => "production-verification";

    public bool Applies(
        QaTechnologyProfile profile,
        string workspace)
    {
        return File.Exists(
            Path.Combine(
                workspace,
                ManifestRelativePath));
    }

    public async Task<QaCrossCuttingResult> RunAsync(
        QaExecutionCommand command,
        QaTechnologyProfile profile,
        string workspace,
        string artifactRoot,
        CancellationToken ct)
    {
        Directory.CreateDirectory(artifactRoot);

        var manifestPath =
            Path.Combine(
                workspace,
                ManifestRelativePath);

        var tools = new List<QaToolExecution>();
        var findings = new List<QaFindingCandidate>();
        var evidence = new List<object>();

        ProductionVerificationManifest manifest;

        try
        {
            manifest =
                JsonSerializer.Deserialize<ProductionVerificationManifest>(
                    await File.ReadAllTextAsync(
                        manifestPath,
                        ct),
                    Json)
                ?? new ProductionVerificationManifest();
        }
        catch (JsonException ex)
        {
            findings.Add(
                F(
                    "production-verification",
                    Severity.Critical,
                    "Invalid production verification manifest",
                    ex.Message,
                    ManifestRelativePath,
                    "DIP-PROD-MANIFEST"));

            tools.Add(
                Unavailable(
                    "production-verification-manifest",
                    "Manifest JSON is invalid."));

            return await FinishAsync();
        }

        await RunContractAsync(
            "localization",
            manifest.Localization,
            ValidateLocalizationAsync);

        await RunContractAsync(
            "accessibility",
            manifest.Accessibility,
            ValidateAccessibilityAsync);

        await RunContractAsync(
            "input-device-matrix",
            manifest.InputDeviceMatrix,
            ValidateInputMatrixAsync);

        await RunContractAsync(
            "save-compatibility",
            manifest.SaveCompatibility,
            ValidateSaveAsync);

        await RunContractAsync(
            "console-certification",
            manifest.ConsoleCertification,
            ValidateCertificationAsync);

        await RunContractAsync(
            "device-farm",
            manifest.DeviceFarm,
            ValidateDeviceFarmAsync);

        await RunContractAsync(
            "professional-gpu-capture",
            manifest.ProfessionalGpuCapture,
            ValidateGpuCaptureAsync);

        await RunContractAsync(
            "semantic-graphics",
            manifest.SemanticGraphicsQa,
            ValidateSemanticGraphicsAsync);

        await RunContractAsync(
            "distributed-multiplayer",
            manifest.DistributedMultiplayer,
            ValidateDistributedAsync);

        await RunContractAsync(
            "continuous-24x365",
            manifest.ContinuousVerification,
            ValidateContinuousAsync);

        await RunContractAsync(
            "windows-native-race",
            manifest.WindowsNativeRace,
            ValidateWindowsNativeRaceAsync);

        await RunContractAsync(
            "asset-repository",
            manifest.AssetRepositoryQa,
            ValidateAssetRepositoryAsync);

        await RunContractAsync(
            "shader-qa",
            manifest.ShaderQa,
            ValidateShaderQaAsync);

        return await FinishAsync();

        async Task RunContractAsync(
            string id,
            VerificationContract? spec,
            Func<
                string,
                VerificationContract,
                Task<BuiltInAssessment>> builtIn)
        {
            ct.ThrowIfCancellationRequested();

            if (spec is null || !spec.Enabled)
            {
                tools.Add(
                    Unavailable(
                        $"prod:{id}",
                        $"{id} is not configured; status=NOT_EVALUATED."));
                return;
            }

            var assessment =
                await builtIn(
                    id,
                    spec);

            findings.AddRange(
                assessment.Findings);

            var matrix =
                (spec.Matrix is { Count: > 0 }
                    ? spec.Matrix
                    : ["default"])
                .Where(
                    x => !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(64)
                .ToArray();

            if (matrix.Length == 0)
            {
                matrix = ["default"];
            }

            if (string.IsNullOrWhiteSpace(spec.Command))
            {
                var critical =
                    assessment.Findings.Any(
                        x => x.Severity == Severity.Critical);

                tools.Add(
                    new QaToolExecution(
                        $"prod:{id}",
                        assessment.CanPassWithoutRuntime,
                        assessment.CanPassWithoutRuntime &&
                        !critical,
                        assessment.CanPassWithoutRuntime &&
                        !critical
                            ? 0
                            : null,
                        0,
                        "",
                        "",
                        assessment.CanPassWithoutRuntime
                            ? $"{id} built-in verification completed."
                            : $"{id} runtime contract is missing; built-in/static evidence cannot establish PASS."));

                return;
            }

            var executable =
                ResolveExecutable(
                    workspace,
                    spec.Command!);

            if (executable is null)
            {
                tools.Add(
                    Unavailable(
                        $"prod:{id}",
                        $"Configured command '{spec.Command}' could not be resolved."));

                findings.Add(
                    F(
                        id,
                        Severity.Warning,
                        $"{id} verification tool unavailable",
                        $"Configured command '{spec.Command}' is unavailable; status=NOT_EVALUATED.",
                        ManifestRelativePath,
                        $"DIP-PROD-{Rule(id)}-TOOL"));

                return;
            }

            var timeout =
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        spec.TimeoutSeconds ??
                        command.TimeoutSeconds,
                        5,
                        7200));

            var allSucceeded = true;
            long duration = 0;
            string firstOut = "";
            string firstErr = "";

            foreach (var matrixValue in matrix)
            {
                var runRoot =
                    Path.Combine(
                        artifactRoot,
                        id,
                        Sanitize(matrixValue));

                Directory.CreateDirectory(runRoot);

                var stdout =
                    Path.Combine(
                        runRoot,
                        "stdout.log");

                var stderr =
                    Path.Combine(
                        runRoot,
                        "stderr.log");

                if (string.IsNullOrEmpty(firstOut))
                {
                    firstOut = Rel(stdout);
                    firstErr = Rel(stderr);
                }

                var args =
                    (spec.Arguments ?? [])
                    .Select(
                        x => x
                            .Replace(
                                "{matrix}",
                                matrixValue,
                                StringComparison.Ordinal)
                            .Replace(
                                "{workspace}",
                                workspace,
                                StringComparison.Ordinal)
                            .Replace(
                                "{artifactRoot}",
                                runRoot,
                                StringComparison.Ordinal))
                    .ToArray();

                var env =
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        ["DIP_QA_CONTRACT"] = id,
                        ["DIP_QA_MATRIX_VALUE"] = matrixValue,
                        ["DIP_QA_ARTIFACT_ROOT"] = runRoot
                    };

                if (spec.Environment is not null)
                {
                    foreach (var pair in spec.Environment)
                    {
                        if (!string.IsNullOrWhiteSpace(pair.Key))
                        {
                            env[pair.Key] =
                                pair.Value ??
                                string.Empty;
                        }
                    }
                }

                var execution =
                    await executor.RunQaAsync(
                        executable,
                        args,
                        workspace,
                        env,
                        timeout,
                        stdout,
                        stderr,
                        ct);

                duration += execution.DurationMs;

                var runSucceeded =
                    execution.ExitCode == 0 &&
                    !execution.TimedOut;

                allSucceeded &= runSucceeded;

                evidence.Add(
                    new
                    {
                        contract = id,
                        matrix = matrixValue,
                        execution.ExitCode,
                        execution.TimedOut,
                        execution.DurationMs,
                        execution.CpuTimeMs,
                        sandbox = execution.Sandbox
                    });

                if (!runSucceeded)
                {
                    findings.Add(
                        F(
                            id,
                            Severity.Critical,
                            $"{id} runtime verification failed",
                            execution.TimedOut
                                ? $"Matrix '{matrixValue}' timed out."
                                : $"Matrix '{matrixValue}' exited with code {execution.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}.",
                            ManifestRelativePath,
                            $"DIP-PROD-{Rule(id)}-RUNTIME"));
                }
            }

            var requiredEvidence =
                ResolveEvidenceFiles(
                    workspace,
                    artifactRoot,
                    spec.RequiredEvidenceFiles);

            var minimumEvidence =
                Math.Clamp(
                    spec.MinimumEvidenceFiles ?? 1,
                    1,
                    10000);

            var evidenceSatisfied =
                requiredEvidence.Count >= minimumEvidence;

            if (!evidenceSatisfied)
            {
                allSucceeded = false;

                findings.Add(
                    F(
                        id,
                        Severity.Critical,
                        $"{id} evidence contract was not satisfied",
                        $"Found {requiredEvidence.Count} non-empty evidence file(s); required minimum is {minimumEvidence}.",
                        ManifestRelativePath,
                        $"DIP-PROD-{Rule(id)}-EVIDENCE"));
            }

            var blocking =
                findings.Any(
                    x =>
                        x.Severity == Severity.Critical &&
                        x.Category.Equals(
                            id,
                            StringComparison.OrdinalIgnoreCase));

            var succeeded =
                allSucceeded &&
                evidenceSatisfied &&
                !blocking;

            tools.Add(
                new QaToolExecution(
                    $"prod:{id}",
                    true,
                    succeeded,
                    succeeded
                        ? 0
                        : 1,
                    duration,
                    firstOut,
                    firstErr,
                    succeeded
                        ? $"{id} verified across {matrix.Length} matrix value(s) with {requiredEvidence.Count} evidence artifact(s)."
                        : $"{id} did not satisfy its runtime/evidence contract."));
        }

        async Task<BuiltInAssessment> ValidateLocalizationAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var required =
                spec.RequiredCultures?
                    .Where(
                        x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                ?? [];

            if (required.Length == 0)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Localization culture contract is missing",
                        "Configure requiredCultures for release locales.",
                        ManifestRelativePath,
                        "DIP-PROD-LOCALIZATION-CONFIG"));
            }

            var resourceFiles =
                EnumerateFiles(workspace)
                    .Where(
                        x => Regex.IsMatch(
                            Path.GetFileName(x),
                            @"\.(resx|po|json|strings|arb)$",
                            RegexOptions.IgnoreCase))
                    .Take(20000)
                    .ToArray();

            foreach (var culture in required)
            {
                var token =
                    culture.Replace(
                        '-',
                        '_');

                var found =
                    resourceFiles.Any(
                        x =>
                            x.Contains(
                                culture,
                                StringComparison.OrdinalIgnoreCase) ||
                            x.Contains(
                                token,
                                StringComparison.OrdinalIgnoreCase));

                if (!found)
                {
                    rows.Add(
                        F(
                            id,
                            Severity.Critical,
                            "Required localization culture is missing",
                            $"No localization resource found for '{culture}'.",
                            ManifestRelativePath,
                            "DIP-PROD-LOCALIZATION-CULTURE"));
                }
            }

            foreach (var file in resourceFiles
                         .Where(
                             x => x.EndsWith(
                                 ".json",
                                 StringComparison.OrdinalIgnoreCase))
                         .Take(2000))
            {
                try
                {
                    using var _ =
                        JsonDocument.Parse(
                            await File.ReadAllTextAsync(
                                file,
                                ct));
                }
                catch (JsonException ex)
                {
                    rows.Add(
                        F(
                            id,
                            Severity.Critical,
                            "Invalid localization JSON",
                            ex.Message,
                            Rel(file),
                            "DIP-PROD-LOCALIZATION-JSON"));
                }
            }

            return new(false, rows);
        }

        Task<BuiltInAssessment> ValidateAccessibilityAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var files =
                EnumerateFiles(workspace)
                    .Where(
                        x =>
                            x.EndsWith(
                                ".html",
                                StringComparison.OrdinalIgnoreCase) ||
                            x.EndsWith(
                                ".razor",
                                StringComparison.OrdinalIgnoreCase) ||
                            x.EndsWith(
                                ".tsx",
                                StringComparison.OrdinalIgnoreCase) ||
                            x.EndsWith(
                                ".jsx",
                                StringComparison.OrdinalIgnoreCase))
                    .Take(5000);

            foreach (var file in files)
            {
                string text;

                try
                {
                    text =
                        File.ReadAllText(file);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if (Regex.IsMatch(
                        text,
                        @"<img\b(?![^>]*\balt\s*=)[^>]*>",
                        RegexOptions.IgnoreCase))
                {
                    rows.Add(
                        F(
                            id,
                            Severity.Warning,
                            "Image has no alt text",
                            "Static preflight found an image element without alt text; runtime accessibility tooling remains authoritative.",
                            Rel(file),
                            "DIP-PROD-A11Y-ALT"));
                }
            }

            return Task.FromResult(
                new BuiltInAssessment(
                    false,
                    rows));
        }

        Task<BuiltInAssessment> ValidateInputMatrixAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var count =
                spec.Matrix?
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count()
                ?? 0;

            var minimum =
                Math.Max(
                    2,
                    spec.MinimumMatrixEntries ?? 2);

            if (count < minimum)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Input device matrix is incomplete",
                        "Configure explicit keyboard/mouse, controller, touch/VR or project-specific device profiles.",
                        ManifestRelativePath,
                        "DIP-PROD-INPUT-MATRIX"));
            }

            return Task.FromResult(
                new BuiltInAssessment(
                    false,
                    rows));
        }

        Task<BuiltInAssessment> ValidateSaveAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            if (spec.Fixtures is null ||
                spec.Fixtures.Count == 0)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "No save compatibility fixtures configured",
                        "Historical/current save fixtures are required.",
                        ManifestRelativePath,
                        "DIP-PROD-SAVE-FIXTURE"));
            }

            foreach (var fixture in (spec.Fixtures ?? []).Take(200))
            {
                var full =
                    ResolveWorkspacePath(
                        workspace,
                        fixture);

                if (full is null ||
                    !File.Exists(full))
                {
                    rows.Add(
                        F(
                            id,
                            Severity.Critical,
                            "Save compatibility fixture is missing",
                            $"Fixture '{fixture}' is unavailable.",
                            ManifestRelativePath,
                            "DIP-PROD-SAVE-MISSING"));
                }
            }

            return Task.FromResult(
                new BuiltInAssessment(
                    false,
                    rows));
        }

        async Task<BuiltInAssessment> ValidateCertificationAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var path =
                string.IsNullOrWhiteSpace(spec.ChecklistPath)
                    ? null
                    : ResolveWorkspacePath(
                        workspace,
                        spec.ChecklistPath!);

            if (path is null ||
                !File.Exists(path))
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Certification checklist is unavailable",
                        "Configure checklistPath with explicit rule status and evidence.",
                        ManifestRelativePath,
                        "DIP-PROD-CERT-CHECKLIST"));

                return new(false, rows);
            }

            try
            {
                using var doc =
                    JsonDocument.Parse(
                        await File.ReadAllTextAsync(
                            path,
                            ct));

                if (!doc.RootElement.TryGetProperty(
                        "rules",
                        out var rules) ||
                    rules.ValueKind != JsonValueKind.Array)
                {
                    rows.Add(
                        F(
                            id,
                            Severity.Critical,
                            "Certification checklist has no rules array",
                            "Checklist cannot prove readiness.",
                            Rel(path),
                            "DIP-PROD-CERT-SCHEMA"));
                }
                else
                {
                    foreach (var rule in rules
                                 .EnumerateArray()
                                 .Take(2000))
                    {
                        var rid =
                            rule.TryGetProperty(
                                "id",
                                out var r)
                                ? r.GetString()
                                : "unknown";

                        var status =
                            rule.TryGetProperty(
                                "status",
                                out var s)
                                ? s.GetString()
                                : null;

                        var ev =
                            rule.TryGetProperty(
                                "evidence",
                                out var e)
                                ? e.GetString()
                                : null;

                        if (!string.Equals(
                                status,
                                "passed",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(ev))
                        {
                            rows.Add(
                                F(
                                    id,
                                    Severity.Critical,
                                    "Certification rule is not evidenced",
                                    $"Rule '{rid}' status='{status ?? "missing"}', evidence='{ev ?? "missing"}'.",
                                    Rel(path),
                                    "DIP-PROD-CERT-RULE"));
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Invalid certification checklist JSON",
                        ex.Message,
                        Rel(path),
                        "DIP-PROD-CERT-JSON"));
            }

            return new(false, rows);
        }

        Task<BuiltInAssessment> ValidateDeviceFarmAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var count =
                spec.Matrix?
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count()
                ?? 0;

            var minimum =
                Math.Max(
                    2,
                    spec.MinimumMatrixEntries ?? 2);

            if (count < minimum)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Device farm coverage is below policy",
                        $"Configured {count} device profile(s); required {minimum}.",
                        ManifestRelativePath,
                        "DIP-PROD-DEVICE-COVERAGE"));
            }

            return Task.FromResult(
                new BuiltInAssessment(
                    false,
                    rows));
        }

        async Task<BuiltInAssessment> ValidateGpuCaptureAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            if (spec.RequiredEvidenceFiles is null ||
                spec.RequiredEvidenceFiles.Count == 0)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "GPU capture artifact contract is missing",
                        "Require RenderDoc/PIX/Nsight/Xcode GPU capture plus an exported machine-readable report.",
                        ManifestRelativePath,
                        "DIP-PROD-GPU-CAPTURE"));
            }

            var reportPath =
                string.IsNullOrWhiteSpace(spec.GpuCaptureReportPath)
                    ? null
                    : ResolveWorkspacePath(
                        workspace,
                        spec.GpuCaptureReportPath!);

            if (reportPath is null ||
                !File.Exists(reportPath))
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "GPU capture report is unavailable",
                        "Configure gpuCaptureReportPath with tool/api/frame/marker metadata exported by the capture pipeline.",
                        ManifestRelativePath,
                        "DIP-PROD-GPU-REPORT"));

                return new(false, rows);
            }

            try
            {
                using var doc =
                    JsonDocument.Parse(
                        await File.ReadAllTextAsync(
                            reportPath,
                            ct));

                var root =
                    doc.RootElement;

                var tool =
                    root.TryGetProperty(
                        "tool",
                        out var t)
                        ? t.GetString()
                        : null;

                var api =
                    root.TryGetProperty(
                        "api",
                        out var a)
                        ? a.GetString()
                        : null;

                var capture =
                    root.TryGetProperty(
                        "captureFile",
                        out var c)
                        ? c.GetString()
                        : null;

                if (string.IsNullOrWhiteSpace(tool) ||
                    string.IsNullOrWhiteSpace(api) ||
                    string.IsNullOrWhiteSpace(capture))
                {
                    rows.Add(
                        F(
                            id,
                            Severity.Critical,
                            "GPU capture report is incomplete",
                            "Expected tool, api and captureFile fields.",
                            Rel(reportPath),
                            "DIP-PROD-GPU-SCHEMA"));
                }

                var markers =
                    root.TryGetProperty(
                        "markers",
                        out var m) &&
                    m.ValueKind == JsonValueKind.Array
                        ? m.EnumerateArray()
                            .Where(
                                x =>
                                    x.ValueKind ==
                                    JsonValueKind.String)
                            .Select(
                                x =>
                                    x.GetString() ??
                                    string.Empty)
                            .ToHashSet(
                                StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);

                foreach (var required in spec.RequiredMarkers ?? [])
                {
                    if (!markers.Contains(required))
                    {
                        rows.Add(
                            F(
                                id,
                                Severity.Critical,
                                "Required GPU capture marker is missing",
                                $"Marker '{required}' was not present in the capture report.",
                                Rel(reportPath),
                                "DIP-PROD-GPU-MARKER"));
                    }
                }
            }
            catch (JsonException ex)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Invalid GPU capture report JSON",
                        ex.Message,
                        Rel(reportPath),
                        "DIP-PROD-GPU-JSON"));
            }

            return new(false, rows);
        }

        async Task<BuiltInAssessment> ValidateSemanticGraphicsAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var path =
                string.IsNullOrWhiteSpace(spec.SemanticScenePath)
                    ? null
                    : ResolveWorkspacePath(
                        workspace,
                        spec.SemanticScenePath!);

            if (path is null ||
                !File.Exists(path))
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Semantic graphics report is unavailable",
                        "Configure semanticScenePath containing objects[] with stable semantic ids.",
                        ManifestRelativePath,
                        "DIP-PROD-GFX-MISSING"));

                return new(false, rows);
            }

            try
            {
                using var doc =
                    JsonDocument.Parse(
                        await File.ReadAllTextAsync(
                            path,
                            ct));

                if (!doc.RootElement.TryGetProperty(
                        "objects",
                        out var objects) ||
                    objects.ValueKind != JsonValueKind.Array)
                {
                    rows.Add(
                        F(
                            id,
                            Severity.Critical,
                            "Semantic graphics report has no object set",
                            "Expected objects[] with stable object/material/visibility semantics.",
                            Rel(path),
                            "DIP-PROD-GFX-SCHEMA"));
                }
                else
                {
                    var ids =
                        new HashSet<string>(
                            StringComparer.Ordinal);

                    foreach (var obj in objects
                                 .EnumerateArray()
                                 .Take(100000))
                    {
                        var key =
                            obj.TryGetProperty(
                                "id",
                                out var value)
                                ? value.GetString()
                                : null;

                        if (string.IsNullOrWhiteSpace(key))
                        {
                            rows.Add(
                                F(
                                    id,
                                    Severity.Warning,
                                    "Rendered object has no stable semantic id",
                                    "Object-level regressions cannot be correlated reliably.",
                                    Rel(path),
                                    "DIP-PROD-GFX-ID"));
                        }
                        else if (!ids.Add(key))
                        {
                            rows.Add(
                                F(
                                    id,
                                    Severity.Critical,
                                    "Duplicate semantic graphics id",
                                    $"Object id '{key}' is duplicated.",
                                    Rel(path),
                                    "DIP-PROD-GFX-DUPLICATE"));
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Invalid semantic graphics report",
                        ex.Message,
                        Rel(path),
                        "DIP-PROD-GFX-JSON"));
            }

            return new(false, rows);
        }

        Task<BuiltInAssessment> ValidateDistributedAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var count =
                spec.Matrix?
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count()
                ?? 0;

            var minimum =
                Math.Max(
                    1,
                    spec.MinimumMatrixEntries ?? 1);

            if (count < minimum)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Distributed topology coverage is below policy",
                        "Configure dedicated/listen/P2P/relay/NAT or project-specific topology matrix values.",
                        ManifestRelativePath,
                        "DIP-PROD-NET-TOPOLOGY"));
            }

            return Task.FromResult(
                new BuiltInAssessment(
                    false,
                    rows));
        }

        async Task<BuiltInAssessment> ValidateContinuousAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var path =
                string.IsNullOrWhiteSpace(spec.HeartbeatJournalPath)
                    ? null
                    : ResolveWorkspacePath(
                        workspace,
                        spec.HeartbeatJournalPath!);

            if (path is null ||
                !File.Exists(path))
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "24/365 heartbeat journal is unavailable",
                        "Configure heartbeatJournalPath produced by the real continuous verifier.",
                        ManifestRelativePath,
                        "DIP-PROD-24X365-JOURNAL"));

                return new(false, rows);
            }

            var samples =
                new List<DateTimeOffset>();

            foreach (var line in await File.ReadAllLinesAsync(
                         path,
                         ct))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var doc =
                        JsonDocument.Parse(line);

                    if (doc.RootElement.TryGetProperty(
                            "timestampUtc",
                            out var ts) &&
                        ts.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(
                            ts.GetString(),
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal,
                            out var parsed))
                    {
                        samples.Add(
                            parsed.ToUniversalTime());
                    }
                }
                catch (JsonException)
                {
                    rows.Add(
                        F(
                            id,
                            Severity.Critical,
                            "Malformed heartbeat journal row",
                            line[..Math.Min(line.Length, 180)],
                            Rel(path),
                            "DIP-PROD-24X365-ROW"));
                }
            }

            samples =
                samples
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

            if (samples.Count < 2)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Insufficient 24/365 heartbeat evidence",
                        $"Observed {samples.Count} valid heartbeat(s).",
                        Rel(path),
                        "DIP-PROD-24X365-SAMPLES"));

                return new(false, rows);
            }

            var hours =
                (samples[^1] - samples[0])
                .TotalHours;

            var minimumHours =
                Math.Clamp(
                    spec.MinimumContinuousHours ?? 24,
                    1,
                    24 * 365);

            if (hours < minimumHours)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Continuous verification window is too short",
                        $"Observed {hours:0.##}h; required {minimumHours}h.",
                        Rel(path),
                        "DIP-PROD-24X365-WINDOW"));
            }

            var maxGap =
                Math.Clamp(
                    spec.MaximumHeartbeatGapMinutes ?? 10,
                    1,
                    24 * 60);

            var worstGap =
                samples
                    .Zip(
                        samples.Skip(1),
                        (a, b) =>
                            (b - a).TotalMinutes)
                    .DefaultIfEmpty(0)
                    .Max();

            if (worstGap > maxGap)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Continuous verification heartbeat gap exceeded",
                        $"Worst gap {worstGap:0.##} min; maximum {maxGap} min.",
                        Rel(path),
                        "DIP-PROD-24X365-GAP"));
            }

            var stale =
                (DateTimeOffset.UtcNow - samples[^1])
                .TotalMinutes;

            if (stale > maxGap * 2)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Continuous verification evidence is stale",
                        $"Last heartbeat is {stale:0.##} minutes old.",
                        Rel(path),
                        "DIP-PROD-24X365-STALE"));
            }

            evidence.Add(
                new
                {
                    contract = id,
                    samples = samples.Count,
                    coverageHours = hours,
                    worstGapMinutes = worstGap,
                    lastHeartbeatUtc = samples[^1]
                });

            return new(true, rows);
        }

        Task<BuiltInAssessment> ValidateWindowsNativeRaceAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            if (!OperatingSystem.IsWindows())
            {
                rows.Add(
                    F(
                        id,
                        Severity.Warning,
                        "Windows native race contract is running on a non-Windows host",
                        "Dedicated Windows race evidence is NOT_EVALUATED on this runner.",
                        ManifestRelativePath,
                        "DIP-PROD-WINRACE-HOST"));
            }

            if (string.IsNullOrWhiteSpace(spec.Command))
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Windows native race harness is not configured",
                        "Configure a concurrency harness (for example Intel Inspector, Application Verifier-backed stress, or a project-owned race probe) and require its evidence artifact.",
                        ManifestRelativePath,
                        "DIP-PROD-WINRACE-CONFIG"));
            }

            var count =
                spec.Matrix?.Count ?? 0;

            var minimum =
                Math.Max(
                    3,
                    spec.MinimumMatrixEntries ?? 3);

            if (count < minimum)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Warning,
                        "Windows native race repetition matrix is small",
                        "Use multiple seeds/schedules/repetitions; a single clean run is weak race evidence.",
                        ManifestRelativePath,
                        "DIP-PROD-WINRACE-MATRIX"));
            }

            return Task.FromResult(
                new BuiltInAssessment(
                    false,
                    rows));
        }

        Task<BuiltInAssessment> ValidateAssetRepositoryAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var assetsRoot =
                Path.Combine(
                    workspace,
                    "Assets");

            if (Directory.Exists(assetsRoot))
            {
                var assets =
                    EnumerateFiles(assetsRoot)
                        .Take(20000)
                        .ToArray();

                foreach (var asset in assets
                             .Where(
                                 x =>
                                     !x.EndsWith(
                                         ".meta",
                                         StringComparison.OrdinalIgnoreCase))
                             .Take(12000))
                {
                    if (!File.Exists(asset + ".meta"))
                    {
                        rows.Add(
                            F(
                                id,
                                Severity.Warning,
                                "Unity asset is missing .meta",
                                "Repository-level asset QA can prove reference hygiene without collector metadata.",
                                Rel(asset),
                                "DIP-PROD-ASSET-META"));
                    }
                }

                var owners =
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);

                foreach (var meta in assets
                             .Where(
                                 x =>
                                     x.EndsWith(
                                         ".meta",
                                         StringComparison.OrdinalIgnoreCase))
                             .Take(12000))
                {
                    try
                    {
                        var guid =
                            File.ReadLines(meta)
                                .Take(20)
                                .FirstOrDefault(
                                    x =>
                                        x.StartsWith(
                                            "guid:",
                                            StringComparison.OrdinalIgnoreCase))?[5..]
                                .Trim();

                        if (string.IsNullOrWhiteSpace(guid))
                        {
                            continue;
                        }

                        if (owners.TryGetValue(
                                guid,
                                out var first) &&
                            !first.Equals(
                                meta,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            rows.Add(
                                F(
                                    id,
                                    Severity.Critical,
                                    "Duplicate Unity asset GUID",
                                    $"GUID '{guid}' is shared by '{Rel(first)}' and '{Rel(meta)}'.",
                                    Rel(meta),
                                    "DIP-PROD-ASSET-GUID"));
                        }
                        else
                        {
                            owners[guid] = meta;
                        }
                    }
                    catch (IOException)
                    {
                        continue;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                }
            }

            var assetExtensions =
                new[]
                {
                    ".fbx",
                    ".ma",
                    ".mb",
                    ".blend",
                    ".png",
                    ".jpg",
                    ".jpeg",
                    ".wav",
                    ".exr",
                    ".dpx",
                    ".uasset"
                };

            foreach (var file in EnumerateFiles(workspace)
                         .Where(
                             x =>
                                 assetExtensions.Contains(
                                     Path.GetExtension(x),
                                     StringComparer.OrdinalIgnoreCase))
                         .Take(20000))
            {
                try
                {
                    if (new FileInfo(file).Length == 0)
                    {
                        rows.Add(
                            F(
                                id,
                                Severity.Critical,
                                "Zero-byte production asset",
                                "Asset cannot be treated as valid production evidence.",
                                Rel(file),
                                "DIP-PROD-ASSET-ZERO"));
                    }
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
            }

            return Task.FromResult(
                new BuiltInAssessment(
                    true,
                    rows));
        }

        Task<BuiltInAssessment> ValidateShaderQaAsync(
            string id,
            VerificationContract spec)
        {
            var rows =
                new List<QaFindingCandidate>();

            var shaderExtensions =
                new[]
                {
                    ".hlsl",
                    ".compute",
                    ".glsl",
                    ".vert",
                    ".frag",
                    ".geom"
                };

            var shaders =
                EnumerateFiles(workspace)
                    .Where(
                        x =>
                            shaderExtensions.Contains(
                                Path.GetExtension(x),
                                StringComparer.OrdinalIgnoreCase))
                    .Take(10000)
                    .ToArray();

            if (shaders.Length > 0 &&
                string.IsNullOrWhiteSpace(spec.Command))
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Shader runtime/reflection contract is missing",
                        "Compile-only validation is insufficient here. Configure a shader compiler/reflection or engine validation command and require reflection/disassembly artifacts.",
                        ManifestRelativePath,
                        "DIP-PROD-SHADER-CONFIG"));
            }

            if (shaders.Length > 0 &&
                (spec.RequiredEvidenceFiles?.Count ?? 0) == 0)
            {
                rows.Add(
                    F(
                        id,
                        Severity.Critical,
                        "Shader evidence artifact contract is missing",
                        "Require compiler diagnostics plus reflection/disassembly or engine shader validation output.",
                        ManifestRelativePath,
                        "DIP-PROD-SHADER-EVIDENCE"));
            }

            return Task.FromResult(
                new BuiltInAssessment(
                    false,
                    rows));
        }

        async Task<QaCrossCuttingResult> FinishAsync()
        {
            var summary =
                JsonSerializer.Serialize(
                    new
                    {
                        schema =
                            "dip.production-verification.v1",
                        pack = PackId,
                        manifest =
                            ManifestRelativePath,
                        tools =
                            tools.Select(
                                x =>
                                    new
                                    {
                                        x.Tool,
                                        x.WasAvailable,
                                        x.Succeeded,
                                        x.ExitCode,
                                        x.DurationMs,
                                        x.Summary
                                    }),
                        findings =
                            findings.Select(
                                x =>
                                    new
                                    {
                                        x.Category,
                                        severity =
                                            x.Severity.ToString(),
                                        x.RuleId,
                                        x.Title,
                                        x.FilePath
                                    }),
                        evidence
                    },
                    Json);

            await File.WriteAllTextAsync(
                Path.Combine(
                    artifactRoot,
                    "production-verification.json"),
                summary,
                ct);

            return new(
                PackId,
                tools,
                findings,
                summary);
        }

        string Rel(string path)
        {
            return Path.GetRelativePath(
                    workspace,
                    path)
                .Replace(
                    '\\',
                    '/');
        }
    }

    private static string? ResolveExecutable(
        string workspace,
        string configured)
    {
        var text =
            configured.Trim();

        if (Path.IsPathRooted(text))
        {
            var candidate =
                Path.GetFullPath(text);

            var root =
                Path.GetFullPath(workspace)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            var comparison =
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

            return candidate.StartsWith(
                       root,
                       comparison) &&
                   File.Exists(candidate)
                ? candidate
                : null;
        }

        if (text.Contains(
                Path.DirectorySeparatorChar) ||
            text.Contains(
                Path.AltDirectorySeparatorChar))
        {
            var candidate =
                ResolveWorkspacePath(
                    workspace,
                    text);

            return candidate is not null &&
                   File.Exists(candidate)
                ? candidate
                : null;
        }

        return ProcessExecutor.FindExecutable(text);
    }

    private static string? ResolveWorkspacePath(
        string workspace,
        string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) ||
            Path.IsPathRooted(relative))
        {
            return null;
        }

        var root =
            Path.GetFullPath(workspace);

        var full =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    relative));

        var comparison =
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        var prefix =
            root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        return full.Equals(
                   root,
                   comparison) ||
               full.StartsWith(
                   prefix,
                   comparison)
            ? full
            : null;
    }

    private static IReadOnlyList<string> ResolveEvidenceFiles(
        string workspace,
        string artifactRoot,
        IReadOnlyList<string>? patterns)
    {
        if (patterns is null ||
            patterns.Count == 0)
        {
            return [];
        }

        var comparer =
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        var found =
            new HashSet<string>(comparer);

        foreach (var pattern in patterns.Take(100))
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            foreach (var root in new[]
                     {
                         workspace,
                         artifactRoot
                     })
            {
                try
                {
                    var directory =
                        Path.GetDirectoryName(pattern)?
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar)
                        ?? string.Empty;

                    var mask =
                        Path.GetFileName(pattern);

                    var baseDir =
                        ResolveWorkspacePath(
                            root,
                            string.IsNullOrWhiteSpace(directory)
                                ? "."
                                : directory);

                    if (baseDir is null ||
                        !Directory.Exists(baseDir))
                    {
                        continue;
                    }

                    foreach (var file in Directory
                                 .EnumerateFiles(
                                     baseDir,
                                     mask,
                                     SearchOption.TopDirectoryOnly)
                                 .Take(1000))
                    {
                        if (new FileInfo(file).Length > 0)
                        {
                            found.Add(file);
                        }
                    }
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }
        }

        return found.ToArray();
    }

    private static IEnumerable<string> EnumerateFiles(
        string root)
    {
        var ignored =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ".git",
                "bin",
                "obj",
                "node_modules",
                "Library",
                "Temp",
                "Logs",
                "Build",
                "build",
                "artifacts",
                ".dipqa"
            };

        var stack =
            new Stack<string>();

        stack.Push(root);

        while (stack.Count > 0)
        {
            var current =
                stack.Pop();

            string[] dirs;
            string[] files;

            try
            {
                dirs =
                    Directory.GetDirectories(current);

                files =
                    Directory.GetFiles(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var dir in dirs)
            {
                if (!ignored.Contains(
                        Path.GetFileName(dir)))
                {
                    stack.Push(dir);
                }
            }
        }
    }

    private static string Rule(
        string id)
    {
        return Regex.Replace(
                id.ToUpperInvariant(),
                "[^A-Z0-9]+",
                "-")
            .Trim('-');
    }

    private static string Sanitize(
        string text)
    {
        var value =
            Regex.Replace(
                text,
                "[^A-Za-z0-9._-]+",
                "_");

        return string.IsNullOrWhiteSpace(value)
            ? "default"
            : value[..Math.Min(
                80,
                value.Length)];
    }

    private static QaToolExecution Unavailable(
        string tool,
        string summary)
    {
        return new(
            tool,
            false,
            false,
            null,
            0,
            "",
            "",
            summary);
    }

    private static QaFindingCandidate F(
        string category,
        Severity severity,
        string title,
        string message,
        string file,
        string rule)
    {
        return new(
            category,
            severity,
            "dip-production-verification",
            title,
            message,
            file,
            RuleId: rule,
            SourceFormat:
                "dip-production-verification",
            SourceFormatVersion:
                "1.0");
    }

    private sealed record BuiltInAssessment(
        bool CanPassWithoutRuntime,
        IReadOnlyList<QaFindingCandidate> Findings);

    private sealed record ProductionVerificationManifest(
        VerificationContract? Localization = null,
        VerificationContract? Accessibility = null,
        VerificationContract? InputDeviceMatrix = null,
        VerificationContract? SaveCompatibility = null,
        VerificationContract? ConsoleCertification = null,
        VerificationContract? DeviceFarm = null,
        VerificationContract? ProfessionalGpuCapture = null,
        VerificationContract? SemanticGraphicsQa = null,
        VerificationContract? DistributedMultiplayer = null,
        VerificationContract? ContinuousVerification = null,
        VerificationContract? WindowsNativeRace = null,
        VerificationContract? AssetRepositoryQa = null,
        VerificationContract? ShaderQa = null);

    private sealed record VerificationContract(
        bool Enabled = false,
        string? Command = null,
        IReadOnlyList<string>? Arguments = null,
        IReadOnlyDictionary<string, string?>? Environment = null,
        IReadOnlyList<string>? Matrix = null,
        int? MinimumMatrixEntries = null,
        IReadOnlyList<string>? RequiredEvidenceFiles = null,
        int? MinimumEvidenceFiles = null,
        int? TimeoutSeconds = null,
        IReadOnlyList<string>? RequiredCultures = null,
        IReadOnlyList<string>? Fixtures = null,
        string? ChecklistPath = null,
        string? GpuCaptureReportPath = null,
        IReadOnlyList<string>? RequiredMarkers = null,
        string? SemanticScenePath = null,
        string? HeartbeatJournalPath = null,
        int? MinimumContinuousHours = null,
        int? MaximumHeartbeatGapMinutes = null);
}
