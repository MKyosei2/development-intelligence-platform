# Use Redis

Status: Superseded

Superseded by the PostgreSQL-backed durable queue decision recorded below.

## Historical decision

Redis was originally selected for webhook bursts, queue delivery, caching and deduplication support.

## Replacement decision

The supported runtime now stores both durable application state and queue-delivery state in PostgreSQL. Queue semantics remain isolated behind `IQueueClient`; `PostgresQueueClient` implements leasing, acknowledgement, retry and dead-letter delivery.

## Rationale and consequences

- removes Redis as a mandatory runtime and operational dependency;
- keeps delivery records transactional and inspectable beside application state;
- retains a provider-neutral boundary so the implementation can change later;
- increases PostgreSQL workload, so queue latency, connection use and table growth must be monitored;
- requires schema, recovery and load tests to cover both business and delivery state.
