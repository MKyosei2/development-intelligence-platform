# Non-Functional Requirements

## Performance
- Dashboard API p95 500ms以内
- Collector受付 p95 1000ms以内
- 1Projectあたり100万Metricを想定

## Reliability
- WebhookとReportを冪等処理
- Retry、Timeout、Dead Letter
- Python停止時もQuality Gate継続

## Security
- HMAC、OAuth State、RBAC、Secret非保存、Audit Log

## Maintainability
- Clean Architecture、Version付きContract、Testで保護
