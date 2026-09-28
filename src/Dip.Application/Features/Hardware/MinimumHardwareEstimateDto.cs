// Purpose: Defines the MinimumHardwareEstimateDto application contract.
namespace Dip.Application.Features.Hardware;

public sealed record MinimumHardwareEstimateDto(decimal EstimatedCpuPercent, decimal EstimatedGpuPercent, decimal ConfidencePercent, string Status, IReadOnlyList<string> TestedPoints, string EvidenceNote)
{
    public decimal EstimatedMemoryBandwidthPercent
    {
        get;
        init;
    }
    public decimal EstimatedStoragePercent
    {
        get;
        init;
    }
    public long EstimatedMemoryBudgetMb
    {
        get;
        init;
    }
    public decimal RecommendedCpuPercent
    {
        get;
        init;
    }
    public decimal RecommendedGpuPercent
    {
        get;
        init;
    }
    public decimal RecommendedMemoryBandwidthPercent
    {
        get;
        init;
    }
    public decimal RecommendedStoragePercent
    {
        get;
        init;
    }
    public long RecommendedMemoryBudgetMb
    {
        get;
        init;
    }
}
