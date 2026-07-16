using Dip.UnityCollector.Editor.Models;
using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Analysis
{
    public static class MeshAnalyzer
    {
        public static void Analyze(CollectorReport report, int maxTriangles)
        {
            var guids = AssetDatabase.FindAssets("t:Mesh");
            long totalTriangles = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) continue;

                var triangles = mesh.triangles.Length / 3;
                totalTriangles += triangles;

                if (triangles > maxTriangles)
                {
                    report.issues.Add(new CollectorIssue
                    {
                        category = "asset",
                        severity = "Warning",
                        title = "High triangle mesh",
                        description = $"{triangles} triangles exceeds {maxTriangles}.",
                        source = path
                    });
                }
            }

            report.metrics.Add(new CollectorMetric
            {
                name = "mesh_count",
                value = guids.Length,
                unit = "count"
            });
            report.metrics.Add(new CollectorMetric
            {
                name = "total_mesh_triangles",
                value = totalTriangles,
                unit = "triangles"
            });
        }
    }
}
