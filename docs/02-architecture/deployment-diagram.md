# Deployment

```mermaid
flowchart TB
 U[Browser] --> N[Nginx]
 N --> W[Web]
 N --> A[API]
 A --> P[(PostgreSQL)]
 A --> R[(Redis)]
 K[Worker] --> R
 K --> PY[Python]
 A --> O[OTel]
 PY --> O
 O --> PR[Prometheus]
 G[Grafana] --> PR
```
