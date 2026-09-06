# DIP Playwright GUI E2E

DeterministicなLocal Test Suiteでは、外部GitHub credentialを必要とせず、通常のGUI onboardingを検証します。

Hosted Public Browser Suiteでは、実際に公開されたPortfolio URLそのものを検証します。

- HTTPS / API healthへ到達できる
- Portfolio pageがLoginなしで描画される
- UI上で「live read-only public view」であることを明確に示す
- Project readが匿名で成功する
- 匿名でのProject作成が401 / 403で拒否される
- 公開済みProjectが存在する場合、そのProjectを開ける

すでにDeploy済みのDIP URLに対してHosted checkを実行する例:

```bash
DIP_WEB_BASE_URL=https://your-host.example npm run test:public
```

`Live Browser Deployment` は、対象となる正確なGit commitをDeployした後、このSuiteを自動実行します。Browser checkが失敗した場合はDeployment jobも失敗し、直前のimage-setへ戻すRollback Pathを起動します。

任意の `@live-github` Testは別扱いです。これは匿名Reviewer Pathではなく、認証済みGitHub App Integrationを検証するためです。
