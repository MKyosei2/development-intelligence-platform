// Purpose: Serializes measured Unreal QA counters into the bounded line-oriented contracts consumed by DIP runners.
#include "DIPQaTelemetry.h"

namespace
{
FString OptionalInt(const TCHAR* Key, const TOptional<int64>& Value)
{
    return Value.IsSet() && Value.GetValue() >= 0
        ? FString::Printf(TEXT(" %s=%lld"), Key, Value.GetValue())
        : FString();
}

FString SanitizeStateHash(const FString& Input)
{
    FString Value = Input.Left(256).TrimStartAndEnd();
    Value.ReplaceInline(TEXT(" "), TEXT("_"));
    Value.ReplaceInline(TEXT("\t"), TEXT("_"));
    Value.ReplaceInline(TEXT("="), TEXT("_"));
    return Value;
}
}

void FDIPQaTelemetry::EmitGpuMetric(
    int64 Frame,
    double GpuFrameMs,
    TOptional<int64> VramMb,
    TOptional<int64> DrawCalls,
    TOptional<int64> Triangles,
    TOptional<int64> ShaderVariants)
{
    if (Frame < 0 || !FMath::IsFinite(GpuFrameMs) || GpuFrameMs < 0.0)
    {
        UE_LOG(LogTemp, Warning, TEXT("DIP GPU telemetry rejected invalid frame/time input."));
        return;
    }

    const FString OptionalFields =
        OptionalInt(TEXT("vram_mb"), VramMb)
        + OptionalInt(TEXT("draw_calls"), DrawCalls)
        + OptionalInt(TEXT("triangles"), Triangles)
        + OptionalInt(TEXT("shader_variants"), ShaderVariants);
    UE_LOG(
        LogTemp,
        Display,
        TEXT("DIP_GPU_METRIC frame=%lld gpu_frame_ms=%.6f%s"),
        Frame,
        GpuFrameMs,
        *OptionalFields);
}

void FDIPQaTelemetry::EmitMultiplayerState(
    int32 Client,
    int64 Tick,
    const FString& StateHash,
    TOptional<double> RttMs,
    TOptional<double> PacketLossPercent,
    bool bNetworkImpairmentApplied)
{
    const FString Hash = SanitizeStateHash(StateHash);
    if (Client < 0 || Tick < 0 || Hash.IsEmpty())
    {
        UE_LOG(LogTemp, Warning, TEXT("DIP multiplayer telemetry rejected invalid identity/state input."));
        return;
    }
    if ((RttMs.IsSet() && (!FMath::IsFinite(RttMs.GetValue()) || RttMs.GetValue() < 0.0))
        || (PacketLossPercent.IsSet()
            && (!FMath::IsFinite(PacketLossPercent.GetValue())
                || PacketLossPercent.GetValue() < 0.0
                || PacketLossPercent.GetValue() > 100.0)))
    {
        UE_LOG(LogTemp, Warning, TEXT("DIP multiplayer telemetry rejected invalid network metrics."));
        return;
    }

    const FString Rtt = RttMs.IsSet()
        ? FString::Printf(TEXT(" rtt_ms=%.6f"), RttMs.GetValue())
        : FString();
    const FString Loss = PacketLossPercent.IsSet()
        ? FString::Printf(TEXT(" packet_loss_percent=%.6f"), PacketLossPercent.GetValue())
        : FString();
    UE_LOG(
        LogTemp,
        Display,
        TEXT("DIP_MP_STATE client=%d tick=%lld hash=%s%s%s network_impairment_applied=%d"),
        Client,
        Tick,
        *Hash,
        *Rtt,
        *Loss,
        bNetworkImpairmentApplied ? 1 : 0);
}
