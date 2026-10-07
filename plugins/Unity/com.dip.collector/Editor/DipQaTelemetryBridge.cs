// Purpose: Emits standardized DIP GPU and multiplayer telemetry from Unity test/game harnesses without inventing measurements inside the collector.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DIP.Collector
{
    public static class DipQaTelemetryBridge
    {
        public const string GpuMetricPrefix = "DIP_GPU_METRIC";
        public const string MultiplayerStatePrefix = "DIP_MP_STATE";

        public static void EmitGpuMetric(
            long frame,
            double gpuFrameMs,
            long? vramMb = null,
            long? drawCalls = null,
            long? triangles = null,
            long? shaderVariants = null)
        {
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame));
            if (double.IsNaN(gpuFrameMs) || double.IsInfinity(gpuFrameMs) || gpuFrameMs < 0d)
                throw new ArgumentOutOfRangeException(nameof(gpuFrameMs));
            ValidateNonNegative(vramMb, nameof(vramMb));
            ValidateNonNegative(drawCalls, nameof(drawCalls));
            ValidateNonNegative(triangles, nameof(triangles));
            ValidateNonNegative(shaderVariants, nameof(shaderVariants));

            var fields = new List<string>
            {
                GpuMetricPrefix,
                "frame=" + frame.ToString(CultureInfo.InvariantCulture),
                "gpu_frame_ms=" + gpuFrameMs.ToString("0.######", CultureInfo.InvariantCulture)
            };
            Add(fields, "vram_mb", vramMb);
            Add(fields, "draw_calls", drawCalls);
            Add(fields, "triangles", triangles);
            Add(fields, "shader_variants", shaderVariants);
            Debug.Log(string.Join(" ", fields));
        }

        public static void EmitMultiplayerState(
            int client,
            long tick,
            string stateHash,
            double? rttMs = null,
            double? packetLossPercent = null,
            bool networkImpairmentApplied = false)
        {
            if (client < 0)
                throw new ArgumentOutOfRangeException(nameof(client));
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick));
            if (string.IsNullOrWhiteSpace(stateHash))
                throw new ArgumentException("A stable state hash is required.", nameof(stateHash));
            if (rttMs.HasValue && (double.IsNaN(rttMs.Value) || double.IsInfinity(rttMs.Value) || rttMs.Value < 0d))
                throw new ArgumentOutOfRangeException(nameof(rttMs));
            if (packetLossPercent.HasValue
                && (double.IsNaN(packetLossPercent.Value)
                    || double.IsInfinity(packetLossPercent.Value)
                    || packetLossPercent.Value < 0d
                    || packetLossPercent.Value > 100d))
                throw new ArgumentOutOfRangeException(nameof(packetLossPercent));

            var normalizedHash = new string(stateHash.Trim()
                .Take(256)
                .Select(ch => char.IsWhiteSpace(ch) || ch == '=' ? '_' : ch)
                .ToArray());
            var fields = new List<string>
            {
                MultiplayerStatePrefix,
                "client=" + client.ToString(CultureInfo.InvariantCulture),
                "tick=" + tick.ToString(CultureInfo.InvariantCulture),
                "hash=" + normalizedHash
            };
            if (rttMs.HasValue)
                fields.Add("rtt_ms=" + rttMs.Value.ToString("0.######", CultureInfo.InvariantCulture));
            if (packetLossPercent.HasValue)
                fields.Add("packet_loss_percent=" + packetLossPercent.Value.ToString("0.######", CultureInfo.InvariantCulture));
            fields.Add("network_impairment_applied=" + (networkImpairmentApplied ? "1" : "0"));
            Debug.Log(string.Join(" ", fields));
        }

        private static void ValidateNonNegative(long? value, string name)
        {
            if (value.HasValue && value.Value < 0)
                throw new ArgumentOutOfRangeException(name);
        }

        private static void Add(ICollection<string> fields, string key, long? value)
        {
            if (value.HasValue)
                fields.Add(key + "=" + value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
