using Dip.UnityCollector.Editor.Cli;
using UnityEditor;
using UnityEngine;

namespace Dip.UnityCollector.Editor.UI
{
    public sealed class UnityCollectorWindow : EditorWindow
    {
        [MenuItem("Tools/DIP/Unity Collector")]
        public static void Open() => GetWindow<UnityCollectorWindow>("DIP Collector");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Development Intelligence Platform", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "CIではUnityCollectorCli.RunをBatchModeから実行してください。",
                MessageType.Info);

            if (GUILayout.Button("Open Settings"))
                Selection.activeObject = Configuration.UnityCollectorSettings.LoadOrCreate();
        }
    }
}
