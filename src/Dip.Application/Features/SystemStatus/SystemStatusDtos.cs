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
    DateTimeOffset CheckedAtUtc);

public sealed record SystemStatusDto(
    string Overall,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<SystemComponentStatusDto> Components);
