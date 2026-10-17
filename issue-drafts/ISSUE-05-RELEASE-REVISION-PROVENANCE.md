# [Release] Release Evidenceの改訂版・署名・Artifact整合性を強化

> Type: Verification / Security | Priority: P0 | Pillar: Verification

## 背景

必要なEvidenceがあるという情報だけでなく、選択したリリースSHAと、生成物の来歴が一致していることを確認する必要がある。

## 作業

Release Readiness、Project Truth、Evidence一覧、SBOM/Provenanceの判定条件を監査し、古い結果、異なるSHA、不正Artifactを混ぜたケースで判定を検証する。

## Acceptance criteria

- [ ] リリース候補SHAと各必須Evidenceの整合を確認。
- [ ] 必須Artifact欠損・hash mismatchを成功扱いしない。
- [ ] 旧snapshotや別プロジェクト結果がCurrentへ混入しない。
- [ ] 例外承認できる条件と必ず拒否する条件を文書化。
- [ ] 正常・異常・環境不可のE2Eを追加。

## 関連設計

`docs/10-future/architecture/ARCH-004-EVIDENCE-CHAIN.md`
`docs/10-future/verification/RELEASE-ACCEPTANCE.md`
