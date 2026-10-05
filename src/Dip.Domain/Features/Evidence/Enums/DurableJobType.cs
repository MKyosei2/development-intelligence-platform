// Purpose: Defines DurableJobType values.
namespace Dip.Domain.Enums;

// Keep new values appended so existing integer-backed persistence remains stable.
public enum DurableJobType
{
    Fuzz,
    Mutation,
    Soak,
    GitBisect,
    MediaRegression,
    Benchmark,
    GameSessionReplay,
    ReleaseEvidence,
    AutonomousQaSession
}
