# Reviewer Brief

DIPはC# Backend/Web/Worker、Python分析、C++ Collector、CI/CD、PostgreSQL、Redis、Dockerを一つのDevelopment Intelligence製品として統合しています。

現在はQA結果を単に表示するのではなく、Finding lifecycle、Flaky reliability、Regression baseline、Ownership、期限付きWaiver、QA Policy / Release Gate、Evidence provenance、QA self-health、technical-debt trendまで接続しています。PM/プロデューサーはProduct FailureとQA Infrastructure Failureを分けた上で、GO / REVIEW REQUIRED / NO-GOとそのEvidenceを確認できます。

実装済みであることとruntime検証済みであることは分離しています。正式な説明では、exact revisionのverification artifactがある項目だけをruntime-verifiedとして扱います。
