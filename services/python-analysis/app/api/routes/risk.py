# Purpose: Defines a focused responsibility within the development intelligence platform.
from fastapi import APIRouter, Depends

from app.api.dependencies import get_risk_analyzer
from app.core.security import verify_api_key
from app.schemas.risk import RiskRequest, RiskResponse
from app.services.risk_analyzer import RiskAnalyzer

router = APIRouter(prefix="/api/analysis", tags=["analysis"], dependencies=[Depends(verify_api_key)])


@router.post("/risk", response_model=RiskResponse)
def analyze_risk(
    request: RiskRequest,
    analyzer: RiskAnalyzer = Depends(get_risk_analyzer),
) -> RiskResponse:
    return analyzer.analyze(request)
