using System;
using System.Linq;
using Dip.UnityCollector.Editor.Models;
using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Analysis
{
    public static class AnimationAnalyzer
    {
        public static void Analyze(CollectorReport report, int maxCurvesPerClip)
        {
            var guids = AssetDatabase.FindAssets("t:AnimationClip");
            long totalCurves = 0;
            long totalEvents = 0;
            var legacyClips = 0;
            var emptyClips = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                var floatCurves = AnimationUtility.GetCurveBindings(clip).Length;
                var objectCurves = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                var curveCount = floatCurves + objectCurves;
                var events = AnimationUtility.GetAnimationEvents(clip);
                totalCurves += curveCount;
                totalEvents += events.Length;
                if (clip.legacy) legacyClips++;

                if (clip.length <= 0.0001f || curveCount == 0)
                {
                    emptyClips++;
                    report.issues.Add(new CollectorIssue
                    {
                        category = "animation",
                        severity = "Warning",
                        title = "Empty or zero-length animation clip",
                        description = $"{clip.name} has length={clip.length:0.####}s and {curveCount} animated curve(s).",
                        source = path
                    });
                }

                if (curveCount > maxCurvesPerClip)
                {
                    report.issues.Add(new CollectorIssue
                    {
                        category = "animation",
                        severity = "Warning",
                        title = "High animation curve count",
                        description = $"{clip.name} contains {curveCount} curves, exceeding the configured limit {maxCurvesPerClip}.",
                        source = path
                    });
                }

                var duplicateEvents = events
                    .GroupBy(e => $"{e.functionName}|{e.time:0.0000}", StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .ToArray();
                if (duplicateEvents.Length > 0)
                {
                    report.issues.Add(new CollectorIssue
                    {
                        category = "animation",
                        severity = "Warning",
                        title = "Duplicate animation events",
                        description = $"{clip.name} contains {duplicateEvents.Length} duplicated function/time event group(s).",
                        source = path
                    });
                }
            }

            report.metrics.Add(new CollectorMetric { name = "animation_clip_count", value = guids.Length, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "animation_curve_count", value = totalCurves, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "animation_event_count", value = totalEvents, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "legacy_animation_clip_count", value = legacyClips, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "empty_animation_clip_count", value = emptyClips, unit = "count" });
        }
    }
}
