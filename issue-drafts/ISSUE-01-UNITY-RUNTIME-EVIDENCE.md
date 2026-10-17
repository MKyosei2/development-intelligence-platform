# [QA] Unity実EditorのEditMode/PlayMode/Build検証Evidenceを追加

> Type: Feature / Verification | Priority: P0 | Pillar: Game QA  
> これはIssue登録用の草案。番号や担当、Milestoneは未設定。

## 背景

Unity Adapter/CollectorのソースとEditor契約テストは、実Unity上でPlayMode/Buildが正しく動いた証拠とは限らない。

## 作業スコープ

- 公開許可のある小規模Unity検証プロジェクトを固定する。
- 対象ゲームSHA、DIP SHA、Unity version、OS、テストcommandを記録する。
- 実EditorでEditMode、PlayMode、Buildを実行する。
- Test FailureとEditor不足／Infrastructure Failureを明確に区別する。
- 結果・ログ・ArtifactをProject Truthへ通す代表ケースを追加する。

## Acceptance criteria

- [ ] 実Editorの実行証拠が再現可能な形式で残る。
- [ ] 別SHAのログや古いログがCurrent PASSにならない。
- [ ] ゲームのQA失敗と環境不足が別のステータスになる。
- [ ] CIの契約テスト成功を実Editorの成功として表示しない。
- [ ] 実行手順・制約・対象環境がドキュメント化される。

## 関連設計

`docs/10-future/rfcs/RFC-001-REAL-ENGINE-QA.md`
