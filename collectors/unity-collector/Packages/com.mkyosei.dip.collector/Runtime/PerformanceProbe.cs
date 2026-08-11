using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace Dip.UnityCollector.Runtime
{
    [DefaultExecutionOrder(10000)]
    public sealed class PerformanceProbe : MonoBehaviour
    {
        [Serializable]
        public sealed class Snapshot
        {
            public string schemaVersion = "1.0";
            public string capturedAt = string.Empty;
            public int sampleCount;
            public double averageFrameMs;
            public double p50FrameMs;
            public double p95FrameMs;
            public double p99FrameMs;
            public double maxFrameMs;
            public double maxMonoUsedMb;
            public double maxTotalAllocatedMb;
            public int gcCollectionDelta0;
            public int gcCollectionDelta1;
            public int gcCollectionDelta2;
        }

        [SerializeField] private int warmupFrames = 120;
        [SerializeField] private int maxSamples = 10000;
        [SerializeField] private bool writeOnApplicationQuit = true;
        [SerializeField] private string outputFileName = "dip-runtime-performance.json";

        private readonly List<float> _frameMilliseconds = new(10000);
        private int _framesSeen;
        private long _maxMonoUsed;
        private long _maxTotalAllocated;
        private readonly int[] _gcBaseline = new int[3];

        private void OnEnable()
        {
            _frameMilliseconds.Clear();
            _framesSeen = 0;
            _maxMonoUsed = 0;
            _maxTotalAllocated = 0;
            for (var generation = 0; generation < 3; generation++)
                _gcBaseline[generation] = GC.CollectionCount(generation);
        }

        private void Update()
        {
            _framesSeen++;
            if (_framesSeen <= Math.Max(0, warmupFrames)) return;
            if (_frameMilliseconds.Count >= Math.Max(1, maxSamples)) return;

            _frameMilliseconds.Add(Time.unscaledDeltaTime * 1000f);
            _maxMonoUsed = Math.Max(_maxMonoUsed, Profiler.GetMonoUsedSizeLong());
            _maxTotalAllocated = Math.Max(_maxTotalAllocated, Profiler.GetTotalAllocatedMemoryLong());
        }

        private void OnApplicationQuit()
        {
            if (writeOnApplicationQuit)
                WriteSnapshot(ResolveOutputPath());
        }

        public Snapshot CreateSnapshot()
        {
            var values = _frameMilliseconds.Select(value => (double)value).OrderBy(value => value).ToArray();
            return new Snapshot
            {
                capturedAt = DateTimeOffset.UtcNow.ToString("O"),
                sampleCount = values.Length,
                averageFrameMs = values.Length == 0 ? 0 : values.Average(),
                p50FrameMs = Percentile(values, 0.50),
                p95FrameMs = Percentile(values, 0.95),
                p99FrameMs = Percentile(values, 0.99),
                maxFrameMs = values.Length == 0 ? 0 : values[^1],
                maxMonoUsedMb = _maxMonoUsed / 1024d / 1024d,
                maxTotalAllocatedMb = _maxTotalAllocated / 1024d / 1024d,
                gcCollectionDelta0 = GC.CollectionCount(0) - _gcBaseline[0],
                gcCollectionDelta1 = GC.CollectionCount(1) - _gcBaseline[1],
                gcCollectionDelta2 = GC.CollectionCount(2) - _gcBaseline[2]
            };
        }

        public void WriteSnapshot(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, JsonUtility.ToJson(CreateSnapshot(), true));
        }

        public static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0) return 0;
            var clamped = Math.Max(0, Math.Min(1, percentile));
            var position = clamped * (sortedValues.Count - 1);
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            if (lower == upper) return sortedValues[lower];
            var fraction = position - lower;
            return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * fraction;
        }

        private string ResolveOutputPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == "-dipRuntimeOutput")
                    return args[i + 1];
            return Path.Combine(Application.persistentDataPath, outputFileName);
        }
    }
}
