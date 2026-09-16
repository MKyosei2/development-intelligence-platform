// Purpose: Represents R ep ro du ct io nA tt em pt in the Production domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class ReproductionAttempt
{
    private ReproductionAttempt() { }

    public ReproductionAttempt(
        Guid id,
        Guid projectId,
        Guid qaFindingId,
        int attemptNumber,
        string artifactDirectory,
        DateTimeOffset startedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (qaFindingId == Guid.Empty) throw new ArgumentException("QaFindingId is required.", nameof(qaFindingId));
        if (attemptNumber < 1) throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        QaFindingId = qaFindingId;
        AttemptNumber = attemptNumber;
        ArtifactDirectory = Required(artifactDirectory, 1000, nameof(artifactDirectory));
        Status = ReproductionStatus.Preparing;
        StartedAtUtc = startedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid QaFindingId { get; private set; }
    public int AttemptNumber { get; private set; }
    public ReproductionStatus Status { get; private set; }
    public bool Reproduced { get; private set; }
    public bool Crashed { get; private set; }
    public bool TimedOut { get; private set; }
    public int? ExitCode { get; private set; }
    public long DurationMs { get; private set; }
    public string ArtifactDirectory { get; private set; } = string.Empty;
    public string? VideoPath { get; private set; }
    public string? AudioPath { get; private set; }
    public string? DumpPath { get; private set; }
    public string? ResultJson { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public void Complete(
        ReproductionStatus status,
        bool reproduced,
        bool crashed,
        bool timedOut,
        int? exitCode,
        long durationMs,
        string? videoPath,
        string? audioPath,
        string? dumpPath,
        string resultJson,
        DateTimeOffset at)
    {
        Status = status;
        Reproduced = reproduced;
        Crashed = crashed;
        TimedOut = timedOut;
        ExitCode = exitCode;
        DurationMs = Math.Max(0, durationMs);
        VideoPath = Optional(videoPath, 1000, nameof(videoPath));
        AudioPath = Optional(audioPath, 1000, nameof(audioPath));
        DumpPath = Optional(dumpPath, 1000, nameof(dumpPath));
        ResultJson = Optional(resultJson, 32000, nameof(resultJson));
        FinishedAtUtc = at;
    }

    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }

    private static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.Length > max) throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return text;
    }
}
