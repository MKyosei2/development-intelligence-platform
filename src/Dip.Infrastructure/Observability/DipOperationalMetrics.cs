using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using Dip.Application.Abstractions.Observability;

namespace Dip.Infrastructure.Observability;
public sealed class DipOperationalMetrics:IDipOperationalMetrics,IDisposable
{
    public static readonly ActivitySource ActivitySource=new("DIP.DevelopmentIntelligence","1.0.0");
    private readonly Meter _meter=new("DIP.DevelopmentIntelligence","1.0.0");private readonly Counter<long> _jobStartedCounter;private readonly Counter<long> _jobFinishedCounter;private readonly Counter<long> _artifactCounter;private readonly Counter<long> _artifactBytesCounter;private readonly Counter<long> _leaseRecoveredCounter;private readonly Histogram<double> _jobDuration;private readonly ConcurrentDictionary<(string Type,string Status),long> _jobFinished=new();private readonly ConcurrentDictionary<string,long> _jobStarted=new();private long _artifacts;private long _artifactBytes;private long _leases;private long _queueDepth;
    public DipOperationalMetrics(){_jobStartedCounter=_meter.CreateCounter<long>("dip.jobs.started");_jobFinishedCounter=_meter.CreateCounter<long>("dip.jobs.finished");_artifactCounter=_meter.CreateCounter<long>("dip.artifacts.stored");_artifactBytesCounter=_meter.CreateCounter<long>("dip.artifacts.bytes");_leaseRecoveredCounter=_meter.CreateCounter<long>("dip.jobs.lease_recovered");_jobDuration=_meter.CreateHistogram<double>("dip.jobs.duration","s");}
    public void JobStarted(string type){_jobStarted.AddOrUpdate(type,1,(_,v)=>v+1);_jobStartedCounter.Add(1,new KeyValuePair<string,object?>("job.type",type));}
    public void JobFinished(string type,string status,TimeSpan duration){_jobFinished.AddOrUpdate((type,status),1,(_,v)=>v+1);_jobFinishedCounter.Add(1,new KeyValuePair<string,object?>("job.type",type),new KeyValuePair<string,object?>("job.status",status));_jobDuration.Record(duration.TotalSeconds,new KeyValuePair<string,object?>("job.type",type));}
    public void ArtifactStored(string provider,long bytes){Interlocked.Increment(ref _artifacts);Interlocked.Add(ref _artifactBytes,bytes);_artifactCounter.Add(1,new KeyValuePair<string,object?>("provider",provider));_artifactBytesCounter.Add(bytes,new KeyValuePair<string,object?>("provider",provider));}
    public void LeaseRecovered(){Interlocked.Increment(ref _leases);_leaseRecoveredCounter.Add(1);}public void QueueDepthObserved(long depth)=>Interlocked.Exchange(ref _queueDepth,Math.Max(0,depth));
    public string RenderPrometheus(){var sb=new StringBuilder();sb.AppendLine("# TYPE dip_jobs_started_total counter");foreach(var x in _jobStarted.OrderBy(x=>x.Key,StringComparer.Ordinal))sb.Append("dip_jobs_started_total{type=\"").Append(Escape(x.Key)).Append("\"} ").AppendLine(x.Value.ToString(CultureInfo.InvariantCulture));sb.AppendLine("# TYPE dip_jobs_finished_total counter");foreach(var x in _jobFinished.OrderBy(x=>x.Key.Type,StringComparer.Ordinal).ThenBy(x=>x.Key.Status,StringComparer.Ordinal))sb.Append("dip_jobs_finished_total{type=\"").Append(Escape(x.Key.Type)).Append("\",status=\"").Append(Escape(x.Key.Status)).Append("\"} ").AppendLine(x.Value.ToString(CultureInfo.InvariantCulture));sb.Append("dip_artifacts_stored_total ").AppendLine(Interlocked.Read(ref _artifacts).ToString(CultureInfo.InvariantCulture));sb.Append("dip_artifact_bytes_total ").AppendLine(Interlocked.Read(ref _artifactBytes).ToString(CultureInfo.InvariantCulture));sb.Append("dip_job_lease_recovered_total ").AppendLine(Interlocked.Read(ref _leases).ToString(CultureInfo.InvariantCulture));sb.Append("dip_job_queue_depth ").AppendLine(Interlocked.Read(ref _queueDepth).ToString(CultureInfo.InvariantCulture));return sb.ToString();}
    private static string Escape(string value)=>value.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);public void Dispose(){_meter.Dispose();}
}
