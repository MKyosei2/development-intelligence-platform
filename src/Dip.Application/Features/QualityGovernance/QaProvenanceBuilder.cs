// Purpose: Builds deterministic QA provenance and compact toolchain evidence.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Domain.Entities;

namespace Dip.Application.Features.QualityGovernance;

public sealed class QaProvenanceBuilder
{
    public QaEvidenceProvenance Build(
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        string policyVersion,
        DateTimeOffset now)
    {
        var environment = new
        {
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            osArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            processorCount = Environment.ProcessorCount,
            machine = Environment.MachineName
        };

        var environmentJson = JsonSerializer.Serialize(environment);
        var input = JsonSerializer.Serialize(new
        {
            run.ProjectId,
            run.SubjectKey,
            run.Language,
            run.WorkspaceRelativePath,
            run.CommitSha,
            run.CreatedAtUtc
        });
        var evidence = JsonSerializer.Serialize(
            findings.OrderBy(x => x.Fingerprint).Select(x => new
            {
                x.Fingerprint,
                x.Severity,
                x.Tool,
                x.DiagnosticSchemaVersion,
                x.SourceFormat,
                x.SourceFormatVersion
            }));

        return new QaEvidenceProvenance(
            Guid.NewGuid(),
            run.ProjectId,
            run.Id,
            run.CommitSha,
            Hash(environmentJson),
            BuildToolchainEvidence(run.SummaryJson, environment, policyVersion),
            "2.0.0",
            policyVersion,
            Hash(input),
            Hash(evidence),
            now);
    }

    private static string BuildToolchainEvidence(string? summaryJson, object environment, string policyVersion)
    {
        var tools = new List<object>();
        var adapters = new List<object>();
        object? detected = null;

        if (!string.IsNullOrWhiteSpace(summaryJson))
        {
            try
            {
                using var document = JsonDocument.Parse(summaryJson);
                var root = document.RootElement;
                if (root.TryGetProperty("tools", out var toolArray)
                    && toolArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in toolArray.EnumerateArray().Take(100))
                    {
                        string? Read(string name) =>
                            tool.TryGetProperty(name, out var value)
                            && value.ValueKind == JsonValueKind.String
                                ? value.GetString()
                                : null;
                        long ReadLong(string name) =>
                            tool.TryGetProperty(name, out var number)
                            && number.TryGetInt64(out var value)
                                ? value
                                : 0;

                        tools.Add(new
                        {
                            tool = Read("Tool") ?? Read("tool"),
                            executable = Read("Executable") ?? Read("executable"),
                            version = Read("Version") ?? Read("version"),
                            sandbox = Read("SandboxJson") ?? Read("sandboxJson"),
                            cpuTimeMs = ReadLong("CpuTimeMs") + ReadLong("cpuTimeMs")
                        });
                    }
                }

                if (root.TryGetProperty("adapters", out var adapterArray)
                    && adapterArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var adapter in adapterArray.EnumerateArray().Take(50))
                        adapters.Add(JsonSerializer.Deserialize<object>(adapter.GetRawText())!);
                }

                if (root.TryGetProperty("detected", out var detectedValue))
                    detected = JsonSerializer.Deserialize<object>(detectedValue.GetRawText());
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    "QA summary JSON is invalid while building toolchain provenance.",
                    ex);
            }
        }

        var full = JsonSerializer.Serialize(new
        {
            environment,
            normalizerVersion = "2.0.0",
            governanceVersion = QaGovernanceCoreEngine.GovernanceVersion,
            policyVersion,
            detected,
            adapters,
            tools
        });
        if (full.Length <= 15900)
            return full;

        return JsonSerializer.Serialize(new
        {
            environment,
            normalizerVersion = "2.0.0",
            governanceVersion = QaGovernanceCoreEngine.GovernanceVersion,
            policyVersion,
            toolCount = tools.Count,
            note = "Toolchain evidence was compacted to fit the durable provenance record; immutable QA artifacts contain complete stdout/stderr and run summary."
        });
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
