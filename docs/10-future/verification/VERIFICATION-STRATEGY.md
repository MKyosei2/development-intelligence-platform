# 将来機能の検証戦略

**Status: Proposed** | **原則:** 実装・テスト・実機確認・本番運用は別の段階

## 1. Verification Pyramid

1. Static checks: schema、構文、分析設定、コード検査。
2. Unit tests: 評価器、正規化、権限、データ変換。
3. Integration tests: PostgreSQL、Artifact、Worker、API、Collectorの接続。
4. E2E tests: QA FindingからProject Truth、Release Readinessまで。
5. Real target runtime: 実Unity/Unreal、実GPU、複数Client、実機端末。
6. Operational verification: Load、Soak、Backup/Restore、Failover、Incident、Security boundary。

下位テストの成功で上位テストを代替しない。環境不足の結果は未評価であり成功でも製品不具合でもない。

## 2. Test Reportの最低限

対象機能、DIP SHA、Target SHA、テストID、入力、期待結果、実際結果、実行条件、開始終了時刻、Evidence artifact、ハッシュ、未評価理由、既知の限界。Tool outputを鵜呑みにしない。

## 3. QA指標

- Bug zoo: TP/FP/FN/TN、Recall、Precision、False Positive Rate、再現率、flaky率。
- Visual: Precision/Recall、誤検知箇所、変更許容ルール、主観レビュー差。
- Performance: 実機条件、Warmup、サンプル数、p50/p95/p99、Peak、信頼区間やばらつき。
- AI: Root Cause Accuracy、Verified Fix率、新規Regression、時間、費用、危険候補率。
- Ops: Error Rate、Availability SLI、Queue lag、Recovery time、Restore Integrity。

## 4. 負例が必須

空Artifact、SHA不一致、署名不一致、外部ツール未実行、旧ログ、権限拒否、ネットワーク失敗、Worker死亡、LLM不正出力、テスト自体の不正な抑制を用意する。

## 5. 公開時の証拠

測定が存在しなければ数値を空欄／TBDにする。実績には必要な環境とサンプル数を添え、他の環境への保証と誤解されないようにする。プロジェクトが合成fixtureの場合は明示する。

既存の検証境界：[THREE_PILLAR_PRECISION_CONTRACT](../../09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md)。
