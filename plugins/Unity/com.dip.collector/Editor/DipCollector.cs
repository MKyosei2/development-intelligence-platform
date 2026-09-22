// Purpose: Defines a focused responsibility within the development intelligence platform.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DIP.Collector
{
    [Serializable] internal sealed class DipMetadata
    {
        public string buildTarget;
        public string buildResult;
        public double durationSeconds;
        public int totalErrors;
        public int totalWarnings;
        public string[] importedAssets;
        public double cpuMs;
        public double gpuMs;
        public double fps;
        public string assetType;
        public int textureWidth;
        public int textureHeight;
        public int missingScriptCount;
        public int lodGroupCount;
    }

    [Serializable] internal sealed class DipEvent
    {
        public string eventId;
        public int source = 2;
        public int kind;
        public string toolVersion;
        public string relativePath;
        public string artifactRelativePath;
        public string contentHash;
        public long sizeBytes;
        public string observedAtUtc;
        public DipMetadata metadata;
        public int mediaKind;
        public string mediaRelativePath;
        public string mediaContentType;
    }

    [Serializable] internal sealed class DipMessage
    {
        public string token;
        public DipEvent @event;
    }

    internal static class DipEmitter
    {
        public static void Emit(int kind, string relativePath = null, DipMetadata metadata = null,
            int mediaKind = 0, string mediaRelativePath = null, string mediaContentType = null)
        {
            var evt = new DipEvent
            {
                eventId = "unity:" + Guid.NewGuid().ToString("N"),
                kind = kind,
                toolVersion = Application.unityVersion,
                relativePath = Normalize(relativePath),
                artifactRelativePath = Normalize(relativePath),
                observedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                metadata = metadata ?? new DipMetadata(),
                mediaKind = mediaKind,
                mediaRelativePath = Normalize(mediaRelativePath),
                mediaContentType = mediaContentType
            };
            var message = new DipMessage
            {
                token = Environment.GetEnvironmentVariable("DIP_AGENT_BRIDGE_TOKEN"),
                @event = evt
            };
            DipNamedPipeClient.Send(JsonUtility.ToJson(message));
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var text = value.Replace('\\', '/');
            var project = Directory.GetParent(Application.dataPath)?.FullName?.Replace('\\', '/').TrimEnd('/');
            if (!string.IsNullOrEmpty(project) && text.StartsWith(project + "/", StringComparison.OrdinalIgnoreCase))
                return text.Substring(project.Length + 1);
            return text.TrimStart('/');
        }
    }

    public sealed class DipBuildCollector : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;
        public void OnPreprocessBuild(BuildReport report)
            => DipEmitter.Emit(20, metadata: new DipMetadata { buildTarget = report.summary.platform.ToString() });

        public void OnPostprocessBuild(BuildReport report)
            => DipEmitter.Emit(21, metadata: new DipMetadata
            {
                buildTarget = report.summary.platform.ToString(),
                buildResult = report.summary.result.ToString(),
                durationSeconds = report.summary.totalTime.TotalSeconds,
                totalErrors = report.summary.totalErrors,
                totalWarnings = report.summary.totalWarnings
            });
    }

    public sealed class DipAssetCollector : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported == null || imported.Length == 0) return;
            foreach (var path in imported)
                DipEmitter.Emit(22, path, BuildAssetMetadata(path));
        }

        private static DipMetadata BuildAssetMetadata(string path)
        {
            var metadata = new DipMetadata { importedAssets = new[] { path } };
            try
            {
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                metadata.assetType = type?.Name;
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset is Texture2D texture)
                {
                    metadata.textureWidth = texture.width;
                    metadata.textureHeight = texture.height;
                }
                if (asset is GameObject go)
                {
                    metadata.missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    metadata.lodGroupCount = go.GetComponentsInChildren<LODGroup>(true).Length;
                }
            }
            catch (Exception)
            {
                // Asset metadata is best-effort; event delivery must not block import.
            }
            return metadata;
        }
    }

    public static class DipMediaCollector
    {
        [MenuItem("DIP/Capture Screenshot")]
        public static void CaptureScreenshot()
        {
            var relative = "DIPCaptures/screenshot-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".png";
            var absolute = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute);
            EditorApplication.delayCall += () => WaitForScreenshot(absolute, relative, 0);
        }

        public static void RegisterVideo(string path)
            => DipEmitter.Emit(41, mediaKind: 1, mediaRelativePath: path, mediaContentType: "video/mp4");

        public static void EmitPerformance(double cpuMs, double gpuMs, double fps)
            => DipEmitter.Emit(24, metadata: new DipMetadata { cpuMs = cpuMs, gpuMs = gpuMs, fps = fps });

        private static void WaitForScreenshot(string absolute, string relative, int attempt)
        {
            if (File.Exists(absolute))
            {
                DipEmitter.Emit(40, mediaKind: 0, mediaRelativePath: relative, mediaContentType: "image/png");
                return;
            }
            if (attempt >= 20) return;
            EditorApplication.delayCall += () => WaitForScreenshot(absolute, relative, attempt + 1);
        }
    }
}
