## 目的

このPull Requestが解決するEngineering上の問題を説明してください。機能一覧だけの説明は避けてください。

## 変更内容

- 
- 
- 

## 影響範囲

- [ ] Domain
- [ ] Application
- [ ] Infrastructure
- [ ] API
- [ ] Web dashboard
- [ ] Worker
- [ ] Python analysis service
- [ ] Collector / engine integration
- [ ] Database migration
- [ ] CI/CD
- [ ] Documentation / portfolio evidence

## 技術的判断

このPRで導入・変更した重要なtrade-offまたはinvariantを説明してください。Threshold、retry count、timeout、score、performance targetなどの数値を変更した場合は、その理由も記載してください。

## 検証

**このRevisionで実際に実行したCheckだけ**を記載してください。

```text
Commands/workflows:
- dotnet build ...
- dotnet test ...
- pytest ...
- Integration and E2E Tests
- Security Scan
```

RunnerやDependencyを利用できなかった場合は、PASSとせず「利用不可」と記録してください。

## 品質とRisk

- 想定する品質への影響:
- Performanceへの影響:
- Securityへの影響:
- Backward compatibility:
- Database / Configurationへの影響:
- Rollback方法:

## Evidence（証拠）

Screenshot、Log、Benchmark result、Trace ID、Sample Collector Report、Artifact linkなどを追加してください。計測結果には、対象となった正確なcommit / SHAを明記してください。

## チェックリスト

- [ ] 変更には単一で明確な目的がある。
- [ ] Commit subjectはRepositoryのConventional Commit ruleに従い、意図を表している。
- [ ] 挙動変更に対してTestを追加または更新した。
- [ ] 数値Threshold / Penaltyは、説明のないmagic numberではなく、命名・文書化・設定可能化のいずれかを行った。
- [ ] Public contractとSchemaはBackward compatibleである。またはbreaking changeを明記した。
- [ ] Secretや個人情報を含めていない。
- [ ] Error handlingとLoggingを確認した。
- [ ] 挙動を変更した場合、Documentationも更新した。
- [ ] Generated fileやLocal artifactは、意図したEvidenceでない限りCommitしていない。
- [ ] Historical Evidenceを新しいRevisionの証明として提示していない。
