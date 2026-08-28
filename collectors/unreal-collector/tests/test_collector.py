import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

MODULE = Path(__file__).resolve().parents[1] / "collector.py"
spec = importlib.util.spec_from_file_location("dip_unreal_collector", MODULE)
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)


class CollectorTests(unittest.TestCase):
    def test_collects_project_and_log(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            (root / "Game.uproject").write_text(
                json.dumps({"EngineAssociation": "5.6", "Modules": [{"Name": "Game"}]})
            )
            (root / "Content").mkdir()
            (root / "Content" / "Map.umap").write_bytes(b"x")
            (root / "Saved" / "Logs").mkdir(parents=True)
            (root / "Saved" / "Logs" / "Game.log").write_text(
                "LogTemp: Warning: sample warning\n"
            )

            metrics, issues = collector.collect(root, None)

            self.assertTrue(
                any(item["name"] == "unreal.map_count" and item["value"] == 1 for item in metrics)
            )
            self.assertTrue(any(item["severity"] == "Warning" for item in issues))

    def test_collects_automation_failure_and_crash_artifact(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            (root / "Game.uproject").write_text("{}")
            report = root / "Saved" / "Automation" / "Reports" / "run" / "index.json"
            report.parent.mkdir(parents=True)
            report.write_text(
                json.dumps({"tests": [{"state": "Success"}, {"state": "Fail"}]})
            )
            crash = root / "Saved" / "Crashes" / "Crash-1" / "minidump.dmp"
            crash.parent.mkdir(parents=True)
            crash.write_bytes(b"dmp")

            metrics, issues = collector.collect(root, None, report)

            self.assertTrue(
                any(
                    item["name"] == "unreal.automation.failed" and item["value"] >= 1
                    for item in metrics
                )
            )
            self.assertTrue(
                any(item.get("ruleId") == "UNREAL_AUTOMATION_FAILED" for item in issues)
            )
            self.assertTrue(
                any(
                    item["name"] == "unreal.crash_artifact_count" and item["value"] == 1
                    for item in metrics
                )
            )

    def test_missing_uproject_is_critical(self):
        with tempfile.TemporaryDirectory() as td:
            _, issues = collector.collect(Path(td), None)
            self.assertTrue(
                any(
                    item.get("ruleId") == "UPROJECT_MISSING"
                    and item["severity"] == "Critical"
                    for item in issues
                )
            )

    def test_quality_gate_fails_on_critical_by_default(self):
        issues = [
            {
                "severity": "Critical",
                "ruleId": "UNREAL_AUTOMATION_FAILED",
            }
        ]
        self.assertTrue(collector.quality_gate_failed(issues, "critical"))
        self.assertFalse(collector.quality_gate_failed(issues, "off"))

    def test_quality_gate_warning_mode_fails_on_warning(self):
        issues = [{"severity": "Warning", "ruleId": "UNREAL_RHI_REVIEW"}]
        self.assertFalse(collector.quality_gate_failed(issues, "critical"))
        self.assertTrue(collector.quality_gate_failed(issues, "warning"))

    def test_missing_uproject_always_fails_even_when_gate_is_off(self):
        issues = [
            {
                "severity": "Critical",
                "ruleId": "UPROJECT_MISSING",
            }
        ]
        self.assertTrue(collector.quality_gate_failed(issues, "off"))


if __name__ == "__main__":
    unittest.main()
