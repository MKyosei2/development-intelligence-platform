# Purpose: Defines a focused responsibility within the development intelligence platform.
from dip_maya_collector.models import CollectorMetric, CollectorReport

def analyze(cmds, report: CollectorReport) -> None:
    curves = cmds.ls(type="animCurve") or []
    start = float(cmds.playbackOptions(query=True, minTime=True))
    end = float(cmds.playbackOptions(query=True, maxTime=True))
    report.metrics.append(CollectorMetric("maya_animation_curve_count", len(curves), "count"))
    report.metrics.append(CollectorMetric("maya_timeline_length", end - start, "frames"))
