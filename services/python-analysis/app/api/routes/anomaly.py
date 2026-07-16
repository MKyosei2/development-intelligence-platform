from fastapi import APIRouter, Depends
from app.api.dependencies import get_anomaly_detector
from app.core.config import get_settings
from app.core.security import verify_api_key
from app.schemas.anomaly import AnomalyRequest, AnomalyResponse
from app.services.anomaly_detector import AnomalyDetector

router = APIRouter(prefix="/api/analysis", tags=["analysis"], dependencies=[Depends(verify_api_key)])

@router.post("/anomaly", response_model=AnomalyResponse)
def detect_anomaly(request: AnomalyRequest, detector: AnomalyDetector = Depends(get_anomaly_detector)) -> AnomalyResponse:
    return detector.detect(request, get_settings().anomaly_zscore_threshold)
