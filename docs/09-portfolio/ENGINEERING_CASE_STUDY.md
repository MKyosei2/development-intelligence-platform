# Engineering Case Study（エンジニアリング・ケーススタディ）

この文書は、DIPが単なる機能一覧ではなく、ゲーム・ソフトウェア制作の**Engineering Project**である理由を技術reviewerが短時間で確認するための読み順である。

## 1. 問題設定

Game/software production dataは分散している。Git history、CI、test、engine/runtime event、performance measurement、incident、project-management reportは、それぞれschema、clock、failure mode、trust levelが異なる。Dashboardへ集約できても、すべてのsourceを同じauthorityとして暗黙に扱えば危険な意思決定につながる。

DIPでは、**provenance（出所）とverification stateをfirst-class dataとして扱う**。Claimは人またはsystemが報告した内容、Evidenceはsource / revision / timeへ結び付いた観測可能な結果である。人物へscoreを付けるのではなく、Claimとtechnical Evidenceの関係を評価する。

## 2. Game Development Scenarioを先に見る

DIPのゲーム開発での代表経路は次である。

```text
Unity / Unreal project
        ↓
Engine-aware Collector / QA Adapter
        ↓
Asset・Build・Scene・Runtime・Performance Evidence
        ↓
Project Context / Evidence provenance
        ↓
Project Truth + Quality Gate
        ↓
Release decision / PM・Lead review
```

Unity側では、Asset QA、Build Settings、Scene / Reference、Animation、Runtime Performanceを同一Reportへ統合できる。DIP側ではengine固有のpayloadをそのままtruthへ変換せず、provider/revisionを保持したEvidenceとしてingestする。Criticalなmissing referenceやruntime failureがある場合、完了Claimと矛盾するEvidenceとして扱い、Quality Gate / Project Truthへ反映できる。

Unrealやnative toolchainも同じProject / Evidence boundaryへ接続する。重要なのは「UnityとUnrealを同一schemaへ無理に潰す」ことではなく、provider固有detailを残しながら、release判断に必要なstable fieldだけを共通Evidenceへ昇格することである。

GitHub-hosted E2Eでは実Unity Editorを必須にしない。`GameDevelopmentEvidenceScenario`は、Unity Collectorが生成した後に相当するnormalized EvidenceのingestionからProject Truth / Quality Gateまでを自動検証する。実Unity Editorを含むclaimは`Verify-UnityCollector.ps1`を使うformal runtime verificationへ分離する。これによりcross-platform CIの再現性とengine-runtime Evidenceの正直さを両立する。

詳細は`GAME_DEVELOPMENT_SCENARIO.md`を参照。

## 3. 技術判断: Provider-neutral Evidence

Core boundaryは「GitHub data」「Unity data」ではなく、external providerが正規化して入るEvidence contractである。

理由:

- GitHub、Unity、Unreal、Maya、native runtime、CIを追加してもProject Truth domainへprovider固有schemaを埋め込まずに済む。
- Provider failureとProduct failureを分離できる。
- Evidenceがtrace不能なdashboard数値になるのではなく、revision / provenanceを保持できる。

Trade-off:

- Normalizationにschema / adapter complexityが増える。
- Provider-neutral modelがlowest-common-denominator abstractionになる危険がある。

Mitigation:

- Provider固有payload / artifactを保持しつつ、stableかつdecision-relevantなfieldだけをcommon Evidenceへ昇格する。
- 曖昧なcross-source matchingはsilent mergeせずfail-closedとする。

## 4. Engineering Decision: PostgreSQLをTruthとCoordination Boundaryにする

DIPはdurable business stateとdurable queue-delivery stateをPostgreSQLへ置きつつ、delivery mechanicsはprovider-neutral interfaceの背後に保つ。

- PostgreSQL: durable project / evidence / job stateとqueue-delivery state。
- `IQueueClient`: provider-neutral delivery boundary。
- `PostgresQueueClient`: lease、acknowledgement、retry、dead-letter実装。
- Worker: asynchronous ingestion / analysis execution。
- Python service: 隔離されたanalysis capability。

Queue messageをauthoritative business stateとして扱わない。Delivery recordを明示的に永続化し、「messageが消えたからworkが完了した」と仮定せず、state、lease、acknowledgement、retry、recoveryとしてjobを追跡する。CoordinationをPostgreSQLへ集約することで必須runtime dependencyを減らす一方、DB loadは増えるためmeasurement / capacity planningが必要になる。

## 5. 技術判断: Exact-revision Evidence

Portfolio / CIでは、historical green badgeやbenchmarkを見せてcurrent sourceも同じ状態だと暗示しやすい。DIPはこの仮定を拒否する。

> 新しいrevisionは、古いrevisionのPASSを継承しない。

Historical benchmark / QA Evidenceはbaselineとして有用だが、current sourceをVerifiedと説明するには、そのrevision自身がrequired workflow / runtime checkを通過する必要がある。sample-data screenshotとruntime Evidenceを分離している理由も同じである。

## 6. 技術判断: 明示的なProject Truth Policy

Project Truthでは、small portfolio datasetに対しMLが最適だと見せかけるより、説明可能性を優先してdeterministic heuristicを使う。Evaluator内のthresholdは`ProjectTruthPolicy`へ集約し、injectionする。

利点:

- 散在するmagic numberを減らせる。
- Defaultでbackward compatibilityを保てる。
- Evaluator codeを変更せずthresholdをversion / calibrateできる。
- Testでboundary behaviorを直接確認できる。
- 将来deploymentではpolicy changeをEvidence / reviewへ紐付けられる。

Default valueは保守的なhistorical defaultであり、**統計的に最適だと証明された値ではない**。Productionではlabeled historical decisionでcalibrateし、hold-out validation setを維持する必要がある。`PROJECT_TRUTH_POLICY.md`参照。

## 7. Measured Baselineが示すもの

Historical Evidenceには次の実測baselineが記録されている。

- Project list p95: 4.174 ms
- Collector p95: 34.507 ms
- 100 req/s error rate: 1,500 requestsで0.0000%
- 1M Metric query p95: 363.796 ms
- QA Bug Zoo recall: 19/19
- clean-control false positives: 0/7
- reproducibility: 9/9

これらは特定のverified commitについてmeasurement infrastructureが結果を生成したことを示す。以後のすべてのcommitが同じ結果だと主張するものではない。公開対象のfinal SHAは`benchmark-results.md`のruleに従って別途確認する。

## 8. Failure Model（失敗モデル）

- Provider ID / revisionがあるingestionはidempotentであるべき。
- Infrastructure failureをProduct failureへlabelし直さない。
- 利用できないCI runnerをPASSとしない。
- AI / Fix Agent suggestionはisolated verification成功までadvisory。
- 対応runtimeなしにengine / device claimをVerifiedとしない。
- Native-first formal verificationはDockerの存在を成功条件にしない。

Production Engineeringではhappy pathだけでなくpartial failure時の挙動が重要なため、この区別自体をreviewerへ見せる。

## 9. Portfolioとしての位置付け

DIPと特に相性がよい領域:

- Game development infrastructure / production engineering
- Backend / platform engineering
- QA automation / developer productivity
- CI/CD、release engineering、observability
- Engine / DCC Evidenceを扱うTechnical Artist / Technical Director tooling

Gameplay / rendering showcaseとしては位置付けない。該当職種では、gameplay、graphics、simulation codeを直接示すProjectと組み合わせる。
