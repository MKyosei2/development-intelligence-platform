# 将来のサービス構想

**ステータス: 構想段階。現在提供している商用サービスの機能一覧ではありません。**

## 想定する進化

1. 個人開発者がローカルで導入・検証できる基盤。
2. 複数人の制作チームがIssue・QA・進捗を共通Evidenceで確認できる運用。
3. 組織を跨ぐ開発で認証、権限分離、監査、秘密情報管理を備える環境。
4. 24時間365日のサービス提供に必要な可観測性、障害対応、復元試験、SLOを整備した運用。

これらは段階的に実証すべき目標です。企業向け要件と個人向けの使いやすさの間には費用・運用負担のトレードオフがあります。

## 確認すべきリスク

- 不信頼コードをQAとして実行する際の安全な隔離
- 顧客のソース・画像・音声・ゲーム素材の機密性と権利
- 複数組織間のアクセス権限とデータ隔離
- データ・Artifactの保存費用とLLM利用費用
- APIやWorker障害、データベース復元、リリースのRollBack
- ユーザーの期待と実際のQAカバレッジの違い

## 設計資料

- [24/365運用アーキテクチャ](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/architecture/ARCH-001-24X365-OPERATIONS.md)
- [マルチテナンシー検討](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/architecture/ARCH-002-MULTI-TENANCY.md)
- [ジョブ隔離](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/architecture/ARCH-003-JOB-ISOLATION.md)
- [企業・個人向け要求](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/requirements/PRD-002-ENTERPRISE-AND-CREATOR.md)
- [SLO・運用案](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/operations/SLO-RUNBOOK.md)
