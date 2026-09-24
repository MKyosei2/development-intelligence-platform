# Purpose: Implements summary responsibilities for routes.
from fastapi import APIRouter, Depends

from app.api.dependencies import get_summary_generator
from app.core.security import verify_api_key
from app.schemas.summary import SummaryInput, SummaryResponse
from app.services.summary_generator import SummaryGenerator

router = APIRouter(prefix="/api/analysis", tags=["analysis"], dependencies=[Depends(verify_api_key)])


@router.post("/summary", response_model=SummaryResponse)
def generate_summary(
    request: SummaryInput,
    generator: SummaryGenerator = Depends(get_summary_generator),
) -> SummaryResponse:
    return generator.generate(request)
