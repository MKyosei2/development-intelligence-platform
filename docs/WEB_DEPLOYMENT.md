# Webデプロイ — Native-first

DIPのProduction DeploymentではDockerやContainer Registryは必須ではありません。

## Host側の前提条件

- .NET 8 Runtimeを備えたLinux Host
- Python 3.12+
- Deployment Accountから到達できるPostgreSQL Server
- PostgreSQL client tools (`psql`, `pg_dump`, `pg_restore`)
- Deployment Accountで `systemd --user` が利用可能であること。SSH Logout後もServiceを維持するため、そのAccountでlingerを有効化すること
- `deploy/Caddyfile` を使用してCaddyがInstall / Configureされていること
- Release Directory外にDurable Artifact / Workspace Directoryがあること
- 別MountのOffsite Backup Filesystemがあること

## Release Model（Release方式）

`Live Native Deployment` は `releases/<GITHUB_SHA>` を作成し、Release SHA-256 Manifestを検証し、Python venvを作成し、EF Migration Bundleを適用します。その後 `current` symlinkをAtomicに変更し、analysis / api / worker / webの4つのUser Serviceを再起動します。

Public Reverse Proxyは常にLoopback `5080` / `5081` を参照するため、Release切替時にApplication Addressを変更する必要はありません。

## Rollback（切り戻し）

変更前にWorkflowはPostgreSQL Custom-format Dump、Artifact Archive、Hashを保存し、それらを別FilesystemへMirrorし、Previous Releaseを記録します。Deployment Failure時はNew Serviceを停止し、Database / ArtifactをRestoreし、`current` をPrevious Releaseへ戻して再起動します。

Production Configurationは `deploy/web.env.example` を基にします。SecretはGitHub Environment Secret / Host Secret Managementから供給し、Commitしてはいけません。

Optional Integration / Notification Settingsは `$DIP_DEPLOY_PATH/host-overrides.env` に保持できます。生成されるsystemd unitはCore Deployment Environmentの後にこのFileを読み込み、SecretをRelease Archiveへ含めません。
