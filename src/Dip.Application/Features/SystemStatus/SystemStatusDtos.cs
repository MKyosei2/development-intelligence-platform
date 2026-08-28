namespace Dip.Application.Features.SystemStatus;

public static class SystemComponentStates
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Unavailable = "Unavailable";
    public const string NotConfigured = "NotConfigured";
}

public sealed record SystemComponentStatusDto(
    string Key,
    string Name,
    string State,
    string Detail,
    DateTimeOffset CheckedAtUtc,
    double? DurationMs = null);

public sealed record SystemStatusDto(
    string Overall,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<SystemComponentStatusDto> Components);

public sealed record SystemLivenessDto(
    bool Live,
    string State,
    DateTimeOffset CheckedAtUtc);

public sealed record SystemReadinessDto(
    bool Ready,
    string State,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<string> UnavailableComponents,
    IReadOnlyList<string> DegradedComponents);
