# Use Redis

## Decision
Webhook Burst、Queue、Cache、重複排除補助にRedisを使う。

## Consequences
Contract、Version、Timeout、Migrationを明示的に管理します。
