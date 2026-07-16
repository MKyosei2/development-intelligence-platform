using Dip.UnityCollector.Editor.Models;
using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Analysis
{
    public static class MaterialAnalyzer
    {
        public static void Analyze(CollectorReport report, int maxMaterialsPerRenderer)
        {
            var materialGuids = AssetDatabase.FindAssets("t:Material");
            report.metrics.Add(new CollectorMetric
            {
                name = "material_count",
                value = materialGuids.Length,
                unit = "count"
            });

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.sharedMaterials.Length > maxMaterialsPerRenderer)
                {
                    report.issues.Add(new CollectorIssue
                    {
                        category = "asset",
                        severity = "Warning",
                        title = "Too many materials on renderer",
                        description = $"{renderer.sharedMaterials.Length} materials are assigned.",
                        source = renderer.gameObject.scene.path
                    });
                }
            }
        }
    }
}
