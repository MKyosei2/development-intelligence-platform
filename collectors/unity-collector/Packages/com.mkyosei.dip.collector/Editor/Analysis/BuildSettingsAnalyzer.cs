using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dip.UnityCollector.Editor.Models;
using UnityEditor;

namespace Dip.UnityCollector.Editor.Analysis
{
    public static class BuildSettingsAnalyzer
    {
        public static void Analyze(CollectorReport report)
        {
            var enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            report.metrics.Add(new CollectorMetric
            {
                name = "build_scene_count",
                value = enabled.Length,
                unit = "count"
            });
            report.metrics.Add(new CollectorMetric
            {
                name = "development_build_enabled",
                value = EditorUserBuildSettings.development ? 1 : 0,
                unit = "bool"
            });

            if (enabled.Length == 0)
            {
                report.issues.Add(new CollectorIssue
                {
                    category = "build",
                    severity = "Critical",
                    title = "No enabled build scenes",
                    description = "EditorBuildSettings contains no enabled scene. A player build would have no normal entry scene.",
                    source = "ProjectSettings/EditorBuildSettings.asset"
                });
                return;
            }

            var duplicatePaths = enabled
                .GroupBy(scene => scene.path, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            foreach (var duplicate in duplicatePaths)
            {
                report.issues.Add(new CollectorIssue
                {
                    category = "build",
                    severity = "Warning",
                    title = "Duplicate enabled build scene",
                    description = $"The scene is enabled more than once in build settings: {duplicate}",
                    source = duplicate
                });
            }

            foreach (var scene in enabled)
            {
                if (string.IsNullOrWhiteSpace(scene.path) || !File.Exists(scene.path))
                {
                    report.issues.Add(new CollectorIssue
                    {
                        category = "build",
                        severity = "Critical",
                        title = "Build scene is missing",
                        description = $"The enabled build scene cannot be found: {scene.path}",
                        source = scene.path
                    });
                }
            }

            var first = enabled[0].path;
            if (string.IsNullOrWhiteSpace(first))
            {
                report.issues.Add(new CollectorIssue
                {
                    category = "build",
                    severity = "Critical",
                    title = "Invalid first build scene",
                    description = "The first enabled build scene has an empty path.",
                    source = "ProjectSettings/EditorBuildSettings.asset"
                });
            }
        }
    }
}
