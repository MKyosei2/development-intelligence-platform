// Purpose: Defines the QaExecutionCommand application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaExecutionCommand(Guid ProjectId, string SubjectKey, LanguageId Language, string WorkspaceRelativePath, string? CommitSha, string? RuntimeExecutable, IReadOnlyList<string>? RuntimeArguments, int TimeoutSeconds = 120, bool AutomaticallyReproduce = true, bool RecordScreen = true, bool RecordSystemAudio = true, string? BranchName = null, bool RefreshRemoteBranch = false);
