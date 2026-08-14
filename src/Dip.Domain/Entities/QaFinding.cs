using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Domain.Entities;

public sealed class QaFinding
{
    private QaFinding() { }

    public QaFinding(
        Guid id,
        Guid projectId,
        Guid qaRunId,
        string subjectKey,
        LanguageId language,
        string category,
        Severity severity,
        string tool,
        string title,
        string message,
        string fingerprint,
        string? filePath,
        int? line,
        int? column,
        string? function,
        string? exceptionType,
        string? stackTrace,
        string? ruleId,
        DateTimeOffset observedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (qaRunId == Guid.Empty) throw new ArgumentException("QaRunId is required.", nameof(qaRunId));
        if (string.IsNullOrWhiteSpace(language.Value)) throw new ArgumentException("Language is required.", nameof(language));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        QaRunId = qaRunId;
        SubjectKey = Required(subjectKey, 200, nameof(subjectKey));
        Language = language;
        Category = Required(category, 120, nameof(category));
        Severity = severity;
        Tool = Required(tool, 120, nameof(tool));
        Title = Required(title, 240, nameof(title));
        Message = Required(message, 8000, nameof(message));
        Fingerprint = Required(fingerprint, 64, nameof(fingerprint));
        FilePath = Optional(filePath, 1000, nameof(filePath));
        Line = line is > 0 ? line : null;
        Column = column is > 0 ? column : null;
        Function = Optional(function, 500, nameof(function));
        ExceptionType = Optional(exceptionType, 300, nameof(exceptionType));
        StackTrace = Optional(stackTrace, 32000, nameof(stackTrace));
        RuleId = Optional(ruleId, 300, nameof(ruleId));
        ReproductionStatus = ReproductionStatus.NotRequested;
        ObservedAtUtc = observedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid QaRunId { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public LanguageId Language { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public Severity Severity { get; private set; }
    public string Tool { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string Fingerprint { get; private set; } = string.Empty;
    public string? FilePath { get; private set; }
    public int? Line { get; private set; }
    public int? Column { get; private set; }
    public string? Function { get; private set; }
    public string? ExceptionType { get; private set; }
    public string? StackTrace { get; private set; }
    public string? RuleId { get; private set; }
    public ReproductionStatus ReproductionStatus { get; private set; }
    public int ReproducedAttempts { get; private set; }
    public int TotalAttempts { get; private set; }
    public DateTimeOffset ObservedAtUtc { get; private set; }

    public void ApplyReproduction(ReproductionStatus status, int reproduced, int total)
    {
        ReproductionStatus = status;
        ReproducedAttempts = Math.Max(0, reproduced);
        TotalAttempts = Math.Max(0, total);
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
