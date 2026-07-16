using Dip.UnityCollector.Editor.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dip.UnityCollector.Editor.Analysis
{
    public static class ReferenceAnalyzer
    {
        public static void AnalyzeOpenScenes(CollectorReport report)
        {
            var missingCount = 0;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                foreach (var root in scene.GetRootGameObjects())
                {
                    missingCount += AnalyzeObject(root, report);
                }
            }

            report.metrics.Add(new CollectorMetric
            {
                name = "missing_reference_count",
                value = missingCount,
                unit = "count"
            });
        }

        private static int AnalyzeObject(GameObject gameObject, CollectorReport report)
        {
            var count = 0;
            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    count++;
                    report.issues.Add(new CollectorIssue
                    {
                        category = "reference",
                        severity = "Critical",
                        title = "Missing component reference",
                        description = $"Missing component on {gameObject.name}.",
                        source = gameObject.scene.path
                    });
                }
            }

            foreach (Transform child in gameObject.transform)
                count += AnalyzeObject(child.gameObject, report);

            return count;
        }
    }
}
