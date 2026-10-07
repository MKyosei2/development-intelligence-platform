// Purpose: Exposes a minimal Unreal-side producer for DIP GPU and multiplayer QA telemetry contracts.
#pragma once

#include "CoreMinimal.h"

class DIPCOLLECTOR_API FDIPQaTelemetry
{
public:
    static void EmitGpuMetric(
        int64 Frame,
        double GpuFrameMs,
        TOptional<int64> VramMb = TOptional<int64>(),
        TOptional<int64> DrawCalls = TOptional<int64>(),
        TOptional<int64> Triangles = TOptional<int64>(),
        TOptional<int64> ShaderVariants = TOptional<int64>());

    static void EmitMultiplayerState(
        int32 Client,
        int64 Tick,
        const FString& StateHash,
        TOptional<double> RttMs = TOptional<double>(),
        TOptional<double> PacketLossPercent = TOptional<double>(),
        bool bNetworkImpairmentApplied = false);
};
