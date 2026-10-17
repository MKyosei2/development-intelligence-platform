# 技術設計書・RFC・ADRの読み方

> **文書種別:** 技術解説への入口。本文中の将来計画は実装・実機検証・商用運用の達成を意味しません。

## ドキュメントの役割
- **Wiki:** 技術に不慣れな人にも目的が分かる説明。
- **現行詳細設計:** `docs/02-architecture/`等で、現行ソースの責務と未検証部分を示す。
- **RFC / 将来ARCH:** 将来採用したい設計案。まだ実装済みとは限らない。
- **ADR:** 採用済みの技術的意思決定とその理由。
- **Evidence:** 具体的なSHA/Run/Tool/Artifactに対する実際の確認結果。

## スタート地点
- [新しい詳細技術設計書インデックス](https://github.com/MKyosei2/development-intelligence-platform/blob/work/future-product-documentation/docs/TECHNICAL-DESIGN-INDEX.md)
- [既存ADRの一覧](https://github.com/MKyosei2/development-intelligence-platform/tree/main/docs/02-architecture/architecture-decisions)
- [将来構想資料（提案中）](https://github.com/MKyosei2/development-intelligence-platform/blob/work/future-product-documentation/docs/10-future/00-INDEX.md)
- [既存実装状況](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/IMPLEMENTATION_STATUS.md)

## 技術分野別のWiki
[システム構成](System-Architecture) / [AI QA](AI-QA-Architecture) / [QA対応範囲](QA-Capability-Matrix) / [Evidence](Evidence-Lifecycle) / [運用・デプロイ](Deployment-and-Operations)

## 安全な更新
仕様の採否・実装・Runtime Verifiedは別状態として扱う。正式設計を変更するときはGitのPRと同一SHAのCIを確認し、Wikiは更新箇所へのリンクを追加する。技術に変更があった際の単なる記事更新を、製品の検証成功と混同しない。
