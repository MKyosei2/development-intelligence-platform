# アーキテクチャ概要

DIPはbrowser-firstのEvidence platformである。**durable truth**、**delivery/coordination**、**analysis**、**external Evidence Provider**を分離し、1つのadapter障害がProject Truthの定義そのものを変えないよう設計している。

## 境界

1. **Presentation** — `Dip.Web`（Blazor）がPM/Lead向けviewを提供し、browserへsecretを保存しない。
2. **API / Application** — `Dip.Api`がauthenticatedかつProject-scopedなoperationを公開し、`Dip.Application`がuse caseとdecision logicを持つ。
3. **Domain** — `Dip.Domain`がinfrastructureへ依存せずEvidence、Project Truth、lifecycle invariantを管理する。
4. **Infrastructure** — PostgreSQLをdurable Source of Truthかつdurable queue/coordination storeとする。Delivery semanticsは`IQueueClient`の背後へ分離し、現在は`PostgresQueueClient`が実装する。
5. **Async Execution** — `Dip.Worker`がdurable jobをconsumeし、external/runtime Evidenceを正規化する。
6. **Evidence Provider** — GitHub/CI、Unity、Unreal、Maya、native/runtime collector、management integrationをprovider-neutral contractへmapする。
7. **Verification** — Test、QA Bug Zoo、security scan、benchmark runnerがcommit-bound Evidenceを生成する。新しいrevisionは古いPASSを自動継承しない。

## Failure / Trust Model（失敗 / 信頼モデル）

- External ID/revisionがあるingestionは可能な限りidempotentにする。
- 曖昧なcross-source matchはsilent mergeせずexplicit confirmationを要求する。
- Secretはserver-sideに保持し、必須値が空のProduction configurationはfail-closedとする。
- AI/Fix Agent outputはadvisoryに留め、disposable worktreeでbuild/test/QAを再実行してverification Evidenceが得られるまで確定扱いしない。
- 人物へtruthfulness/skill scoreを付与せず、work Claimとtechnical Evidenceの関係だけを評価する。

Subsystem固有設計は`docs/02-architecture/`を参照。
