# [QA] Production Verificationの公開サンプルと実Tool検証

> Type: Feature / Documentation / Verification | Priority: P1 | Pillar: Game QA

## 背景

ゲーム固有の検査カテゴリを受け取る契約の存在だけでは、専門的な検査が自律的に実装・実証済みとはいえない。

## 作業

契約の種類と実際のToolを分類し、公開サンプルで少なくとも一つの本物の検査器の結果がProject/Release判断へ到達することを確認する。

## Acceptance criteria

- [ ] `.dip/production-verification.json`の公開用サンプルを作成。
- [ ] 契約カテゴリごとにContract Only / Implemented / Runtime Verifiedを分類。
- [ ] 実Toolの実行とReport parsingをE2Eで確認。
- [ ] 別SHA・空Artifact・内容不正・Tool不在をPASSにしない。
- [ ] 未実行カテゴリを未評価として表示。

## 関連設計

`docs/10-future/rfcs/RFC-004-PRODUCTION-QA-CONTRACTS.md`
