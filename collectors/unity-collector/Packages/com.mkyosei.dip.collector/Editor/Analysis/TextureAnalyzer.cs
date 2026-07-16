using Dip.UnityCollector.Editor.Models;
using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Analysis
{
    public static class TextureAnalyzer
    {
        public static void Analyze(CollectorReport report, int maxTextureSize)
        {
            var guids = AssetDatabase.FindAssets("t:Texture");
            long estimatedBytes = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture == null) continue;

                estimatedBytes += (long)texture.width * texture.height * 4L;

                if (texture.width > maxTextureSize || texture.height > maxTextureSize)
                {
                    report.issues.Add(new CollectorIssue
                    {
                        category = "asset",
                        severity = "Warning",
                        title = "Oversized texture",
                        description = $"{texture.width}x{texture.height} exceeds {maxTextureSize}.",
                        source = path
                    });
                }
            }

            report.metrics.Add(new CollectorMetric
            {
                name = "texture_count",
                value = guids.Length,
                unit = "count"
            });
            report.metrics.Add(new CollectorMetric
            {
                name = "estimated_texture_memory_mb",
                value = estimatedBytes / 1024d / 1024d,
                unit = "MB"
            });
        }
    }
}
