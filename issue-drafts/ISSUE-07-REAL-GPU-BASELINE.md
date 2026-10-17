# [Graphics QA] 実GPUを使った画像差分・性能回帰のBaseline

> Type: Verification | Priority: P1 | Pillar: Game QA

## 背景

画像比較とGPU数値解析のコードがあることと、実ゲームのGPU実測を再現できることを区別する。

## 作業

Goldenシーンを固定し、実GPUで画像・フレーム時間・VRAMと必要なキャプチャを取得し、QA結果とSource SHAを関連付ける。

## Acceptance criteria

- [ ] GPU、Driver、OS、API、解像度、Target/DIP SHAを記録。
- [ ] 正常、意図的な描画退行、許容すべき変化を比較。
- [ ] p95/p99とhitchの測定回数・条件を明記。
- [ ] キャプチャの不足・不一致・古さを検出。
- [ ] 差分の誤検知・見逃しと限界を説明。

## 関連設計

`docs/10-future/rfcs/RFC-003-GPU-VISUAL-VERIFICATION.md`
