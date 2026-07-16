from datetime import datetime
from pydantic import BaseModel, Field

class EvidenceItem(BaseModel):
    evidence_id: str = Field(min_length=1, max_length=200)
    source_type: str = Field(min_length=1, max_length=100)
    description: str = Field(min_length=1, max_length=1000)
    value: float | int | str | None = None
    unit: str | None = None
    occurred_at: datetime | None = None
