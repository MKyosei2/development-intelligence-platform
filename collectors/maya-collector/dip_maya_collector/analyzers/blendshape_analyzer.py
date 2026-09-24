# Purpose: Implements blendshape analyzer responsibilities for analyzers.
from dip_maya_collector.models import CollectorMetric, CollectorReport

def analyze(cmds, report: CollectorReport) -> None:
    blend_shapes = cmds.ls(type="blendShape") or []
    report.metrics.append(CollectorMetric("maya_blendshape_count", len(blend_shapes), "count"))
