# ER Diagram

```mermaid
erDiagram
 PROJECT ||--o{ PIPELINE_RUN : has
 PIPELINE_RUN ||--o{ METRIC : contains
 PROJECT ||--o{ QUALITY_ISSUE : has
 PIPELINE_RUN ||--o{ QUALITY_ISSUE : produces
 PROJECT ||--o{ DAILY_SUMMARY : has
```
