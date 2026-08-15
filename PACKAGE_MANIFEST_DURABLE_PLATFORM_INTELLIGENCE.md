# Durable Platform Intelligence full-source package

Base: `MKyosei2/development-intelligence-platform` main at observed `2dd237468b61076bdaf25d4ac48dcf9ad459168d` (`Upgrade7`).

Included implementation:
1. Formal InitialBaseline migration + safe legacy adoption.
2. ArtifactBlob/ArtifactRecord + Local/MinIO durable storage + retention.
3. PostgreSQL-backed DurableJob + Redis transport + lease/heartbeat/recovery/cancel/retry.
4. Durable Fuzz/Mutation/Soak/Git Bisect/Media jobs.
5. Incident/Occurrence/Event timeline + aggregation and reopen/resolve lifecycle.
6. Regression-origin, native symbolication, and evidence-grounded root-cause incident integration.
7. BenchmarkDefinition/Run/Metric/Comparison persistence.
8. Durable BenchmarkJobHandler and HTTP/process benchmark executor.
9. Benchmark -> Artifact -> Evidence -> Project Truth -> Incident integration.

This is a full-source package, not a patch-only archive. Generated package validation artifacts describe what was and was not executed in the packaging environment. In particular, absence of a local .NET 8 SDK means this package must not be treated as having passed a real `dotnet build` or live EF/PostgreSQL/Redis/MinIO E2E run merely because static validation passes.
