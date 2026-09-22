// Purpose: Validates a QA request and prepares an isolated exact-revision execution plan.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

internal sealed class QaExecutionPlan : IAsyncDisposable
{
    private readonly BranchQaWorkspaceLease _lease;

    public QaExecutionPlan(
        QaExecutionCommand command,
        string sourceWorkspaceRelativePath,
        string workspace,
        string artifactRoot,
        LanguageId requestedLanguage,
        LanguageId runLanguage,
        QaTechnologyProfile profile,
        ProductionQaProfile production,
        IReadOnlyList<QaAdapterSelection> selections,
        IReadOnlyList<string> unsupportedDetectedLanguages,
        BranchQaWorkspaceLease lease)
    {
        Command = command;
        SourceWorkspaceRelativePath = sourceWorkspaceRelativePath;
        Workspace = workspace;
        ArtifactRoot = artifactRoot;
        RequestedLanguage = requestedLanguage;
        RunLanguage = runLanguage;
        Profile = profile;
        Production = production;
        Selections = selections;
        UnsupportedDetectedLanguages = unsupportedDetectedLanguages;
        _lease = lease;
    }

    public QaExecutionCommand Command { get; }
    public string SourceWorkspaceRelativePath { get; }
    public string Workspace { get; }
    public string ArtifactRoot { get; }
    public LanguageId RequestedLanguage { get; }
    public LanguageId RunLanguage { get; }
    public QaTechnologyProfile Profile { get; }
    public ProductionQaProfile Production { get; }
    public IReadOnlyList<QaAdapterSelection> Selections { get; }
    public IReadOnlyList<string> UnsupportedDetectedLanguages { get; }
    public string SourceRef => _lease.SourceRef;
    public string RefreshStatus => _lease.RefreshStatus;
    public string? Warning => _lease.Warning;
    public bool IsIsolated => _lease.IsIsolated;

    public ValueTask DisposeAsync() => _lease.DisposeAsync();
}

public sealed class QaExecutionPlanner(
    IProjectRepository projects,
    IQaWorkspaceResolver workspaceResolver,
    IBranchQaWorkspaceManager branchWorkspace,
    IRepositoryTechnologyDetector detector,
    IQaAdapterRegistry registry)
{
    internal async Task<QaExecutionPlan> CreateAsync(
        QaExecutionCommand command,
        Guid runId,
        CancellationToken ct)
    {
        if (command.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(command));

        _ = await projects.GetByIdAsync(command.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {command.ProjectId} not found.");

        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable)
            && Path.IsPathRooted(command.RuntimeExecutable))
        {
            throw new InvalidOperationException(
                "RuntimeExecutable must be relative to the configured QA workspace.");
        }

        var artifactRoot = workspaceResolver.QaArtifactRoot(command.ProjectId, runId);
        Directory.CreateDirectory(artifactRoot);

        var sourceRelativePath = command.WorkspaceRelativePath;
        var sourceWorkspace = workspaceResolver.Resolve(sourceRelativePath);
        var lease = await branchWorkspace.PrepareAsync(
            sourceWorkspace,
            sourceRelativePath,
            command.BranchName,
            command.CommitSha,
            command.RefreshRemoteBranch,
            runId,
            ct);

        try
        {
            var effectiveCommand = command with
            {
                WorkspaceRelativePath = lease.WorkspaceRelativePath,
                CommitSha = lease.CommitSha ?? command.CommitSha,
                BranchName = lease.BranchName ?? command.BranchName
            };

            var requested = string.IsNullOrWhiteSpace(effectiveCommand.Language.Value)
                ? LanguageId.Auto
                : effectiveCommand.Language;
            var profile = await detector.DetectAsync(lease.WorkspacePath, ct);
            var production = ProductionQaIntelligence.DetectProfile(
                profile,
                lease.WorkspacePath);
            var selections = registry.Resolve(requested, profile, lease.WorkspacePath);

            if (selections.Count == 0)
            {
                throw new NotSupportedException(
                    $"No QA adapter can handle '{requested}'. Detected languages: "
                    + $"{string.Join(", ", profile.Languages.Select(x => x.Value))}. "
                    + $"Adapters: {string.Join(", ", registry.AdapterIds)}. "
                    + "Add .dip/qa.json for a custom language.");
            }

            var unsupported = requested.IsAuto
                ? profile.Languages
                    .Where(language => selections.All(x => x.Language != language))
                    .Select(x => x.Value)
                    .ToArray()
                : Array.Empty<string>();
            var runLanguage = requested.IsAuto || selections.Count > 1
                ? LanguageId.Multi
                : selections[0].Language;

            return new QaExecutionPlan(
                effectiveCommand,
                sourceRelativePath,
                lease.WorkspacePath,
                artifactRoot,
                requested,
                runLanguage,
                profile,
                production,
                selections,
                unsupported,
                lease);
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }
}
