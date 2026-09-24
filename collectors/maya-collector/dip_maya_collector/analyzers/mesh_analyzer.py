# Purpose: Measures Maya mesh counts and triangle totals and flags meshes that exceed the configured triangle threshold.
from dip_maya_collector.models import CollectorIssue, CollectorMetric, CollectorReport

def analyze(cmds, report: CollectorReport, max_triangles: int = 100000) -> None:
    meshes = cmds.ls(type="mesh", long=True) or []
    total_triangles = 0
    for mesh in meshes:
        triangles = int(cmds.polyEvaluate(mesh, triangle=True) or 0)
        total_triangles += triangles
        if triangles > max_triangles:
            report.issues.append(CollectorIssue(
                "asset", "Warning", "High triangle Maya mesh",
                f"{triangles} triangles exceeds {max_triangles}.", mesh
            ))
    report.metrics.append(CollectorMetric("maya_mesh_count", len(meshes), "count"))
    report.metrics.append(CollectorMetric("maya_total_triangles", total_triangles, "triangles"))
