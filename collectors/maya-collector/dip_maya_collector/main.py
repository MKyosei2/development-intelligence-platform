from datetime import UTC, datetime
from dip_maya_collector.analyzers import (
    animation_analyzer,
    blendshape_analyzer,
    hierarchy_analyzer,
    material_analyzer,
    mesh_analyzer,
    skin_analyzer,
)
from dip_maya_collector.models import CollectorIssue, CollectorReport
from dip_maya_collector.reporting.report_writer import write_report

def run(project_id: str, commit_sha: str, branch: str, output_path: str) -> None:
    try:
        import maya.cmds as cmds
    except ImportError as exc:
        raise RuntimeError("Run this collector inside Autodesk Maya or mayapy.") from exc

    report = CollectorReport(
        projectId=project_id,
        commitSha=commit_sha,
        branch=branch,
        startedAt=datetime.now(UTC).isoformat(),
    )
    try:
        hierarchy_analyzer.analyze(cmds, report)
        mesh_analyzer.analyze(cmds, report)
        material_analyzer.analyze(cmds, report)
        skin_analyzer.analyze(cmds, report)
        blendshape_analyzer.analyze(cmds, report)
        animation_analyzer.analyze(cmds, report)
    except Exception as exc:
        report.status = "failed"
        report.issues.append(CollectorIssue(
            "collector", "Critical", "Maya collector failed", repr(exc), "maya"
        ))
        raise
    finally:
        report.finishedAt = datetime.now(UTC).isoformat()
        write_report(output_path, report)
