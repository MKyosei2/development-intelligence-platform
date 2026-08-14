using System.Linq;
using Dip.UnityCollector.Editor.Analysis;
using Dip.UnityCollector.Editor.Models;
using NUnit.Framework;

namespace Dip.UnityCollector.Editor.Tests
{
    public sealed class AnimationAnalyzerTests
    {
        [Test]
        public void Analyze_AlwaysEmitsAnimationMetrics()
        {
            var report = new CollectorReport();
            AnimationAnalyzer.Analyze(report, 2000);
            Assert.That(report.metrics.Any(metric => metric.name == "animation_clip_count"), Is.True);
            Assert.That(report.metrics.Any(metric => metric.name == "animation_curve_count"), Is.True);
        }
    }
}
