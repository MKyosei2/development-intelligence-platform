# Purpose: Implements mesh analyzer responsibilities for analyzers.
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
