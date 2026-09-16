// Purpose: Represents G am eR ep la yS es si on in the Quality domain.
namespace Dip.Domain.Entities;

public sealed class GameReplaySession
{
    private GameReplaySession() { }
    public GameReplaySession(Guid id,Guid projectId,string key,string name,string scene,string inputArtifactId,int randomSeed,int durationSeconds,string launchCommand,string arguments,string? workingDirectory,string? baselineCommit,DateTimeOffset createdAtUtc)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        Id=id==Guid.Empty?Guid.NewGuid():id;ProjectId=projectId;Key=Required(key,120,nameof(key));Name=Required(name,200,nameof(name));Scene=Required(scene,300,nameof(scene));InputArtifactId=Required(inputArtifactId,1000,nameof(inputArtifactId));RandomSeed=randomSeed;DurationSeconds=Math.Clamp(durationSeconds,1,86_400);LaunchCommand=Required(launchCommand,1000,nameof(launchCommand));Arguments=Optional(arguments,4000)??string.Empty;WorkingDirectory=Optional(workingDirectory,1000);BaselineCommit=Optional(baselineCommit,64);CreatedAtUtc=createdAtUtc;Enabled=true;
    }
    public Guid Id{get;private set;} public Guid ProjectId{get;private set;} public string Key{get;private set;}=string.Empty; public string Name{get;private set;}=string.Empty; public string Scene{get;private set;}=string.Empty; public string InputArtifactId{get;private set;}=string.Empty; public int RandomSeed{get;private set;} public int DurationSeconds{get;private set;} public string LaunchCommand{get;private set;}=string.Empty; public string Arguments{get;private set;}=string.Empty; public string? WorkingDirectory{get;private set;} public string? BaselineCommit{get;private set;} public bool Enabled{get;private set;} public DateTimeOffset CreatedAtUtc{get;private set;}
    public void SetBaseline(string commit)=>BaselineCommit=Required(commit,64,nameof(commit)); public void SetEnabled(bool enabled)=>Enabled=enabled;
    private static string Required(string? value,int max,string name){var v=value?.Trim()??string.Empty;if(v.Length is <1||v.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);return v;} private static string? Optional(string? value,int max)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().Length<=max?value.Trim():value.Trim()[..max];
}
