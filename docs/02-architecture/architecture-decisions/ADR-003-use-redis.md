# Redis採用判断の変更

Status: Superseded

この判断は、後述するPostgreSQL-backed durable queueの採用判断によって置き換えられた。

## 過去の判断

当初は、Webhook burst、queue delivery、cache、deduplicationを支えるためRedisを採用していた。

## 置き換え後の判断

現在のsupported runtimeでは、durable application stateとqueue-delivery stateの両方をPostgreSQLへ保存する。Queue semanticsは`IQueueClient`の背後へ分離し、`PostgresQueueClient`がlease、acknowledgement、retry、dead-letter deliveryを実装する。

## 理由と影響

- Redisを必須runtime・運用dependencyから外せる。
- Delivery recordをapplication stateの近くでtransactionalかつinspect可能に保てる。
- Provider-neutralな境界を維持するため、将来implementationを変更できる。
- PostgreSQL workloadは増えるため、queue latency、connection使用量、table growthを監視する必要がある。
- Schema、recovery、load testではbusiness stateとdelivery stateの両方を対象にする必要がある。
