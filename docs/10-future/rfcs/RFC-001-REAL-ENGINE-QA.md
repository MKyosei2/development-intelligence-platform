# RFC-001: Unity / Unrealの実エンジンQAをEvidenceとして統合

**Status: Proposed** | **Area: Game Development QA** | **Decision owner: 未設定**

## 問題

CollectorやAdapterが存在することと、実Unity/Unreal EditorまたはPlayer上で検査を完了したことは別である。実ゲームの検証結果を、DIP自体のリビジョンとゲームのリビジョンへ同時に関連づける必要がある。

## 提案

1. 許諾済みの小規模なUnity検証プロジェクトを固定し、対象Git SHAを記録する。
2. Unityの実Editorを起動して、EditMode、PlayMode、Build、重要なシーンのsmoke検査を実施する。
3. Engine version、OS、命令、exit code、ログ、生成レポート、Artifact hash、DIP SHA、target SHAを保存する。
4. `Test Passed / Test Failed / Tool Unavailable / Infrastructure Failed / NOT_EVALUATED`を区別する。
5. データ投入後のQuality Gate、Project Truth、Release Readinessへの影響をE2Eで確かめる。
6. Unityで安定させた後、UnrealのAutomation、Blueprint検証、Cook/Build/Packageへ段階的に拡張する。

## 既存実装との接点

- `src/Dip.Infrastructure/QualityAutomation/UnityProjectQaAdapter.cs`
- `src/Dip.Infrastructure/QualityAutomation/UnrealProjectQaAdapter.cs`
- `src/Dip.Infrastructure/QualityAutomation/GameProjectQaPack.cs`
- `collectors/unity-collector/` および `collectors/unreal-collector/`
- `docs/09-portfolio/runtime-verification.md`

**注意:** これらの存在自体を実エンジンでの成功証拠にしない。Adapter間の重複実行・過去ログの混入・ソースSHA不明のログも設計レビュー対象とする。

## Alternative

- Editor契約テストのみ：CI導入は容易だが実ゲームの実動作を証明できない。
- 実ゲームを最初から複数用意：適用範囲は広がるが、再現性・メンテナンスが難しい。

## Acceptance criteria

- [ ] 実Editor／Playerのバージョンと実行環境を保存する。
- [ ] 同一revisionの正常／不具合サンプルを識別できる。
- [ ] 標準の検査結果と必要なArtifactを再現可能に取得する。
- [ ] 過去ログ／別SHA／実行未完了のケースはPASSにしない。
- [ ] UnityとUnrealの**実測済み**範囲を別々に示す。
- [ ] DIPの最終ポートフォリオゲートと実Engineテストの任意性・必須性を明確に区別する。

## 未決定事項

サンプルゲームのライセンス、検証環境の提供形態、GUIテストの運用費用、Unity/Unrealバージョンマトリクス。
