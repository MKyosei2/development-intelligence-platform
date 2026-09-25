// Purpose: Implements Game Replay Options infrastructure responsibilities for replay.
namespace Dip.Infrastructure.Replay;

public sealed class GameReplayOptions
{
    public const string SectionName = "GameReplay";
    public string WorkspaceRoot { get; set; } = ".";
    public bool AllowPathLookup { get; set; }
}
