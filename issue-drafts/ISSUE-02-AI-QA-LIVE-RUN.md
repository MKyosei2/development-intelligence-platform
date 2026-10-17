# [AI QA] 実LLM APIを使用した修正候補→隔離検証の実証

> Type: Verification | Priority: P0 | Pillar: Game QA

## 背景

LLMインターフェースやスタブテストと、実APIが生成する修正候補がVerifiedFix経路を通ることは別の検証対象。

## 作業

許諾済みの小さなバグを用意し、opt-inで実APIを呼び出し、提案・検証・Human Reviewまでの流れを記録する。無検証のmain書込みや秘密情報送信はしない。

## Acceptance criteria

- [ ] Provider、model、設定、対象SHAを秘密情報なしで記録。
- [ ] 実LLM応答のEvidence ID・パス・schemaを検証。
- [ ] 隔離ワークスペースでbaseline / patch / regressionの結果を取得。
- [ ] 失敗／拒否／成功を区別して結果を報告。
- [ ] 人間のレビュー前にmainへ自動マージしない。
- [ ] 再現手順をドキュメント化。

## 関連設計

`docs/10-future/rfcs/RFC-002-LLM-VERIFIED-FIX.md`
