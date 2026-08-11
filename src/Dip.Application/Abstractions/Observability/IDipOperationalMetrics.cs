namespace Dip.Application.Abstractions.Observability;
public interface IDipOperationalMetrics
{
    void JobStarted(string type); void JobFinished(string type,string status,TimeSpan duration); void ArtifactStored(string provider,long bytes); void LeaseRecovered(); void QueueDepthObserved(long depth); string RenderPrometheus();
}
