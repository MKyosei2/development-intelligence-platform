# 受け入れ基準

1. Projectを登録できる
2. Collector Reportを送信できる
3. PostgreSQLへ保存される
4. FPS -10%またはMemory +20%でFailする
5. DashboardにProjectが表示される
6. Risk APIがScoreとContributionを返す
7. CIでUnitとIntegration Testが通る
8. Native-first runtimeでPostgreSQL / Python Analysis / API / Worker / Webが起動し、health / readinessを検証できる
9. Docker / external queue daemonがなくてもformal Native verificationの主要経路を実行できる
