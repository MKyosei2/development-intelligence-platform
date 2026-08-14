import re
from dataclasses import dataclass


@dataclass(frozen=True)
class LogClassification:
    category: str
    severity: str
    matched_rule: str


class LogClassifier:
    _rules = (
        (re.compile(r"out of memory|oom", re.IGNORECASE), "memory", "critical"),
        (re.compile(r"segmentation fault|access violation", re.IGNORECASE), "native_crash", "critical"),
        (re.compile(r"test[s]? failed", re.IGNORECASE), "test", "warning"),
        (re.compile(r"timeout|timed out", re.IGNORECASE), "timeout", "warning"),
        (re.compile(r"missing reference", re.IGNORECASE), "asset_reference", "warning"),
    )

    def classify(self, line: str) -> LogClassification:
        for pattern, category, severity in self._rules:
            if pattern.search(line):
                return LogClassification(category, severity, pattern.pattern)
        return LogClassification("unknown", "info", "none")
