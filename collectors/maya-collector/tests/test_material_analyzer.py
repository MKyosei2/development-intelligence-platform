# Purpose: Verifies Maya material and shading-engine counts using a Maya-runtime-free command stub.
import unittest

from dip_maya_collector.analyzers import material_analyzer
from dip_maya_collector.models import CollectorReport


class FakeMaterialCmds:
    def ls(self, materials=False, type=None):
        if materials:
            return ["lambert1", "hero_mat", "prop_mat"]
        if type == "shadingEngine":
            return ["initialShadingGroup", "heroSG"]
        raise AssertionError("material analyzer issued an unexpected ls query")


class MaterialAnalyzerTests(unittest.TestCase):
    def test_counts_materials_and_shading_engines(self):
        report = CollectorReport()
        material_analyzer.analyze(FakeMaterialCmds(), report)

        metrics = {metric.name: metric.value for metric in report.metrics}
        self.assertEqual(3, metrics["maya_material_count"])
        self.assertEqual(2, metrics["maya_shading_engine_count"])


if __name__ == "__main__":
    unittest.main()
