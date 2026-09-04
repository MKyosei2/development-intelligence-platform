# Container Diagram

```mermaid
flowchart TB
 C[Collectors] --> A[ASP.NET Core API]
 G[GitHub] --> A
 A --> P[(PostgreSQL)]
 W --> P
 W --> PY[Python Analysis]
 B[Blazor Web] --> A
 A --> M[(Artifact Storage)]
```
