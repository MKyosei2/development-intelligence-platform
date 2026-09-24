# Purpose: Verifies Maya report writing creates directories, preserves UTF-8 text, and emits the expected schema structure.
import json
import tempfile
import unittest
from pathlib import Path

from dip_maya_collector.models import CollectorIssue, CollectorMetric, CollectorReport
from dip_maya_collector.reporting.report_writer import write_report


class ReportWriterTests(unittest.TestCase):
    def test_writes_utf8_json_creates_parent_directories_and_preserves_schema(self):
        report = CollectorReport(
            projectId="project-日本語",
            commitSha="abcdef1",
            branch="main",
            startedAt="2026-09-24T00:00:00+00:00",
            finishedAt="2026-09-24T00:01:00+00:00",
            metrics=[CollectorMetric("maya_mesh_count", 2, "count")],
            issues=[CollectorIssue("asset", "Warning", "日本語タイトル", "説明", "hero_mesh")],
        )

        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "nested" / "reports" / "maya-report.json"
            write_report(str(output), report)

            self.assertTrue(output.is_file())
            raw = output.read_text(encoding="utf-8")
            self.assertIn("日本語タイトル", raw)
            self.assertNotIn("\\u65e5", raw)

            payload = json.loads(raw)
            self.assertEqual("1.0", payload["schemaVersion"])
            self.assertEqual("maya", payload["collectorType"])
            self.assertEqual("project-日本語", payload["projectId"])
            self.assertEqual(1, len(payload["metrics"]))
            self.assertEqual(1, len(payload["issues"]))


if __name__ == "__main__":
    unittest.main()
