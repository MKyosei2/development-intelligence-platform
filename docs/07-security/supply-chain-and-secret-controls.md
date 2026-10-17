# サプライチェーン・依存関係・Secret運用設計

> **文書ステータス:** As-IsのCI機構とTo-Beの運用基準  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 既存の検査
GitHub Actions `Security Scan`はNuGet/Python依存監査、Secret scan、静的解析フォールバックを持つ。`CODEQL_ENABLED`の設定によりCodeQLとfallbackの実行差がある。CIがGreenになったことだけでSAST/依存/Runtimeすべてを網羅したと主張しない。

## 2. セキュリティ修正の再現事例
2026-10-16に`Dip.Web`の`Microsoft.AspNetCore.SignalR.Client`を`8.0.4`→`8.0.32`へ更新し、同一修正SHAの`Security Scan`が成功した。問題は間接依存の`Microsoft.AspNetCore.SignalR.Common 8.0.4`に対するHigh advisory。**検出機構を無効化せずに依存を更新したこと**をEvidenceとして提示する。

## 3. 提案する変更管理
| 対象 | 条件 | 推奨チェック |
|---|---|---|
| Direct/Transitive依存 | 脆弱性、重大度、Runtime到達可能性 | Dependency review + lock/restore |
| GitHub Actions | commit固定・最小権限 | Workflow権限/Action更新 |
| Secret | Provider/Scope/期限 | Vault、短寿命、GitLeaks |
| Container/Runner image | イメージDigest、署名、更新 | SBOM、スキャン、起動検証 |
| Release Artifact | Source SHA、Build情報、hash | 署名・Provenance、検証時刻 |

## 4. Secret漏えい時のRunbook
1. 暴露対象と実際の使用範囲を確認し、操作記録を保全する。
2. 当該資格情報を即時失効・ローテーションする。
3. CI、Agent、Runner、Artifact Store、外部LLM接続のアクセスを確認する。
4. ログのSecretを安全にマスクし、関係者へIncidentとして記録する。
5. 新しいSecretが正常に動くか、旧Secretが拒否されるかを確認する。

## 5. リリース時のEvidence
`dependency audit run id`, advisory一覧, version delta, SBOM digest, signing identity, release SHA, recovery instructions。単一の静的Scan成功だけで外部への安全保証とはしない。

## 参照する実装・既存資料

- `.github/workflows/security-scan.yml`
- `src/Dip.Web/Dip.Web.csproj`
- `docs/09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md`
