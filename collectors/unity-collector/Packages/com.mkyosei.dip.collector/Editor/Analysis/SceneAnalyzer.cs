using System;
using System.Linq;
using Dip.UnityCollector.Editor.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dip.UnityCollector.Editor.Analysis
{
    public static class SceneAnalyzer
    {
        public static void Analyze(CollectorReport report, int maxObjectsPerScene, bool analyzeAllBuildScenes)
        {
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var scenePaths = analyzeAllBuildScenes
                ? EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : Array.Empty<string>();

            long totalObjects = 0;
            long disabledObjects = 0;
            long missingScripts = 0;
            long missingSerializedReferences = 0;
            var analyzedScenes = 0;

            try
            {
                if (scenePaths.Length > 0)
                {
                    foreach (var path in scenePaths)
                    {
                        var alreadyLoaded = SceneManager.GetSceneByPath(path);
                        var scene = alreadyLoaded.IsValid() && alreadyLoaded.isLoaded
                            ? alreadyLoaded
                            : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                        AnalyzeScene(
                            scene,
                            report,
                            maxObjectsPerScene,
                            ref totalObjects,
                            ref disabledObjects,
                            ref missingScripts,
                            ref missingSerializedReferences);
                        analyzedScenes++;

                        if (!(alreadyLoaded.IsValid() && alreadyLoaded.isLoaded))
                            EditorSceneManager.CloseScene(scene, true);
                    }
                }
                else
                {
                    for (var i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (!scene.IsValid() || !scene.isLoaded)
                            continue;

                        AnalyzeScene(
                            scene,
                            report,
                            maxObjectsPerScene,
                            ref totalObjects,
                            ref disabledObjects,
                            ref missingScripts,
                            ref missingSerializedReferences);
                        analyzedScenes++;
                    }
                }
            }
            finally
            {
                try
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
                catch (Exception ex)
                {
                    report.issues.Add(new CollectorIssue
                    {
                        category = "scene",
                        severity = "Warning",
                        title = "Scene setup restoration failed",
                        description = ex.Message,
                        source = "EditorSceneManager",
                        ruleId = "unity.scene.restore-failed"
                    });
                }
            }

            report.metrics.Add(new CollectorMetric { name = "scene_count_analyzed", value = analyzedScenes, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "scene_game_object_count", value = totalObjects, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "disabled_game_object_count", value = disabledObjects, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "missing_script_count", value = missingScripts, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "missing_serialized_reference_count", value = missingSerializedReferences, unit = "count" });
            report.metrics.Add(new CollectorMetric { name = "missing_reference_count", value = missingScripts + missingSerializedReferences, unit = "count" });
        }

        private static void AnalyzeScene(
            Scene scene,
            CollectorReport report,
            int maxObjectsPerScene,
            ref long totalObjects,
            ref long disabledObjects,
            ref long missingScripts,
            ref long missingSerializedReferences)
        {
            var sceneObjects = 0L;
            foreach (var root in scene.GetRootGameObjects())
            {
                AnalyzeObject(
                    root,
                    scene.path,
                    report,
                    ref sceneObjects,
                    ref disabledObjects,
                    ref missingScripts,
                    ref missingSerializedReferences);
            }

            totalObjects += sceneObjects;
            if (sceneObjects > maxObjectsPerScene)
            {
                report.issues.Add(new CollectorIssue
                {
                    category = "scene",
                    severity = "Warning",
                    title = "High scene object count",
                    description = $"Scene contains {sceneObjects} GameObjects, exceeding the configured limit {maxObjectsPerScene}.",
                    source = scene.path,
                    ruleId = "unity.scene.object-count"
                });
            }
        }

        private static void AnalyzeObject(
            GameObject gameObject,
            string scenePath,
            CollectorReport report,
            ref long objectCount,
            ref long disabledObjects,
            ref long missingScripts,
            ref long missingSerializedReferences)
        {
            objectCount++;
            if (!gameObject.activeSelf)
                disabledObjects++;

            var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (missing > 0)
            {
                missingScripts += missing;
                report.issues.Add(new CollectorIssue
                {
                    category = "reference",
                    severity = "Critical",
                    title = "Missing MonoBehaviour script",
                    description = $"{gameObject.name} has {missing} missing script component(s).",
                    source = string.IsNullOrWhiteSpace(scenePath) ? gameObject.name : scenePath,
                    ruleId = "unity.scene.missing-script"
                });
            }

            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                    continue;

                try
                {
                    var serializedObject = new SerializedObject(component);
                    var property = serializedObject.GetIterator();
                    var enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyType != SerializedPropertyType.ObjectReference)
                            continue;
                        if (property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0)
                            continue;

                        missingSerializedReferences++;
                        report.issues.Add(new CollectorIssue
                        {
                            category = "reference",
                            severity = "Critical",
                            title = "Missing serialized object reference",
                            description = $"{gameObject.name}/{component.GetType().Name}.{property.propertyPath} references a missing Unity object.",
                            source = string.IsNullOrWhiteSpace(scenePath) ? gameObject.name : scenePath,
                            ruleId = "unity.scene.missing-reference"
                        });
                    }
                }
                catch (Exception ex)
                {
                    report.issues.Add(new CollectorIssue
                    {
                        category = "collector",
                        severity = "Info",
                        title = "Component serialization inspection skipped",
                        description = $"{component.GetType().FullName}: {ex.Message}",
                        source = scenePath,
                        ruleId = "unity.scene.serialization-inspection-skipped"
                    });
                }
            }

            foreach (Transform child in gameObject.transform)
            {
                AnalyzeObject(
                    child.gameObject,
                    scenePath,
                    report,
                    ref objectCount,
                    ref disabledObjects,
                    ref missingScripts,
                    ref missingSerializedReferences);
            }
        }
    }
}
