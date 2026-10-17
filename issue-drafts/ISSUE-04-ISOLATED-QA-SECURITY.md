# [Security] 不信頼QA実行の隔離とfail-closedを検証

> Type: Security / Verification | Priority: P0 | Pillar: Verification

## 背景

第三者のコードを実行できる環境に、ホストアクセスやネットワークの制約が適切に掛かっていることが重要。

## 作業

現行Process/Sandbox実装と設定を確認し、信頼していないコードの実行で禁止領域にアクセスできないことを負例で実証する。Runner不在時のホスト降格について、用途・方針・挙動を明確にする。

## Acceptance criteria

- [ ] 実際に使用される実行経路をトレースできる。
- [ ] 禁止ファイル・秘密情報・外部ネットワークへのアクセスを拒否。
- [ ] Runner未設定時に無条件で安全と表示しない。
- [ ] Timeout/Cancel/Worker停止時にプロセスが残留しない。
- [ ] 設定・環境・検証Evidenceを記録。

## 関連設計

`docs/10-future/architecture/ARCH-003-JOB-ISOLATION.md`
