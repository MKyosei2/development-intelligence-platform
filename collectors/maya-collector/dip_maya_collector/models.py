from dataclasses import asdict, dataclass, field
from typing import Any

@dataclass
class CollectorMetric:
    name: str
    value: float
    unit: str
    baselineValue: float | None = None

@dataclass
class CollectorIssue:
    category: str
    severity: str
    title: str
    description: str
    source: str | None = None

@dataclass
class CollectorReport:
    schemaVersion: str = "1.0"
    collectorType: str = "maya"
    collectorVersion: str = "0.1.0"
    projectId: str = ""
    commitSha: str = ""
    branch: str = ""
    startedAt: str = ""
    finishedAt: str = ""
    status: str = "succeeded"
    metrics: list[CollectorMetric] = field(default_factory=list)
    issues: list[CollectorIssue] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)
