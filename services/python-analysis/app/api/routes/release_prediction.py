# Purpose: Implements release prediction responsibilities for routes.
from fastapi import APIRouter, Depends

from app.api.dependencies import get_release_predictor
from app.core.security import verify_api_key
from app.schemas.release_prediction import ReleasePredictionRequest, ReleasePredictionResponse
from app.services.release_predictor import ReleasePredictor

router = APIRouter(prefix="/api/analysis", tags=["analysis"], dependencies=[Depends(verify_api_key)])


@router.post("/release-prediction", response_model=ReleasePredictionResponse)
def predict_release(
    request: ReleasePredictionRequest,
    predictor: ReleasePredictor = Depends(get_release_predictor),
) -> ReleasePredictionResponse:
    return predictor.predict(request)
