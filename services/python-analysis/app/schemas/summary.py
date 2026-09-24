# Purpose: Implements summary responsibilities for schemas.
from pydantic import BaseModel, Field

from app.schemas.common import EvidenceItem


class SummaryInput(BaseModel):
    project_id: str
    project_name: str
    health_score: float = Field(ge=0, le=100)
    failed_builds: int = Field(default=0, ge=0)
    test_failures: int = Field(default=0, ge=0)
    critical_issues: int = Field(default=0, ge=0)
    warning_issues: int = Field(default=0, ge=0)
    overdue_tasks: int = Field(default=0, ge=0)
    blocked_tasks: int = Field(default=0, ge=0)
    high_risk_pull_requests: int = Field(default=0, ge=0)
    overloaded_members: int = Field(default=0, ge=0)
    fps_regression_percent: float = 0
    memory_regression_percent: float = 0
    evidence: list[EvidenceItem] = Field(default_factory=list)


class SummaryResponse(BaseModel):
    overall_status: str
    top_risks: list[str]
    recommended_actions: list[str]
    evidence_ids: list[str]
