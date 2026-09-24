# Purpose: Implements dependencies responsibilities for api.
from app.core.config import get_settings
from app.services.anomaly_detector import AnomalyDetector
from app.services.release_predictor import ReleasePredictor
from app.services.risk_analyzer import RiskAnalyzer
from app.services.summary_generator import SummaryGenerator


def get_anomaly_detector() -> AnomalyDetector:
    return AnomalyDetector()


def get_risk_analyzer() -> RiskAnalyzer:
    return RiskAnalyzer()


def get_summary_generator() -> SummaryGenerator:
    return SummaryGenerator()


def get_release_predictor() -> ReleasePredictor:
    return ReleasePredictor(get_settings())
