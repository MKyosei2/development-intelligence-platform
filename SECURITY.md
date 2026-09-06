# セキュリティポリシー

## サポート対象Version

このProjectは現在Active Development中です。Security FixはDefault BranchのLatest Revisionへ適用します。

| Version | Support |
|---|---|
| Default branch / unreleased | Yes |
| Older snapshots and forks | No |

## Vulnerabilityの報告

疑わしいVulnerabilityをPublic Issue、Discussion、Pull Request、Log、Screenshot、Sample Payloadで公開しないでください。

利用可能な場合は、このRepositoryのGitHub Private Vulnerability Reportingを使用してください。利用できない場合はGitHubを通じてRepository OwnerへPrivateに連絡し、次を提示してください。

- Issueの簡潔な説明。
- 影響を受けるComponent、Endpoint、Worker、Collector、Configuration。
- Reproduction Stepまたは最小限のProof of Concept。
- Expected / Actual Behavior。
- Potential Impact。
- 分かる場合はSuggested Mitigation。

Live Credential、Access Token、Webhook Secret、Personal Information、Production Dataを含めないでください。

## 対応Process

Maintainerは次の対応を試みます。

1. Reportを受領したことを確認する。
2. 再現し、Impactを評価する。
3. FixとRegression Testを準備する。
4. UserがUpdateできる合理的な猶予を確保した後、Disclosureを調整する。

ProjectがUnreleasedである間、Response Timeは保証しません。

## セキュリティ上の期待事項

Contributorは次を守ってください。

- SecretをEnvironment Variable、User Secret、またはSecret Managerに保存する。
- `.env`、Token、Password、Private Key、Production PayloadをCommitしない。
- Collector / Webhook InputをValidateする。
- Webhook / Message Idempotencyを維持する。
- Parameterized Database Accessを使用する。
- DependencyとContainer ImageをPatchされた状態に保つ。
- Sensitive Request BodyやCredentialをLogへ出さない。
