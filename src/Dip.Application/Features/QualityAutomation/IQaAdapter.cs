// Purpose: Defines the IQaAdapter application boundary.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public interface IQaAdapter { string AdapterId { get;
 } int Priority { get;
 } bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace);
 Task<QaAdapterResult> RunAsync(QaExecutionCommand command, string workspace, string artifactRoot, CancellationToken cancellationToken);
 }
