using System;
using Dip.UnityCollector.Editor.Analysis;
using Dip.UnityCollector.Editor.Configuration;
using Dip.UnityCollector.Editor.Models;
using Dip.UnityCollector.Editor.Reporting;
using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Cli
{
    public static class UnityCollectorCli
    {
        public static void Run()
        {
            var started = DateTimeOffset.UtcNow;
            var report = new CollectorReport
            {
                projectId = GetArg("-dipProjectId"),
                commitSha = GetArg("-dipCommitSha"),
                branch = GetArg("-dipBranch"),
                startedAt = started.ToString("O")
            };

            try
            {
                var settings = UnityCollectorSettings.LoadOrCreate();
                TextureAnalyzer.Analyze(report, settings.maxTextureSize);
                MeshAnalyzer.Analyze(report, settings.maxMeshTriangles);
                MaterialAnalyzer.Analyze(report, settings.maxMaterialsPerRenderer);
                ReferenceAnalyzer.AnalyzeOpenScenes(report);
                report.status = "succeeded";
            }
            catch (Exception ex)
            {
                report.status = "failed";
                report.issues.Add(new CollectorIssue
                {
                    category = "collector",
                    severity = "Critical",
                    title = "Unity collector failed",
                    description = ex.ToString(),
                    source = "UnityCollectorCli"
                });
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
            finally
            {
                report.finishedAt = DateTimeOffset.UtcNow.ToString("O");
                UnityCollectorReportWriter.Write(GetArg("-dipOutput"), report);
            }
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name)
                    return args[i + 1];

            throw new ArgumentException($"Missing required argument: {name}");
        }
    }
}
