# Purpose: Defines a focused responsibility within the development intelligence platform.
from dip_maya_collector.models import CollectorMetric, CollectorReport

def analyze(cmds, report: CollectorReport) -> None:
    transforms = cmds.ls(type="transform", long=True) or []
    roots = [node for node in transforms if not cmds.listRelatives(node, parent=True)]
    report.metrics.append(CollectorMetric("maya_transform_count", len(transforms), "count"))
    report.metrics.append(CollectorMetric("maya_root_count", len(roots), "count"))
