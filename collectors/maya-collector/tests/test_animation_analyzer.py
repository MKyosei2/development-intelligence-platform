# Purpose: Verifies Maya animation curve counts and timeline-range metrics using a Maya-runtime-free command stub.
import unittest

from dip_maya_collector.analyzers import animation_analyzer
from dip_maya_collector.models import CollectorReport


class FakeAnimationCmds:
    def ls(self, type=None):
        if type != "animCurve":
            raise AssertionError("animation analyzer issued an unexpected ls query")
        return ["curve1", "curve2", "curve3"]

    def playbackOptions(self, query=False, minTime=False, maxTime=False):
        if query is not True:
            raise AssertionError("query=True is required")
        if minTime:
            return 10.0
        if maxTime:
            return 82.5
        raise AssertionError("expected minTime or maxTime query")


class AnimationAnalyzerTests(unittest.TestCase):
    def test_counts_curves_and_calculates_timeline_length(self):
        report = CollectorReport()
        animation_analyzer.analyze(FakeAnimationCmds(), report)

        metrics = {metric.name: metric.value for metric in report.metrics}
        self.assertEqual(3, metrics["maya_animation_curve_count"])
        self.assertEqual(72.5, metrics["maya_timeline_length"])


if __name__ == "__main__":
    unittest.main()
