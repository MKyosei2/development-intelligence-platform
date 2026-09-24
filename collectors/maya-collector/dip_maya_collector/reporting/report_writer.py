# Purpose: Serializes Maya collector reports as UTF-8 JSON and creates the destination directory when needed.
import json
from pathlib import Path
from dip_maya_collector.models import CollectorReport

def write_report(path: str, report: CollectorReport) -> None:
    output = Path(path)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report.to_dict(), ensure_ascii=False, indent=2), encoding="utf-8")
