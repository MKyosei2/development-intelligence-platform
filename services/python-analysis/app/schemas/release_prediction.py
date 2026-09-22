# Purpose: Defines a focused responsibility within the development intelligence platform.
from pydantic import BaseModel, Field

from app.schemas.common import EvidenceItem


class ReleasePredictionRequest(BaseModel):
    project_id: str
    days_until_release: int
    open_tasks: int = Field(default=0, ge=0)
    overdue_tasks: int = Field(default=0, ge=0)
    blocked_tasks: int = Field(default=0, ge=0)
    critical_issues: int = Field(default=0, ge=0)
    failed_builds_last_7_days: int = Field(default=0, ge=0)
    average_review_wait_hours: float = Field(default=0, ge=0)
    weekly_velocity: float = Field(default=0, ge=0)
    evidence: list[EvidenceItem] = Field(default_factory=list)


class ReleasePredictionResponse(BaseModel):
    confidence: float = Field(ge=0, le=100)
    estimated_delay_days: int = Field(ge=0)
    risk_level: str
    factors: list[str]
    evidence_ids: list[str]
