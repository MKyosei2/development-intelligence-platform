// Purpose: Defines the execution boundary for repeated, soak, bisect, mutation, and fuzz experiments.
namespace Dip.Application.Features.P2Intelligence;

public interface IP2ExperimentRunner
{
    Task<FlakyResult> RunRepeatedAsync(
        RepeatedRunCommand command,
        CancellationToken cancellationToken);

    Task<SoakResult> RunSoakAsync(
        SoakCommand command,
        CancellationToken cancellationToken);

    Task<GitBisectResult> FindRegressionCommitAsync(
        GitBisectCommand command,
        CancellationToken cancellationToken);

    Task<MutationResult> RunMutationAsync(
        MutationCommand command,
        CancellationToken cancellationToken);

    Task<FuzzResult> RunFuzzAsync(
        FuzzCommand command,
        CancellationToken cancellationToken);
}
