using System.Linq;
using Dip.UnityCollector.Editor.Analysis;
using Dip.UnityCollector.Editor.Models;
using NUnit.Framework;
using UnityEditor;

namespace Dip.UnityCollector.Editor.Tests
{
    public sealed class BuildSettingsAnalyzerTests
    {
        [Test]
        public void Analyze_NoEnabledScenesProducesCriticalFinding()
        {
            var previous = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];
                var report = new CollectorReport();
                BuildSettingsAnalyzer.Analyze(report);
                Assert.That(report.metrics.Any(metric => metric.name == "build_scene_count" && metric.value == 0), Is.True);
                Assert.That(report.issues.Any(issue => issue.category == "build" && issue.severity == "Critical"), Is.True);
            }
            finally
            {
                EditorBuildSettings.scenes = previous;
            }
        }
    }
}
