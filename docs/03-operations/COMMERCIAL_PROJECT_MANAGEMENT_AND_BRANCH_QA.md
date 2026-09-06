# 商用Project Management + Branch QA

この文書では、DIP Project Control CenterとBranch Quality Centerの運用contractを定義する。

## 1. PMはQA閲覧者ではなくProject Manager

PM向けsurfaceでは、Project Managerがdelivery loop全体を制御できる必要がある。そのためDIPでは次をfirst-classなProject controlとして扱う。

1. Plan / Work Breakdown: Team、Workstream、Task、estimate、remaining work、ownership。
2. Schedule: planned date、forecast finish、delay、slack、critical path。
3. Milestone: linked task progress、due date、blocked state、readiness。
4. Dependency: predecessor/successor、cross-workstream dependency、cycle detection。
5. Blocker: direct/downstream impact、age、必要なmanagement action。
6. Risk Register: probability、impact、exposure、trigger、mitigation、owner、review date。
7. Decision Log: decision owner、due date、rationale、related work、open follow-up action。
8. Action Management: priority、owner、due date、completion、overdue state。
9. Scope Control: baseline、included/deferred requirement、scope growth、未完了high-priority requirement。
10. Change Control: engineering/art/QA effort delta、schedule delta、risk delta、decision linkage。
11. Capacity / Resource Allocation: team/workstream load、buffer、明示的reallocation proposal。
12. RACI: Responsible/Accountable/Consulted/Informed roleとmissing-accountable detection。
13. Approval: required role、separation of duties、due date、response、overdue state。
14. Quality: Project operational QAとbranch-scoped promotion Evidence。

Project Control Centerはexecutive/read-and-decide surfaceである。Durableな編集は既存Management Workspaceとmanagement APIを通すことで、dashboard専用の第二Project modelを作らず、DIP内のSource of Truthを一つに保つ。

## 2. GUIの役割

### Project Control Center（プロジェクト統制画面）

Route:

- `/dashboard/{projectId}/project-control-center`
- `/dashboard/{projectId}/pm`

Raw JSON artifactを開かなくても、次へ答えられることを要件とする。

- Projectは計画どおりか。
- Forecast finishとcurrent delayはどれくらいか。
- Critical path上に何があるか。
- どのMilestoneがrisk/blocked状態か。
- Capacity overloadはどこか。
- Deliveryを脅かすBlocker/Riskは何か。
- OverdueなDecision/Actionは何か。
- 判断待ちのScope Changeは何か。
- Dependency cycleは存在するか。
- RACI/accountabilityとApprovalは揃っているか。
- Default branch quality gateはpromotion可能か。
- 今日PMが判断すべき事項は何か。

### Management Workspace（管理Workspace）

Route:

- `/dashboard/{projectId}/management`

Team、Workstream、Task、Dependency、Milestone、Blocker、Requirement、Risk、Decision、Action、Resource Allocation、Scope Change、RACI、Role Binding、Calendar、Approvalを作成・更新するdurable command surfaceとして維持する。

### Branch Quality Center（Branch品質画面）

Route:

- `/dashboard/{projectId}/qa/branches`
- `/dashboard/{projectId}/branch-quality`

Git working treeまたはGitHub Project provenanceからDIPが確立できる全Branchを表示する。Archive-backed GitHub workspaceでは、default branch、open pull-request head、同期snapshot、保存済みDIP branch/pipeline historyを自動統合する。Manual branch fieldから他のvalid branchも直接検証できるため、QA前にopen PRを必須としない。表示項目は次のとおり。

- 正確なbranch HEAD SHA
- remote/local provenance
- default branchに対するahead/behind
- 最新branch QA runと経過時間
- branch QA Evidenceに紐付くopen critical/warning issue
- `Ready`、`Hold`、`Blocked`、`QA required` promotion gate
- 選択branchでisolated QAを実行する直接Action

## 3. Branch QA分離Contract

DIPはshared synchronized project workspace上で`git checkout`を**実行してはならない**。

Branch QA runでは、次の2つのisolation pathのいずれかを選ぶ。

**Git working-tree source**

1. Gitでbranchをvalidate。
2. 必要に応じてbounded remote refreshを実行。
3. `origin/<branch>`を優先し、次にlocal branchをresolve。
4. Explicit commitが指定された場合はrequested commit ancestryを確認。
5. exact revision向けに`git worktree add --detach`を作成。
6. QA実行後、成功/失敗に関係なくdetached worktreeを削除。

**GitHub archive-backed source（標準GitHub repository sync path）**

1. ArchiveをGit checkoutとみなさず`.dip-sync.json` provenanceを読む。
2. Fail-closedなportable ref ruleでrequested branch nameをvalidate。
3. 構成済み`IGitHubRepositoryCatalog`（GitHub App/token credential）を使い、requested branchをrun固有temporary workspaceへdownload。
4. GitHubが返すexact branch-head SHAを記録。
5. 明示的SHA指定がdownload済みbranch headと一致しない場合は拒否。
6. Isolated branch snapshotに対し、language adapter、cross-cutting pack、media check、reproductionを実行。
7. Failure pathを含めrun後にsnapshotを削除。

両pathとも実branch名を`PipelineRun.Branch`、exact SHAを`PipelineRun.CommitSha`へ保存し、branch/ref/isolation provenanceをQA summaryと`qa-intelligence.json`へ記録する。Shared synchronized project workspaceをcheckout/overwriteしない。

これにより、並行QAがdeveloper/shared checkoutを変更することや、あるbranchのEvidenceを別branchのEvidenceとして誤labelすることを防ぐ。

## 4. Promotion Gate Policy（昇格Gate方針）

Branch promotionはfail-closedとする。

- **QA required**: branch-scoped Evidenceなし。Promoteしない。
- **Blocked**: 最新branch QAがfailed/timed out/cancelled、またはbranchにopen critical issue。Promoteしない。
- **Hold**: Evidenceがstale/partial、またはwarningあり。Promotion前にtriage/approve。
- **Ready**: 最新branch QAが成功・freshで、branch QA runに紐付くopen critical/warning issueがない。

`Ready`はtechnical Evidenceであり、自動merge authorizationではない。Human review、approval、scope、release policyは引き続き適用する。

## 5. Default BranchのRelease Truth

Commercial release gateはProjectのconfigured default branchへscopeする。Feature branch上のP0はProject QA operations queueに表示し続けるが、current default-branch revisionが壊れているEvidenceとして黙って扱わない。

逆に、clean feature branchもdefault branchがreadyであるEvidenceにはならない。Default branch自身のbranch-scoped QA Evidenceが必要である。

Branchが`qa-workspace`として保存されたlegacy QA runはprovenanceが曖昧なため、default-branch Evidenceへ自動昇格しない。

## 6. Remote Freshness（Remote情報の鮮度）

Branch freshnessはworkspace typeに応じて明示する。

- `Fetched`: このrequestでGit refをoriginからrefresh。
- `Cached`: refreshなし、またはrefresh失敗後にGit working treeのcached refを使用。
- `GitHubApi`: authenticated GitHub metadata + DIP historyからarchive-backed branchをdiscover。
- `GitHubArchive`: freshなauthenticated branch snapshotとexact branch-head SHAをdownloadしてQA実行。
- `Unavailable`: branch identity/catalogを確立できない。

Git fetch failureをfresh remote Evidenceとして表示しない。またarchive-backed QAではlocalで計算できないahead/behind countを主張しない。

## 7. Source of TruthとAIの境界

- Git commit SHA + branch ref + tool output + reproduction artifactがEvidence。
- `PipelineRun`がbranch QAのdurable branch/evidence boundary。
- Project management stateは既存Development Graph / management commandから保存。
- AI/debug hypothesisはexperimentがobjective Evidenceを生成するまでhypothesisのまま。
- PM recommendation textはdecision supportであり、approvalや作られたProject factではない。

## 8. 運用Workflow

### Feature Branch（機能Branch）

1. Developer/QAがBranch Quality Centerを開く。
2. Feature branchを選び、remote currencyが必要ならrefをrefresh。
3. Branch QAを実行。
4. Quality Operations / QA GovernanceでP0/P1 findingをtriage。
5. Fix後にexact branch revisionを再実行。
6. Branch Evidence、code review、required approval policyを満たしてからmerge。

### Release / Default Branch（Release / 既定Branch）

1. PMがProject Control Centerを開く。
2. Schedule、critical path、milestone、blocker、risk、decision、scope、capacity、approvalを確認。
3. Branch Quality Centerからdefault/release branch headを確認。
4. Warningをresolveまたは明示的disposition。P0はfail-closedのまま。
5. QA PASSしたexact SHAとpromotion対象SHAが一致することを確認。
6. 既存approval/decision systemからrelease/approval decisionを記録。

## 9. 検証

実行:

```powershell
python tools/validate_pm_branch_qa.py
```

.NET SDKとProject dependencyがある環境では続けて:

```powershell
dotnet build
dotnet test
```

GUI verification:

```powershell
cd tests/Dip.UiE2E
npm ci
npx playwright test tests/pm-branch-control.spec.mjs tests/commercial-quality-operations.spec.mjs
```
