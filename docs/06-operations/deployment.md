# デプロイ

Production deploymentはexact-SHAかつNative-firstで行う。`.github/workflows/live-deploy.yml`は.NET service、Python Analysis、EF migration bundleをimmutable release directoryへpublishし、hashを検証し、durable stateをsnapshotし、`current` symlinkを更新してsystemd user serviceを再起動する。

Caddyはhost-nativeで動作し、public trafficを`127.0.0.1:5080`（API）と`127.0.0.1:5081`（Web）へproxyする。Automated live deployment rollback Evidenceには、deployment filesystemとは別のoffsite filesystemを必須とする。
