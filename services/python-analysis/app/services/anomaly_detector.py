# Purpose: Implements anomaly detector responsibilities for app.
from statistics import fmean, pstdev

from app.schemas.anomaly import AnomalyPoint, AnomalyRequest, AnomalyResponse


class AnomalyDetector:
    def detect(self, request: AnomalyRequest, default_threshold: float) -> AnomalyResponse:
        values = [point.value for point in request.points]
        mean = fmean(values)
        standard_deviation = pstdev(values)
        threshold = request.zscore_threshold or default_threshold
        anomalies: list[AnomalyPoint] = []
        if standard_deviation > 0:
            for point in request.points:
                zscore = (point.value - mean) / standard_deviation
                if abs(zscore) >= threshold:
                    anomalies.append(
                        AnomalyPoint(
                            timestamp=point.timestamp,
                            value=point.value,
                            zscore=round(zscore, 4),
                            reason=(
                                f"Absolute z-score {abs(zscore):.2f} exceeded "
                                f"threshold {threshold:.2f}."
                            ),
                        )
                    )
        return AnomalyResponse(
            metric_name=request.metric_name,
            sample_count=len(values),
            mean=round(mean, 4),
            standard_deviation=round(standard_deviation, 4),
            anomalies=anomalies,
        )
