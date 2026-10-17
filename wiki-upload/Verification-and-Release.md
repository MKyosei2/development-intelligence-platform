# Verification / Release Operations

DIPのリリース支援は、ソフトウェアを「公開できそう」という印象だけで判断せず、**その改訂版に必要なテストと証拠が存在するか**を確認する考え方です。

## 判定に必要なもの

- 対象となるコードのrevision（SHA）
- 必須CIとQAの成功・失敗・未評価
- ベンチマークや性能検証の環境・数値・記録
- 未解決の重大な不具合やセキュリティ上の懸念
- 生成した配布物のハッシュ・来歴
- リリース承認や復旧の条件（将来の製品化）

## 成功と未評価を区別する

`PASS`、`FAIL`、`NOT_EVALUATED`、`BLOCKED`を区別します。ツールが起動できなかったことをゲームの不具合とは断定せず、逆に成功とも扱いません。

## ポートフォリオと将来の商用リリースは別

現行のポートフォリオに必要な同一SHAのCI／Benchmark／Bug Zoo／Project Truthの検証と、将来の商用SLO、障害復旧、セキュリティ、顧客サポートは別の完成条件です。

- [現行の三本柱契約](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md)
- [将来のリリース受け入れ条件](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/verification/RELEASE-ACCEPTANCE.md)
- [検証戦略](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/verification/VERIFICATION-STRATEGY.md)
