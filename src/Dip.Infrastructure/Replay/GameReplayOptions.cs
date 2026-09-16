// Purpose: Provides the G am eR ep la yO pt io ns infrastructure implementation.
namespace Dip.Infrastructure.Replay;

public sealed class GameReplayOptions
{
    public const string SectionName = "GameReplay";
    public string WorkspaceRoot { get; set; } = ".";
    public bool AllowPathLookup { get; set; }
}
