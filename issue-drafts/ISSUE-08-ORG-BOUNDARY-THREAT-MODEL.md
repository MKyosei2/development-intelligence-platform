# [Platform Security] 組織・プロジェクト境界の脅威モデル作成

> Type: Research / Security | Priority: P1 | Pillar: Cross-cutting

## 背景

個人向けのローカル利用と、複数組織での商用利用では必要なデータ隔離・監査・保護レベルが異なる。

## 作業

利用形態の候補を比較し、Tenant/Project/Auth/Artifact/Worker/Logsごとに脅威と必要な検証を明確にする。実装方式の採否は後続ADRで判断する。

## Acceptance criteria

- [ ] 役割別の操作表（RBAC）と対象資源の階層を文書化。
- [ ] ID直接参照、検索、キャッシュ、Artifact、Worker等の横断アクセスの負例を列挙。
- [ ] ログ・AI外部送信での機密データの保護範囲を定義。
- [ ] 論理分離／独立インスタンス等の代替案を比較。
- [ ] 採用する設計と残るリスクを明示。

## 関連設計

`docs/10-future/architecture/ARCH-002-MULTI-TENANCY.md`
