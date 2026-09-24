# Purpose: Implements test anomaly detector responsibilities for python analysis.
from datetime import UTC, datetime, timedelta
from app.schemas.anomaly import AnomalyRequest, TimeSeriesPoint
from app.services.anomaly_detector import AnomalyDetector

def test_detects_large_outlier() -> None:
    start = datetime(2026, 1, 1, tzinfo=UTC)
    values = [10, 10, 11, 9, 10, 40]
    request = AnomalyRequest(
        metric_name="build_duration",
        points=[TimeSeriesPoint(timestamp=start + timedelta(minutes=i), value=v) for i, v in enumerate(values)],
        zscore_threshold=2.0,
    )
    result = AnomalyDetector().detect(request, 2.5)
    assert len(result.anomalies) == 1
    assert result.anomalies[0].value == 40
