# Purpose: Verifies Maya hierarchy root detection does not classify child transforms as scene roots.
import unittest

from dip_maya_collector.analyzers import hierarchy_analyzer
from dip_maya_collector.models import CollectorReport


class FakeHierarchyCmds:
    transforms = ["|rootA", "|rootA|child", "|rootB"]

    def ls(self, type=None, long=False):
        if type != "transform" or long is not True:
            raise AssertionError("hierarchy analyzer issued an unexpected ls query")
        return list(self.transforms)

    def listRelatives(self, node, parent=False):
        if parent is not True:
            raise AssertionError("parent=True is required")
        return ["|rootA"] if node == "|rootA|child" else None


class HierarchyAnalyzerTests(unittest.TestCase):
    def test_counts_only_parentless_transforms_as_roots(self):
        report = CollectorReport()
        hierarchy_analyzer.analyze(FakeHierarchyCmds(), report)

        metrics = {metric.name: metric.value for metric in report.metrics}
        self.assertEqual(3, metrics["maya_transform_count"])
        self.assertEqual(2, metrics["maya_root_count"])


if __name__ == "__main__":
    unittest.main()
