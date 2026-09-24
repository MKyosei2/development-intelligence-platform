# Purpose: Verifies Maya blendshape counts using a Maya-runtime-free command stub.
import unittest

from dip_maya_collector.analyzers import blendshape_analyzer
from dip_maya_collector.models import CollectorReport


class FakeBlendShapeCmds:
    def ls(self, type=None):
        if type != "blendShape":
            raise AssertionError("blendshape analyzer issued an unexpected ls query")
        return ["faceShapes", "correctiveShapes"]


class BlendShapeAnalyzerTests(unittest.TestCase):
    def test_counts_blendshape_nodes(self):
        report = CollectorReport()
        blendshape_analyzer.analyze(FakeBlendShapeCmds(), report)

        metrics = {metric.name: metric.value for metric in report.metrics}
        self.assertEqual(2, metrics["maya_blendshape_count"])


if __name__ == "__main__":
    unittest.main()
