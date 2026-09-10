using System;
using System.Globalization;
using System.Linq;
using Dip.UnityCollector.Editor.Configuration;
using Dip.UnityCollector.Editor.Models;
using Dip.UnityCollector.Editor.Reporting;
using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Cli
{
    /// <summary>
    /// Exact-revision entry point used by portfolio/runtime evidence.
    ///
    /// Unlike the legacy compatibility CLI, this command requires the run identity
    /// fields used by the DIP CollectorReport contract. It never invents a commit,
    /// repository, or external run identifier and it uses the API status vocabulary:
    /// Succeeded / PartialSuccess / Failed.
    /// </summary>
    public static class UnityCollectorExactRevisionCli
    {
        private const string CollectorVersion = "0.5.0";

        public static void Run()
        {
            var started = DateTimeOffset.UtcNow;
            var output = GetRequiredArg("-dipOutput");
            var qualityGate = GetOptionalArg("-dipQualityGate") ?? "critical";

            var report = new CollectorReport
            {
                collectorVersion = CollectorVersion,
                externalRunId = GetRequiredArg("-dipExternalRunId"),
                attemptNumber = GetPositiveIntArg("-dipAttemptNumber", 1),
                projectId = GetRequiredArg("-dipProjectId"),
                repository = GetRequiredArg("-dipRepository"),
                commitSha = GetRequiredArg("-dipCommitSha"),
                branch = GetRequiredArg("-dipBranch"),
                startedAt = started.ToString("O", CultureInfo.InvariantCulture)
            };

            var exitCode = 0;
            try
            {
                var settings = UnityCollectorSettings.LoadOrCreate();
                UnityCollectorCli.AnalyzeProject(
                    report,
                    settings,
                    GetOptionalArg("-dipRuntimeMetrics"));

                AddIssueSummaryMetrics(report);
                var gateFailed = UnityCollectorCli.ShouldFailQualityGate(report, qualityGate);

                report.status = gateFailed
                    ? "Failed"
                    : report.issues.Count > 0
                        ? "PartialSuccess"
                        : "Succeeded";

                if (gateFailed)
                {
                    exitCode = 2;
                    Debug.LogError(
                        $"DIP Unity exact-revision quality gate '{qualityGate}' failed. " +
                        "The report is still written as engine evidence, but release quality is not PASS.");
                }
            }
            catch (Exception ex)
            {
                exitCode = 1;
                report.status = "Failed";
                report.issues.Add(new CollectorIssue
                {
                    category = "collector",
                    severity = "Critical",
                    title = "Unity exact-revision collector failed",
                    description = ex.ToString(),
                    source = "UnityCollectorExactRevisionCli",
                    ruleId = "UNITY_EXACT_REVISION_COLLECTOR_FAILED"
                });
                Debug.LogException(ex);
            }
            finally
            {
                report.finishedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                UnityCollectorReportWriter.Write(output, report);
            }

            if (exitCode != 0)
                EditorApplication.Exit(exitCode);
        }

        private static void AddIssueSummaryMetrics(CollectorReport report)
        {
            report.metrics.Add(new CollectorMetric
            {
                name = "collector_issue_critical_count",
                value = report.issues.Count(issue =>
                    string.Equals(issue.severity, "Critical", StringComparison.OrdinalIgnoreCase)),
                unit = "count"
            });
            report.metrics.Add(new CollectorMetric
            {
                name = "collector_issue_warning_count",
                value = report.issues.Count(issue =>
                    string.Equals(issue.severity, "Warning", StringComparison.OrdinalIgnoreCase)),
                unit = "count"
            });
        }

        private static int GetPositiveIntArg(string name, int defaultValue)
        {
            var raw = GetOptionalArg(name);
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1)
                throw new ArgumentException($"{name} must be a positive integer.");
            return value;
        }

        private static string GetRequiredArg(string name)
        {
            var value = GetOptionalArg(name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            throw new ArgumentException($"Missing required argument: {name}");
        }

        private static string GetOptionalArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            }
            return null;
        }
    }
}
