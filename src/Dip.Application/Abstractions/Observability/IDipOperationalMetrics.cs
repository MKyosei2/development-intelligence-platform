// Purpose: Defines the I Di pO pe ra ti on al Me tr ic s application boundary.
namespace Dip.Application.Abstractions.Observability;
public interface IDipOperationalMetrics
{
    void JobStarted(string type);
    void JobFinished(string type,string status,TimeSpan duration);
    void ArtifactStored(string provider,long bytes);
    void LeaseRecovered();
    void QueueDepthObserved(long depth);
    void GovernanceRunCompleted(string runStatus,string gateVerdict,int openFindings,int flakyTests,int activeWaivers,string selfHealth);
    void GovernanceMutation(string action,string status="success");
    void GovernanceSandboxDenied(string reason);
    string RenderPrometheus();
}
