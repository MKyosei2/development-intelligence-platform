# Purpose: Implements anomaly responsibilities for schemas.
from datetime import datetime

from pydantic import BaseModel, Field


class TimeSeriesPoint(BaseModel):
    timestamp: datetime
    value: float


class AnomalyRequest(BaseModel):
    metric_name: str = Field(min_length=1, max_length=120)
    points: list[TimeSeriesPoint] = Field(min_length=3)
    zscore_threshold: float | None = Field(default=None, ge=1.0, le=10.0)


class AnomalyPoint(BaseModel):
    timestamp: datetime
    value: float
    zscore: float
    reason: str


class AnomalyResponse(BaseModel):
    metric_name: str
    sample_count: int
    mean: float
    standard_deviation: float
    anomalies: list[AnomalyPoint]
