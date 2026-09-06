# Engineering Case Study（エンジニアリング・ケーススタディ）

この文書は、DIPが単なる機能一覧ではなく**Engineering Projectである理由**を技術reviewerが短時間で理解するための読み順である。

## 1. 問題設定

Game/software production dataは本質的に分散している。Git history、CI、test、engine/runtime event、performance measurement、incident、project-management reportは、それぞれschema、clock、failure mode、trust levelが異なる。Dashboardへ集約できても、すべてのsourceを同じauthorityとして暗黙に扱えば危険な意思決定につながる。

そこでDIPでは、**provenance（出所）とverification stateをfirst-class dataとして扱う**。Claimは人またはsystemが報告した内容、Evidenceはsource/revision/timeへ結び付いた観測可能な結果である。人物へscoreを付けるのではなく、両者の関係を評価する。

## 2. 技術判断: Provider-neutral Evidence

Core boundaryは「GitHub data」「Unity data」ではなく、external providerが正規化して入るEvidence contractである。

理由:

- GitHub、Unity、Unreal、Maya、native runtime、CIを追加してもProject Truth domainへprovider固有schemaを埋め込まずに済む。
- Provider failureとProduct failureを分離できる。
- Evidenceがtrace不能なdashboard数値になるのではなく、revision/provenanceを保持できる。

Trade-off:

- Normalizationにschema/adapter complexityが増える。
- Provider-neutral modelがlowest-common-denominator abstractionになる危険がある。

Mitigation:

- Provider固有payload/artifactを保持しつつ、stableかつdecision-relevantなfieldだけをcommon Evidenceへ昇格する。
- 曖昧なcross-source matchingはsilent mergeせずfail-closedとする。

## 3. Engineering Decision: PostgreSQLをTruthとCoordination Boundaryにする

DIPはdurable business stateとdurable queue-delivery stateをPostgreSQLへ置きつつ、delivery mechanicsはprovider-neutral interfaceの背後に保つ。

- PostgreSQL: durable project/evidence/job stateとqueue-delivery state。
- `IQueueClient`: provider-neutral delivery boundary。
- `PostgresQueueClient`: lease、acknowledgement、retry、dead-letter実装。
- Worker: asynchronous ingestion/analysis execution。
- Python service: 隔離されたanalysis capability。

Queue messageをauthoritative business stateとして扱わない。Delivery recordを明示的に永続化し、「messageが消えたからworkが完了した」と仮定するのではなく、state、lease、acknowledgement、retry、recoveryとしてjobを追えるようにする。CoordinationをPostgreSQLへ集約することで必須runtime dependencyを減らす一方、DB loadは増えるためmeasurement/capacity planningが必要になる。

## 4. 技術判断: Exact-revision Evidence

Portfolio/CIでは、historical green badgeやbenchmarkを見せてcurrent sourceも同じ状態だと暗示するミスが起きやすい。DIPはこの仮定を明示的に拒否する。

Verification rule:

> 新しいrevisionは、古いrevisionのPASSを継承しない。

Historical benchmark/QA Evidenceはbaselineとして有用だが、current sourceをVerifiedと説明するには、そのrevision自身がrequired workflow/runtime checkを通過する必要がある。READMEでsample-data screenshotとruntime Evidenceを分離している理由も同じである。

## 5. 技術判断: 明示的なProject Truth Policy

Project Truthでは、small portfolio datasetに対しMLが最適だと見せかけるより、説明可能性を優先してdeterministic heuristicを使う。以前evaluator内に直接埋め込まれていたconstantは`ProjectTruthPolicy`へ集約し、injectionする。

利点:

- 散在するmagic numberをなくせる。
- Defaultでbackward compatibilityを保てる。
- Evaluator codeを変更せずthresholdをversion/calibrateできる。
- Testでboundary behaviorを直接確認できる。
- 将来deploymentではpolicy changeをEvidence/reviewへ紐付けられる。

重要な制約:

Default valueは保守的なhistorical defaultであり、**統計的に最適だと証明された値ではない**。Productionではlabeled historical decisionでcalibrateし、hold-out validation setを維持する必要がある。`PROJECT_TRUTH_POLICY.md`参照。

## 6. Measured Baselineが示すもの

Historical Evidenceには、たとえば次の実測値が記録されている。

- Project list p95: 4.174 ms
- Collector p95: 34.507 ms
- 100 req/s error rate: 1,500 requestsで0.0000%
- 1M Metric query p95: 363.796 ms
- QA Bug Zoo recall: 19/19
- clean-control false positives: 0/7
- reproducibility: 9/9

これらは、特定のverified commitについてmeasurement infrastructureが実際に結果を生成したことを示す。以後のすべてのcommitが同じ結果だと主張するものではない。

## 7. Failure Model（失敗モデル）

Systemはfailure boundaryを明示する。

- Provider ID/revisionがあるingestionはidempotentであるべき。
- Infrastructure failureをProduct failureへlabelし直さない。
- 利用できないCI runnerをPASSとしない。
- AI/Fix Agent suggestionはisolated verification成功までadvisory。
- 対応runtimeなしにengine/device claimをVerifiedとしない。

Production Engineeringではhappy pathだけでなくpartial failure時の挙動が重要なため、この区別自体をreviewerへ見せる。

## 8. Portfolioとしての位置付け

DIPと特に相性がよい領域:

- Game development infrastructure / production engineering
- Backend/platform engineering
- QA automation / developer productivity
- CI/CD、release engineering、observability
- Engine/DCC Evidenceを扱うTechnical Artist / Technical Director tooling

Gameplay/rendering showcaseとしては位置付けない。該当職種では、gameplay、graphics、simulation codeを直接示すProjectと組み合わせる。
