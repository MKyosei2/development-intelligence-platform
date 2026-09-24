# Purpose: Implements risk responsibilities for schemas.
from pydantic import BaseModel, Field

from app.schemas.common import EvidenceItem


class RiskFactorInput(BaseModel):
    changed_files: int = Field(default=0, ge=0)
    changed_lines: int = Field(default=0, ge=0)
    build_failed: bool = False
    test_failures: int = Field(default=0, ge=0)
    fps_regression_percent: float = 0
    memory_regression_percent: float = 0
    review_wait_hours: float = Field(default=0, ge=0)
    critical_issues: int = Field(default=0, ge=0)
    warning_issues: int = Field(default=0, ge=0)


class RiskRequest(BaseModel):
    project_id: str
    pull_request_id: str
    factors: RiskFactorInput


class RiskContribution(BaseModel):
    factor: str
    points: float
    explanation: str


class RiskResponse(BaseModel):
    score: float = Field(ge=0, le=100)
    level: str
    contributions: list[RiskContribution]
    evidence: list[EvidenceItem]
