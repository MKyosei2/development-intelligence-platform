# Reviewer Brief

DIPはC# Backend/Web/Worker、Python分析、C++ Collector、CI/CD、PostgreSQL-backed durable queueを一つのDevelopment Intelligence製品として統合しています。

現在はQA結果を単に表示するのではなく、Finding lifecycle、Flaky reliability、Regression baseline、Ownership、期限付きWaiver、QA Policy / Release Gate、Evidence provenance、QA self-health、technical-debt trendまで接続しています。PM/プロデューサーはProduct FailureとQA Infrastructure Failureを分けた上で、GO / REVIEW REQUIRED / NO-GOとそのEvidenceを確認できます。

書類選考ではローカル実行を要求しません。READMEのGIF、Screenshots、Architecture、固定済みEvidenceだけで製品の主要範囲を確認できます。

実際にRuntimeを確認する場合も、別のDemo版や簡易Backendへ切り替えません。READMEのPowerShell 1行からhost-native runtimeを起動し、PostgreSQL / Python Analysis / Dip.Api / Dip.Worker / Dip.Webの応答確認が完了した場合だけブラウザを開く構成です。外部queue daemonおよびDockerはsupported runtimeの必須依存ではありません。ダウンロードした`.cmd` / `.ps1`を選考担当者に実行させることは前提にしていません。

実装済みであることとruntime検証済みであることは分離しています。正式な説明では、exact revisionのverification artifactがある項目だけをruntime-verifiedとして扱います。
