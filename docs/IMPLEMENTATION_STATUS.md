# 実装状況

この文書では、**実装済みのコード**と**検証済みの完了状態**を明確に分ける。

## 現在のコード状態

| 領域 | コード状態 | 完了を示すEvidence |
|---|---|---|
| Project Truth / Evidence model | P0としてImplemented | Domain/Application tests + live E2E |
| Claim vs Evidence | P0としてImplemented | Claim verification tests + Project Truth E2E |
| Evidence Confidence | P0としてImplemented | Evaluator tests + dashboard observation |
| Evidence-backed / Reported Progress | P0としてImplemented | Progress evaluator tests + Project Truth E2E |
| Risk / Blocker evaluation | P0としてImplemented | Project Truth evaluator tests + live E2E |
| Evidence Timeline | P0 dashboardとしてImplemented | Dashboard query + live data inspection |
| PM-first Blazor dashboard | P0としてImplemented | Dashboard query tests + live API/E2E |
| 既存C# backend / Collector path | Evidence Providerとして維持・拡張 | Unit + integration tests |
| Worker / PostgreSQL-backed queue | Implemented | Live E2E workflow |
| Quality Gate | Evidence Providerの一つとして維持 | Quality unit tests + live E2E |
| Python analysis | 独立FastAPI serviceをImplemented | Python test workflow |
| C++ native collector | `collectors/native-collector`にImplemented | Native collector CI |
| GitHub Actions | Workflowを収録 | Latest commitがgreenになった場合のみComplete |

## P0の完了ルール

Project Truth P0は、source fileが存在するだけでVerifiedと説明してはならない。Resulting repository revisionで`.NET CI`、`Integration and E2E Tests`、Python tests、Native Collector CIがgreenになるか、Windows local verificationが成功して`scripts/Verify-Solution.ps1`がcompletion artifactを作成した場合にのみVerifiedとする。

P0 implementationでは意図的に次の2概念を分離する。

- **Claim**: 人が報告した内容。
- **Evidence**: GitHub、CI、Collector、runtime/quality/performance check、その他のobservable systemが実際に報告した内容。

Platformは人物へtruthfulness scoreを付けない。評価対象は、現在のClaimと利用可能なEvidenceの関係だけである。

## Database互換性

Formal EF Core migration baselineはsourceへ実装済み。
`20260812000100_InitialBaseline`はmigration導入前のDIP schema、`20260812000200_AddDurablePlatformIntelligence`はdurable artifact/job/incident/benchmark schemaを表す。`LegacyMigrationAdopter`はhistorical `EnsureCreated` databaseを検証してからbaseline適用済みとして記録し、互換性のないlegacy schemaはsilent adoptionせずfail-closedとする。以後`DbInitializer`は通常schema evolution pathとして`MigrateAsync`を使用する。

このsource stateは、clean PostgreSQL databaseと代表的なhistorical DIP databaseの両方でtarget .NET/PostgreSQL環境のmigration pathが成功するまで、**live-migration verifiedではない**。

## P1 Technical Evidence拡張

Hardcoded Python/C#/C++ QA、fingerprint-based automatic reproduction、Windows capture/audio/dump helper、hardware calibration、relative performance simulation、PM explanation、Project Truth integrationのP1 source pathを含む。Windows固有native Evidenceを含む`docs/P1_IMPLEMENTATION_STATUS.md`の要件を実行するまで、P1は**Verifiedではない**。

## P2 Predictive Development Intelligence（予測型開発知識）

Source implementationには、Community Hardware aggregation、Minimum Hardware Finder、Adaptive QA Planner、Git regression-origin search、flaky/soak reliability、fuzz/mutation testing、media regression、evidence governance、portability analysis、evidence-grounded root-cause candidate、forecast、PM decision briefを含む。

Completion hardeningではさらに次を行った。

- `Home.razor`と`PerformanceSimulationService`のcurrent `.NET CI` compile blockerを修正。
- Minimum Hardware boundary probeをtarget-defined environment variableへ依存させず、既存のconstrained performance simulation経由へ変更。
- Soakをobservation windowとして扱い、DIP managed heapではなくtarget-process memoryをsample。
- Regression/mutation executionをdisposable Git worktreeへ隔離。
- Low-precision media fallbackをHigh-confidence Evidenceとして保存しない。
- Completion velocityをraw Evidence volumeではなくProject Truth progress historyから算出。
- Recent Community Hardware retryをone-way consent-scoped pseudonymでdeduplicate。
- P2 live API E2E coverageとexecution-safety unit coverageを追加。
- Non-Development API configurationは既定でauthenticated access、Developmentはlocal unauthenticated verification behaviorを明示的に維持。

Target Windows/.NET環境で`scripts/Verify-P2.ps1`がskip switchなしで完了し、そのrepository revisionがCI greenになるまで、P2を**runtime-verifiedと宣言してはならない**。Simulation-derived hardware resultはreal-device Evidenceと明確に分離する。

## 2026-08-11 優先Hardening

| 領域 | コード状態 | 必須Completion Evidence |
|---|---|---|
| QA Bug Zoo evaluator | Implemented | 実Python/C#/C++ QA engineが生成する`artifacts/qa-bug-zoo/qa-bug-zoo-results.json` |
| QA clean controls / false-positive measurement | Implemented | Bug Zoo report |
| Stable-fingerprint re-run measurement | Implemented | Bug Zoo reproduction metric |
| Unity Build Settings QA | Implemented | Unity BatchMode + EditMode Evidence |
| Unity Animation QA | Implemented | Unity BatchMode + EditMode Evidence |
| Unity Scene missing-script/reference QA | Implemented | Unity BatchMode report |
| Unity Runtime PerformanceProbe | Implemented | Real Unity Player/Editor runtime metric |
| Portfolio readiness orchestration | Implemented | non-skipped verification phaseがすべて通過した後の`PORTFOLIO_READINESS_PASS.txt` |

Universal / unknown-language QA adapterはこのhistorical phaseでは延期されていたが、後述する2026-08-14 Universal QA layerで実装済み。

## Durable Platform Intelligence実装 — 2026-08-12

Source implementationには次を含む。

- Formal EF Core baseline + fail-closed legacy adoption。
- Local/MinIO providerとretentionを持つdurable artifact metadata/blob storage。
- Lease、heartbeat、cancellation、retry、recoveryを持つPostgreSQL-backed asynchronous job / queue delivery。
- Durable Fuzz/Mutation/Soak/Git Bisect/Media execution path。
- 既存QA crash fingerprint上のIncident/Occurrence/Event lifecycleとtimeline aggregation。
- Regression-origin、native symbolication、evidence-grounded root-cause incident connection。
- Benchmark definition/run/metric/comparison、durable benchmark job、Benchmark -> Artifact -> Evidence -> Project Truth -> Incident integration。

これらは**sourceへImplemented**であり、runtime-verifiedではない。Runtime completionにはexact resulting revisionで.NET 8 build/test PASS、live PostgreSQL queue delivery、selected Artifact Store verificationが必要。

## Development Intelligence拡張 — 2026-08-12

Upgrade8-based sourceでは、Intelligence Dashboard aggregation、deterministic Game Session Regression Replay、platform Performance Budget、Unreal collector、real-device hardware backtesting/calibration、deterministic Change Risk、automatic evidence-based Regression Triage、Release Readiness gating、explicit GitHub Issue work-item creation、Release Provenance/CycloneDX SBOM bundle、OpenTelemetry/Prometheus/Grafana observability、Multi-repository Dependency Graphを実装した。Persistence modelは`20260812000300_AddDevelopmentIntelligenceExpansion`で拡張する。

これらも**sourceへImplementedであり、自動的にruntime-verifiedではない**。Completion Evidenceにはexact revisionの.NET 8 build/test、clean/legacy PostgreSQL migration run、live PostgreSQL-backed Worker durable-job test、selected Local/MinIO artifact round trip、OTLP export verification、engine/hardware固有path向けreal Unity/Unreal/device runが必要。Real-device calibrationは実測sampleが記録されるまでemptyのままであり、portfolio benchmarkの`TBD`もfixed-environment measurementまで変更しない。

このphaseでimplemented external work-item pathはGitHub Issues connectorであり、Jira/LinearをImplementedとは表現しない。この時点で延期されていたUniversal/unknown-language QA abstractionは、後述する2026-08-14 Universal QA layerで実装された。

## 2026-08-11 Upgrade6 CI修復Package

Upgrade6 repair packageは、latest `.NET CI` runで観測された7件のcompiler error（P2 E2E client method欠損3件、Web API client method欠損2件、`MediaRegressionRunner` declaration-space collision 2件）を対象とする。また、以前のreconstructed packageに不足していたcurrent-main compatibility fileを復元し、legacy intelligence serviceを再登録する。

Static package validationは`artifacts/package-validation/UPGRADE6_FIX_STATIC_VALIDATION.txt`へ記録する。Exact resulting revisionで.NET 8 build/test runとlive Integration/E2E workflowが成功するまで、このrevisionは引き続き**runtime-verifiedではない**。

## Project Control / Development Graph（プロジェクト統制 / 開発Graph）— 2026-08-12

Source implementationにはcentral Development Graph、GitHub/Perforce version-control normalization、Change Impact/Blast Radius、Release Readiness integration、Milestone/Critical Path、Test Impact graph link、Review Intelligence、Feature Readiness、Acceptance Criteria Evidence、Asset Intelligence、cross-repository graph bridging、Decision Log/Brief、What-if simulation、Jenkins/normalized external CI ingestionを含む。

このphaseではhardcoded QA language scopeを意図的にPython/C++/C#へ限定した。Exact resulting revisionが.NET build/tests、PostgreSQL migration/application、connector smoke test、live dashboard/API verificationを通過するまで、**sourceへImplementedだがruntime-verifiedではない**。`docs/PROJECT_CONTROL_IMPLEMENTATION_20260812.md`参照。

## 2026-08-14 Universal QA S-priority実装

| 領域 | コード状態 | Exact-revision Completion Evidence |
|---|---|---|
| Extensible `LanguageId` | Implemented | target .NETでのDomain/build tests |
| Universal Adapter Registry | Implemented | Universal QA Bug Zoo |
| Build/Framework/Game Engine detection | Implemented | detector tests + Bug Zoo/profile Evidence |
| SARIF / JSON / JUnit normalization | Implemented | normalizer tests + structured-report cases |
| Multi-language Bug Zoo | Implemented | `artifacts/qa-bug-zoo/qa-bug-zoo-results.json` |
| Unknown/proprietary language manifest | Implemented | `customlang` Bug Zoo cases |
| Formal Windows/.NET/Unity verification | Runner implemented | exact commit用`artifacts/formal-portfolio-verification/FORMAL_PORTFOLIO_VERIFICATION_PASS.txt` |
| Portfolio benchmark measurement | Runner implemented | exact commit用`artifacts/portfolio-benchmark/portfolio-benchmark-results.json` |

以前のclosed `QaLanguage` enumは削除済み。既存persisted value（`Python`、`CSharp`、`Cpp`）は`LanguageId` alias経由で引き続き読めるため、QA language columnに破壊的schema migrationは不要。Python/C#/C++はdeep specialized adapterを維持し、追加言語はbuilt-in generic adapterまたは`.dip/qa.json`を使用する。Structured diagnosticをtext fallbackより先にnormalizeする。

このgenerated source treeは、**target machine上でexact revisionについてrunnerを実行するまでWindows/.NET/Unity runtime PASSやbenchmark numberを主張しない**。Previous-revision Evidenceの誤labelを防ぐためである。

## 2026-08-14 QA Governance / Reliability拡張

| 領域 | Source state | Exact-revision Evidence |
|---|---|---|
| Flaky/Test Reliability | Implemented | Governance domain tests + repeated-run Evidence |
| Finding lifecycle / Waiver | Implemented | Governance domain/API verification |
| Regression baseline | Implemented | baseline records + regression/bisect Evidence |
| Ownership / CODEOWNERS fallback | Implemented | repository-rule verification |
| QA Policy / Release Gate | Implemented | `qa_policy_evaluations` + release-readiness integration |
| Execution sandbox boundary | Implemented | tool sandbox metadata + target OS verification |
| Provenance / reproducibility | Implemented | exact-commit `qa_evidence_provenance` |
| QA self-health | Implemented | Product findingと分離したhealth snapshot |
| Root-cause/change correlation | Implemented | finding/baseline correlation。既存evidence-root-causeがauthoritative evidence-ranking path |
| Quality trend / technical debt | Implemented | trend snapshot + Project Intelligence UI |
| Smart test selection | Implemented | path/test impact rule + API |
| Diagnostic schema versioning | Implemented | finding上の`dip.finding.v2` |
| Artifact retention | Implemented | ArtifactService / retention workerがretention ruleを適用 |
| Freshness / confidence | Implemented | provenance age + QA self-health confidence |
| QA cost/runtime budget | Implemented | wall/tool/CPU/artifact cost record + policy check |
| Test recommendation | Implemented | changed-path/flaky/coverage-gap recommendation |
| Automatic bug minimization | Implemented（safe argument reduction） | sandboxed reproduction Evidence必須 |
| Cross-project benchmark comparison | Implemented | same-definition successful run比較。Environment comparabilityはcaller責任 |

Formal completionには`scripts/Verify-QaGovernance.ps1`と既存exact-revision portfolio runnerが必要。Generation environmentはWindows/.NET/Unity runtime Evidenceの代替ではない。

## 2026-08-16 Defect Intelligence / Universal QA拡張

| 領域 | Source state | Completion Evidence |
|---|---|---|
| Deep Language Pack / capability planner | Implemented | exact-revision adapter tests + Bug Zoo |
| Universal structural AST + bounded data-flow | Implemented | unit tests + formal .NET run |
| Runtime Defect Intelligence | Implemented | runtime event -> Incident E2E |
| Verified Fix + Regression Test isolation | Implemented | disposable worktree QA E2E |
| Unity/Unreal Game QA expansion | Implemented | real Unity/Unreal runs |
| Sentry / OpenTelemetry ingest | Implemented | authenticated sample event E2E |
| Jira / Linear connectors | Implemented | configured sandbox integration test |
| Fuzz/property/race extensibility | Implemented | native tool availabilityまたは`.dip/qa.json` Evidence |
| API/DB/distributed QA pack | Implemented, opt-in | `.dip/system-qa.json` E2E |
| Evidence-grounded Fix Agent | Implemented, default OFF | evidence-ID contract + VerifiedFix E2E |
| IDE/LSP diagnostics bridge | Implemented | API + bridge round trip |
| Native-toolchain benchmark | Toolがある環境でMeasured | `artifacts/universal-qa-benchmark` |

Bootstrap benchmarkは意図的に`formalAdapterBenchmark=false`とlabelし、`Dip.QaBugZooVerifier`の代替にはしない。GitHubがhosted runnerを実際に開始でき、exact packaged revisionが.NET/integration/GUI/security workflowを通過するまで、このsource packageをfully CI-greenと説明してはならない。

## 2026-08-16 Management Operations S+/S拡張

| 領域 | Source state | Runtime Completion Evidence |
|---|---|---|
| Daily Cockpit / My Actions / My Decisions / Approvals | Implemented | authenticated browser/API smoke |
| Risk / Decision / Action / Scope / Resource GUI CRUD | Implemented | Blazor runtime + persistence round trip |
| Slack / Teams / SMTP notifications | Implemented | configured test-destination delivery |
| Ack / Snooze / L1-L3 escalation | Implemented | Worker time-based escalation test |
| Formal approval + role binding + SoD | Implemented | authenticated multi-role approval E2E |
| GitHub / Jira / Linear bidirectional Action sync | Implemented; paginated + ProjectId-scoped | sandbox provider round trip + conflict replay |
| Google Sheets two-sided sync | Implemented | sheet snapshot/conflict/resolution E2E |
| Portfolio / Program Command Center | Implemented | multi-project authenticated browser test |
| Explicit cross-project Shared Resource Pool | Implemented | two-project capacity scenario |
| RACI responsibility + alert routing | Implemented | RACI -> notification route E2E |
| Daily / Weekly / Release meeting mode | Implemented | browser/API smoke |
| Work Calendar / PTO / holiday / meeting capacity | Implemented; all-day eventはconfigured working dayを尊重 | capacity fixture + Google Calendar sandbox sync |
| Source Authority / conflict resolution | Implemented | Manual/PreferAuthority provider conflict E2E |

Static integration contract Evidenceは`tools/validate_management_operations.py`が`artifacts/management-operations-validation-20260816/static-contract.json`へ生成する。Wiring、authorization declaration、Web/API surface、conflict-safety contract、configuration presenceを検証するが、**.NET compilationやlive third-party delivery/synchronizationを主張しない**。これらはexact-revision runtime completion requirementのまま。

## 2026-08-16 Portfolio公開Hardening

既存exact-revision verification policyを弱めず、reviewer-facing portfolio hardeningを追加した。

- Deterministic sample-data screenshot/GIFとarchitecture overviewを持つ簡潔なREADME。
- Publication gateとしてfail-closed `tools/verify_publication_safety.py` + GitHub Security Scan。
- Repository visibilityをpublicへ変更する前のexact-SHA required-workflow gate。
- Commit-bound historical benchmark / QA Bug Zoo / Windows .NET Evidence snapshot。
- Package内zero-byte `.razor` pageを0件化。
- `ManagementOperations`をRazor view、code-behind、再利用可能cockpit/tab componentへ分割。
- Future PR向けConventional Commit validation。
- Build、unit test、safety check、SHA-256 release manifestを伴うguarded `v1.0.0-portfolio` tag/release path。

**重要:** これらを追加しても、runnerが実行する前のpackaged revisionをCI-greenとは主張しない。Job step開始前にworkflowが失敗した場合はinfrastructure-unavailableであり、PASSではない。Public visibilityとGitHub Releaseはexternal state changeなので、modified revision自身がrequired workflow成功Evidenceを持つまで意図的にgateする。

## 2026-08-29 Portfolio Production Hardening（本番品質強化）

残るportfolio concernをhardeningしつつ、missing runtime EvidenceをPASSへ変換しない。P2 soakはharness terminationとtarget crashを区別し、Python Analysisはproduction API key欠損時にfail-closed、benchmarkは15分/25-RPS default soakを持つ専用non-Development production-like profileを使用、restoreはpre-restore backupを取得してfailure時にautomatic rollback、Unrealは任意でUnrealEditor-Cmd Automation Testsを実行可能、unknown Project Truth progressはnullableを維持、readiness probeはcritical dependencyだけを確認、PostgreSQL-backed queue operationはfail-closed、latest-revision documentationはstale repair baselineを固定しない。GitHub-hosted CI successとexact-revision performance numberは引き続きexternal Evidence requirementであり、このsource changeからfabricateしない。
