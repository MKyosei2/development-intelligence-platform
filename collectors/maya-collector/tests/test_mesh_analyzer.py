# Purpose: Verifies Maya mesh metrics, triangle aggregation, threshold findings, and empty-scene behavior without Maya runtime.
import unittest

from dip_maya_collector.analyzers import mesh_analyzer
from dip_maya_collector.models import CollectorReport


class FakeMeshCmds:
    def __init__(self, triangles):
        self.triangles = triangles

    def ls(self, type=None, long=False):
        self.assert_mesh_query(type, long)
        return list(self.triangles)

    @staticmethod
    def assert_mesh_query(type, long):
        if type != "mesh" or long is not True:
            raise AssertionError("mesh analyzer issued an unexpected ls query")

    def polyEvaluate(self, mesh, triangle=False):
        if triangle is not True:
            raise AssertionError("triangle=True is required")
        return self.triangles[mesh]


class MeshAnalyzerTests(unittest.TestCase):
    def test_aggregates_triangles_and_flags_only_meshes_over_threshold(self):
        report = CollectorReport()
        cmds = FakeMeshCmds({"|root|hero": 150, "|root|prop": 40})

        mesh_analyzer.analyze(cmds, report, max_triangles=100)

        metrics = {metric.name: metric.value for metric in report.metrics}
        self.assertEqual(2, metrics["maya_mesh_count"])
        self.assertEqual(190, metrics["maya_total_triangles"])
        self.assertEqual(1, len(report.issues))
        self.assertEqual("|root|hero", report.issues[0].source)
        self.assertIn("150 triangles exceeds 100", report.issues[0].description)

    def test_empty_scene_reports_zero_counts_without_findings(self):
        report = CollectorReport()
        mesh_analyzer.analyze(FakeMeshCmds({}), report)

        metrics = {metric.name: metric.value for metric in report.metrics}
        self.assertEqual(0, metrics["maya_mesh_count"])
        self.assertEqual(0, metrics["maya_total_triangles"])
        self.assertEqual([], report.issues)


if __name__ == "__main__":
    unittest.main()
