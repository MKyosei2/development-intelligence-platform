# Purpose: Verifies Maya skin-cluster analysis for normal rigs and empty scenes without Maya runtime.
import unittest

from dip_maya_collector.analyzers import skin_analyzer
from dip_maya_collector.models import CollectorReport


class FakeSkinCmds:
    def __init__(self, clusters):
        self.clusters = clusters

    def ls(self, type=None):
        if type != "skinCluster":
            raise AssertionError("skin analyzer issued an unexpected ls query")
        return list(self.clusters)

    def skinCluster(self, cluster, query=False, influence=False):
        if query is not True or influence is not True:
            raise AssertionError("skinCluster query=True, influence=True is required")
        return self.clusters[cluster]


class SkinAnalyzerTests(unittest.TestCase):
    def test_normal_skin_data_reports_count_without_warning(self):
        report = CollectorReport()
        cmds = FakeSkinCmds({"skinCluster1": ["joint1", "joint2", "joint3", "joint4"]})

        skin_analyzer.analyze(cmds, report)

        metrics = {metric.name: metric.value for metric in report.metrics}
        self.assertEqual(1, metrics["maya_skin_cluster_count"])
        self.assertEqual([], report.issues)

    def test_empty_skin_data_reports_zero_clusters(self):
        report = CollectorReport()
        skin_analyzer.analyze(FakeSkinCmds({}), report)

        metrics = {metric.name: metric.value for metric in report.metrics}
        self.assertEqual(0, metrics["maya_skin_cluster_count"])
        self.assertEqual([], report.issues)


if __name__ == "__main__":
    unittest.main()
