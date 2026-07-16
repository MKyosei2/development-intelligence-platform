# Python Service Boundary

## Decision
PythonはHTTP Serviceとし、主DBへ直接書き込ませない。

## Consequences
Contract、Version、Timeout、Migrationを明示的に管理します。
