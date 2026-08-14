using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace Dip.UnityCollector.Runtime
{
    /// <summary>
    /// Opt-in runtime bridge for DIP deterministic replay jobs. It auto-starts only when
    /// DIP_REPLAY_MODE=1 is present, seeds UnityEngine.Random, replays semantic input events,
    /// emits machine-readable metrics, and terminates the standalone build after the requested duration.
    /// Game code subscribes to InputEventObserved to map semantic actions to its own input layer.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class ReplayProtocolProbe : MonoBehaviour
    {
        [Serializable] public sealed class ReplayInputEvent { public float timeSeconds; public string action = string.Empty; public string value = string.Empty; }
        [Serializable] private sealed class ReplayTrace { public ReplayInputEvent[] events = Array.Empty<ReplayInputEvent>(); }
        public static event Action<ReplayInputEvent>? InputEventObserved;
        private ReplayInputEvent[] _events = Array.Empty<ReplayInputEvent>();
        private int _nextEvent;
        private float _startedAt;
        private int _durationSeconds;
        private int _sampleCounter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DIP_REPLAY_MODE"), "1", StringComparison.Ordinal)) return;
            if (FindObjectOfType<ReplayProtocolProbe>() != null) return;
            var go = new GameObject("DIP Replay Protocol"); DontDestroyOnLoad(go); go.AddComponent<ReplayProtocolProbe>();
        }

        private void Awake()
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("DIP_REPLAY_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
                UnityEngine.Random.InitState(seed);
            _durationSeconds = int.TryParse(Environment.GetEnvironmentVariable("DIP_REPLAY_DURATION_SECONDS"), out var duration) ? Math.Max(1, duration) : 60;
            _events = LoadEvents(Environment.GetEnvironmentVariable("DIP_REPLAY_INPUT_ARTIFACT"));
            Array.Sort(_events, (a,b) => a.timeSeconds.CompareTo(b.timeSeconds));
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            var elapsed = Time.realtimeSinceStartup - _startedAt;
            while (_nextEvent < _events.Length && _events[_nextEvent].timeSeconds <= elapsed)
                InputEventObserved?.Invoke(_events[_nextEvent++]);

            var frameMs = Time.unscaledDeltaTime * 1000d;
            Emit("frame_time_ms", frameMs, "ms");
            if (Time.unscaledDeltaTime > 0) Emit("fps", 1d / Time.unscaledDeltaTime, "fps");
            if (++_sampleCounter % 30 == 0)
            {
                Emit("memory_mb", Profiler.GetTotalAllocatedMemoryLong() / 1024d / 1024d, "MB");
                Emit("mono_used_mb", Profiler.GetMonoUsedSizeLong() / 1024d / 1024d, "MB");
            }
            if (elapsed >= _durationSeconds)
                Application.Quit(0);
        }

        private static ReplayInputEvent[] LoadEvents(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Array.Empty<ReplayInputEvent>();
            try
            {
                var json = File.ReadAllText(path);
                var trace = JsonUtility.FromJson<ReplayTrace>(json);
                return trace?.events ?? Array.Empty<ReplayInputEvent>();
            }
            catch (Exception ex) { Debug.LogWarning($"DIP replay input could not be loaded: {ex.Message}"); return Array.Empty<ReplayInputEvent>(); }
        }

        private static void Emit(string name, double value, string unit)
        {
            var line = "DIP_REPLAY_METRIC {\"name\":\"" + name + "\",\"value\":" + value.ToString("R", CultureInfo.InvariantCulture) + ",\"unit\":\"" + unit + "\"}";
            Console.WriteLine(line);
        }
    }
}
