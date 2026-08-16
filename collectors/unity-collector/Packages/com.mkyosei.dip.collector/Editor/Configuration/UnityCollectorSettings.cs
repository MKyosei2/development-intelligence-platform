using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Configuration
{
    public sealed class UnityCollectorSettings : ScriptableObject
    {
        [Header("Asset QA")]
        public int maxTextureSize = 4096;
        public int maxMeshTriangles = 100000;
        public int maxMaterialsPerRenderer = 8;
        public bool requireLodForLargeMeshes = true;

        [Header("Scene / Animation QA")]
        public int maxSceneGameObjects = 50000;
        public int maxAnimationCurvesPerClip = 2000;
        public bool analyzeAllEnabledBuildScenes = true;
        public int maxDependencyFindings = 100;
        public int maxScriptHeuristicWarnings = 50;

        [Header("Runtime performance QA")]
        public double warningFrameTimeP95Ms = 16.6667;
        public double criticalFrameTimeP95Ms = 25.0;
        public double warningMonoMemoryMb = 2048.0;
        public string runtimePerformanceReportPath = "Library/DIP/dip-runtime-performance.json";

        public static UnityCollectorSettings LoadOrCreate()
        {
            const string path = "Assets/DipUnityCollectorSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<UnityCollectorSettings>(path);
            if (settings != null) return settings;

            settings = CreateInstance<UnityCollectorSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
