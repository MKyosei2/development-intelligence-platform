// Purpose: Keeps the IP2ExperimentRunner contract stable while delegating each experiment family to a focused runner.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

public sealed class P2ExperimentRunner : IP2ExperimentRunner
{
    private readonly RepeatedExperimentRunner _repeated;
    private readonly SoakExperimentRunner _soak;
    private readonly GitBisectRunner _bisect;
    private readonly MutationExperimentRunner _mutation;
    private readonly FuzzExperimentRunner _fuzz;

    public P2ExperimentRunner(ProcessExecutor executor, IQaWorkspaceResolver workspaceResolver)
    {
        _repeated = new(executor, workspaceResolver);
        _soak = new(executor, workspaceResolver);
        _bisect = new(executor, workspaceResolver);
        _mutation = new(executor, workspaceResolver);
        _fuzz = new(executor, workspaceResolver);
    }

    public Task<FlakyResult> RunRepeatedAsync(RepeatedRunCommand command, CancellationToken ct) => _repeated.RunRepeatedAsync(command, ct);
    public Task<SoakResult> RunSoakAsync(SoakCommand command, CancellationToken ct) => _soak.RunSoakAsync(command, ct);
    public Task<GitBisectResult> FindRegressionCommitAsync(GitBisectCommand command, CancellationToken ct) => _bisect.FindRegressionCommitAsync(command, ct);
    public Task<MutationResult> RunMutationAsync(MutationCommand command, CancellationToken ct) => _mutation.RunMutationAsync(command, ct);
    public Task<FuzzResult> RunFuzzAsync(FuzzCommand command, CancellationToken ct) => _fuzz.RunFuzzAsync(command, ct);
}
