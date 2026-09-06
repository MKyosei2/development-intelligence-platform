# DIP Infrastructure（基盤構成）

このDirectoryには、DIPのInfrastructure関連資産をまとめています。

- API / Web / Worker / Python分析用Dockerfile
- PostgreSQL初期化・Seed SQL
- Prometheus
- OpenTelemetry Collector
- Grafana Provisioning
- Nginx Reverse Proxy
- Local起動・停止・Reset・Migration・Test用PowerShell

本番環境ではPassword、API Key、JWT KeyをFileへ直接書かず、Secret Managerまたは環境変数から注入してください。
