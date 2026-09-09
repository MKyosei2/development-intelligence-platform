# 目標と対象外

## 目標

- 共通Collector Reportを受信する
- MetricとIssueを保存する
- Baseline比較とQuality Gateを実行する
- Project HealthとPR Riskを説明可能に表示する
- Native-first runtimeとCIで、同じProject / Evidence contractを再現・検証する
- Unity / Unreal / native toolchainのprovider-specific EvidenceをProject Contextへ接続する
- Historical PASSとcurrent revision PASSを分離し、verification provenanceを保持する

## 対象外

- GitHubやJiraの完全代替
- AIによる無検証の自動Mergeや人事評価
- 根拠のない納期断定
- gameplay / rendering engineそのものの代替
- 初期版でのKubernetes必須化
- Docker / external queue daemonを正式Native-first runtimeの必須依存にすること
