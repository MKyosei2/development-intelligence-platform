namespace Dip.Domain.Entities;

public sealed class AdaptiveQaPlanSnapshot
{
    private AdaptiveQaPlanSnapshot() { }
    public AdaptiveQaPlanSnapshot(Guid id,Guid projectId,string changeSetHash,decimal riskScore,decimal estimatedMinutes,string planJson,DateTimeOffset createdAtUtc)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        Id=id==Guid.Empty?Guid.NewGuid():id;ProjectId=projectId;
        ChangeSetHash=Required(changeSetHash,64,nameof(changeSetHash));
        RiskScore=Math.Clamp(riskScore,0m,100m);
        EstimatedMinutes=estimatedMinutes<0?throw new ArgumentOutOfRangeException(nameof(estimatedMinutes)):decimal.Round(estimatedMinutes,2);
        PlanJson=Required(planJson,32000,nameof(planJson));CreatedAtUtc=createdAtUtc;
    }
    public Guid Id{get;private set;}
    public Guid ProjectId{get;private set;}
    public string ChangeSetHash{get;private set;}=string.Empty;
    public decimal RiskScore{get;private set;}
    public decimal EstimatedMinutes{get;private set;}
    public string PlanJson{get;private set;}=string.Empty;
    public DateTimeOffset CreatedAtUtc{get;private set;}
    private static string Required(string? value,int max,string name){var text=value?.Trim()??string.Empty;if(text.Length is <1||text.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);return text;}
}
