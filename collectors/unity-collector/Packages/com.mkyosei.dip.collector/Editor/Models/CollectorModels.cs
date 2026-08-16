using System;
using System.Collections.Generic;

namespace Dip.UnityCollector.Editor.Models
{
    [Serializable] public sealed class CollectorReport
    {
        public string schemaVersion="1.0";public string reportId=Guid.NewGuid().ToString();public string collectorType="unity";public string collectorVersion;public string projectId;public string repository;public string commitSha;public string branch;public string startedAt;public string finishedAt;public string status;public List<CollectorMetric> metrics=new List<CollectorMetric>();public List<CollectorIssue> issues=new List<CollectorIssue>();
    }
    [Serializable] public sealed class CollectorMetric{public string name;public double value;public string unit;public string benchmarkVersion;public string metadataJson;}
    [Serializable] public sealed class CollectorIssue{public string category;public string severity;public string title;public string description;public string source;public string ruleId;public string evidenceJson;}
}
