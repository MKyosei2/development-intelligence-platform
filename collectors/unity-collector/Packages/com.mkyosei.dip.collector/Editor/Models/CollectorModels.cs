using System;
using System.Collections.Generic;

namespace Dip.UnityCollector.Editor.Models
{
    [Serializable]
    public sealed class CollectorReport
    {
        public string schemaVersion = "1.0";
        public string collectorType = "unity";
        public string collectorVersion = "0.1.0";
        public string projectId = string.Empty;
        public string commitSha = string.Empty;
        public string branch = string.Empty;
        public string startedAt = string.Empty;
        public string finishedAt = string.Empty;
        public string status = "succeeded";
        public List<CollectorMetric> metrics = new();
        public List<CollectorIssue> issues = new();
    }

    [Serializable]
    public sealed class CollectorMetric
    {
        public string name = string.Empty;
        public double value;
        public string unit = string.Empty;
        public double baselineValue;
        public bool hasBaseline;
    }

    [Serializable]
    public sealed class CollectorIssue
    {
        public string category = string.Empty;
        public string severity = "Info";
        public string title = string.Empty;
        public string description = string.Empty;
        public string source = string.Empty;
    }
}
