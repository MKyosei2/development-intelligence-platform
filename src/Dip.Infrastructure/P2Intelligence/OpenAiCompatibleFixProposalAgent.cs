// Purpose: Implements Open AI Compatible Fix Proposal Agent infrastructure behavior for this repository area.
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

/// <summary>
/// Compatibility shim for the retired external-AI proposal adapter.
///
/// The runtime registers <see cref="DeterministicLocalDebugAgent"/> as the
/// authoritative fix-proposal/debug implementation. The previous implementation
/// depended on a P2IntelligenceOptions contract that was never defined or wired
/// into dependency injection, which made the entire solution uncompilable even
/// though the adapter was not reachable at runtime.
///
/// Keep this public type temporarily so existing callers do not fail to load, but
/// fail closed: no network request is made and no unverified AI output is emitted.
/// Reintroducing an external adapter requires an explicit opt-in configuration
/// contract, DI registration, security review, and evidence-grounding tests.
/// </summary>
[Obsolete("External AI proposal adapter is not a supported runtime dependency. Use DeterministicLocalDebugAgent.")]
public sealed class OpenAiCompatibleFixProposalAgent : IFixProposalAgent, IAutonomousDebugAgent, IDisposable
{
    public Task<FixProposalResponse?> ProposeAsync(FixProposalRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<FixProposalResponse?>(null);
    }

    public Task<DebugHypothesisResponse?> GenerateHypothesesAsync(DebugHypothesisRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<DebugHypothesisResponse?>(null);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
