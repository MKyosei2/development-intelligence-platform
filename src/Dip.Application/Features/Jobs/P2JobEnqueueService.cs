// Purpose: Coordinates P2 Jo bE nq ue ue Se rv ic e application behavior for Jobs.
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Jobs;

public sealed class P2JobEnqueueService(DurableJobService jobs)
{
    public Task<EnqueuedJobDto> FuzzAsync(FuzzCommand command,CancellationToken ct)=>jobs.EnqueueAsync(command.Target.ProjectId,DurableJobType.Fuzz,command.Target.SubjectKey,command,3,ct);
    public Task<EnqueuedJobDto> MutationAsync(MutationCommand command,CancellationToken ct)=>jobs.EnqueueAsync(command.Target.ProjectId,DurableJobType.Mutation,command.Target.SubjectKey,command,3,ct);
    public Task<EnqueuedJobDto> SoakAsync(SoakCommand command,CancellationToken ct)=>jobs.EnqueueAsync(command.Target.ProjectId,DurableJobType.Soak,command.Target.SubjectKey,command,2,ct);
    public Task<EnqueuedJobDto> BisectAsync(GitBisectCommand command,CancellationToken ct)=>jobs.EnqueueAsync(command.Target.ProjectId,DurableJobType.GitBisect,command.Target.SubjectKey,command,2,ct);
    public Task<EnqueuedJobDto> MediaAsync(MediaRegressionCommand command,CancellationToken ct)=>jobs.EnqueueAsync(command.ProjectId,DurableJobType.MediaRegression,command.SubjectKey,command,3,ct);
}
