using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.Configuration
{
    public sealed class UnityCollectorSettings : ScriptableObject
    {
        public int maxTextureSize = 4096;
        public int maxMeshTriangles = 100000;
        public int maxMaterialsPerRenderer = 8;
        public bool requireLodForLargeMeshes = true;

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
