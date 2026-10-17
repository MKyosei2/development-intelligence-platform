# DIP 将来設計・製品仕様インデックス

> Status: Proposed / Documentation Draft  
> 対象: 現行DIPの**将来計画**。実装・検証・製品提供を保証する文書ではない。  
> 基準: 現在の事実はコード、対象SHAのCI、ランタイムEvidenceを優先する。

## このフォルダの目的

DIPの目指す製品像、要求、候補アーキテクチャ、検証条件、段階的ロードマップを、現在のソース実装と区別して管理する。

| 確認したいこと | 文書 |
|---|---|
| どんな製品にするか | [01-PRODUCT-VISION](01-PRODUCT-VISION.md) |
| 何が現在の実装で何が将来構想か | [02-STATUS-BOUNDARY](02-STATUS-BOUNDARY.md) |
| 開発の順番と完了条件 | [03-ROADMAP](03-ROADMAP.md) |
| GitHubの運用・Wikiへの掲載方法 | [04-GITHUB-CONTENT-WORKFLOW](04-GITHUB-CONTENT-WORKFLOW.md) |
| 製品要求 | [requirements/](requirements/PRD-001-PRODUCT-SCOPE.md) |
| システム構成の候補 | [architecture/](architecture/ARCH-001-24X365-OPERATIONS.md) |
| ゲームQAの設計方針 | [qa/](qa/QA-001-COVERAGE-MATRIX.md) |
| 採用前の設計案（RFC） | [rfcs/](rfcs/RFC-001-REAL-ENGINE-QA.md) |
| 検証・リリース判断 | [verification/](verification/VERIFICATION-STRATEGY.md) |
| Issuesへ分割する計画 | [planning/](planning/INITIAL-ISSUE-BACKLOG.md) |

## 既存資料との役割分担

- 現行の製品概要：[docs/00-overview/project-overview.md](../00-overview/project-overview.md)
- 現行の要件：[docs/01-requirements/](../01-requirements/functional-requirements.md)
- 現行の設計：[docs/02-architecture/](../02-architecture/system-context.md)
- 実装状況：[docs/IMPLEMENTATION_STATUS.md](../IMPLEMENTATION_STATUS.md)
- 3つの柱の検証契約：[THREE_PILLAR_PRECISION_CONTRACT.md](../09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md)
- 採用担当者向け実績：[reviewer-brief.md](../09-portfolio/reviewer-brief.md)

## この文書群の運用原則

1. **ProposedはImplementedではない。** 仕様の説明は実装の証明にならない。
2. **ImplementedはRuntime Verifiedではない。** 実環境での検証が別途必要。
3. **Historical PASSはCurrent SHA PASSではない。** Evidenceの対象改訂版を必ず示す。
4. 本番サービスのSLO、費用、容量、可用性の数値は計測前に保証値として記載しない。
5. 採用済みの設計判断だけをADRへ移し、未決定事項はRFCとして残す。
6. PRで仕様が変わったら影響を受ける関連文書・Issueを同時更新する。
