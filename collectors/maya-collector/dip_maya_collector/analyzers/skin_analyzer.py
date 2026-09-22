# Purpose: Defines a focused responsibility within the development intelligence platform.
from dip_maya_collector.models import CollectorIssue, CollectorMetric, CollectorReport

def analyze(cmds, report: CollectorReport, max_influences: int = 4) -> None:
    clusters = cmds.ls(type="skinCluster") or []
    report.metrics.append(CollectorMetric("maya_skin_cluster_count", len(clusters), "count"))
    for cluster in clusters:
        influences = cmds.skinCluster(cluster, query=True, influence=True) or []
        if len(influences) > 128:
            report.issues.append(CollectorIssue(
                "rig", "Warning", "High skin influence count",
                f"{len(influences)} influences are connected.", cluster
            ))
