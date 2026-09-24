# Purpose: Measures Maya material and shading-engine counts for collector reports.
from dip_maya_collector.models import CollectorMetric, CollectorReport

def analyze(cmds, report: CollectorReport) -> None:
    materials = cmds.ls(materials=True) or []
    shading_engines = cmds.ls(type="shadingEngine") or []
    report.metrics.append(CollectorMetric("maya_material_count", len(materials), "count"))
    report.metrics.append(CollectorMetric("maya_shading_engine_count", len(shading_engines), "count"))
