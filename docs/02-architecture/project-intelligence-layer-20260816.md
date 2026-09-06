# Project Intelligence Layer（プロジェクト知識層）— 2026-08-16

## 目的

DIPは、企業にExcel、Redmine、Slack、Teams、GitHub、CI、QA toolの置き換えを要求しない。Project Intelligence Layerは既存sourceを読み取り、provenance（出所）を保持し、同じwork itemを示すreference同士を関連付け、計画・報告状態・機械的に観測されたEvidenceが一致しない場所をPM/Leadへ示す。

中心となる比較は、意図的に次の4layerへ分離する。

1. **Plan** — Excel/CSVからimportしたWBS/Gantt data。
2. **Operational report** — Redmine status/progressとcollaboration上のprogress Claim。
3. **Engineering evidence** — DIPに保存済みのGitHub/PR/CI/build/test/QA/runtime Evidence。
4. **Management interpretation** — stale signal、contradiction、evidence readiness、digest action。

Chatで「done」と報告されてもverified completionへ昇格させない。Redmineのdone ratioもbuild/test resultとして扱わない。`EvidenceReadinessPercent`はwork-item単位のEvidence heuristicであり、**従業員のskill scoreやperformance ratingではない**。

## Excel / WBS / Ganttの理解

Upload対応形式は`.xlsx`、`.xlsm`、`.csv`。Legacy `.xls`はimport前に`.xlsx`またはCSVへ保存する必要がある。

Analyzerは各sheetの先頭20行をscanし、次の日本語/英語aliasを検出する。

- WBS key / task ID
- task / work-item name
- parent
- planned start / end / due date
- declared progress
- owner
- status
- predecessor/dependency
- milestone

Browserではimport前に必ずmapping previewを表示する。WBS hierarchyがない場合は`1.2.3`のようなdotted identifierまたはtitle indentationから推定できる。Importerはworkbook revisionごとに`ProjectPlanImport` + `PlanItem` nodeとしてDevelopment Graphへ保存し、**canonical Task/Action/Requirement nodeを上書きしない**。

Safety limit:

- upload: 12 MB
- XLSX package entry: 2,000
- expanded workbook: 合計80 MB / entryごと32 MB
- plan row: import revisionあたり20,000

## Cross-source Entity Resolution（情報源横断のEntity統合）

DIPはExcel WBS row、Redmine issue、Slack observation、GitHub PR、commitなどのsource entityをcanonical project entityへmappingしようとする。

主なsignal:

- exact logical key
- 明示的`DIP:` / `TASK:` reference
- external URL/reference match
- source title内のtarget key
- normalized title-token similarity

Automatic persistenceは意図的に保守的にする。Exact/非常に強いmatch、または高scoreかつ次候補に十分なmarginがある場合だけ自動保存する。Low-confidence matchはMaintainerがconfirmするまで`Suggested`のまま残す。Manual confirmationでは`EquivalentTo` edgeと監査可能な`EntityBinding` nodeを作成する。

## Redmine情報源

Redmine adapterは**read-only**でREST APIを使用し、次をimportする。

- issue ID / subject / description
- tracker / status / priority
- assignee
- start / due date
- done ratio
- fixed version
- relation
- attachment
- custom field

実際のRedmineでは企業固有WBS/ownership conventionをcustom fieldで表すことが多いため、custom fieldはsource metadataとして保持する。Field名に`DIP`、`Task`、`WBS`を含むものはentity reference候補としても扱う。

Configuration:

```json
{
  "ProjectIntelligenceSources": {
    "Redmine": {
      "BaseUrl": "https://redmine.example.com",
      "ApiKey": "server-secret",
      "ProjectIdentifier": "default-project",
      "ProjectIdentifiers": {
        "<dip-project-guid>": "redmine-project-key"
      }
    }
  }
}
```

Production Redmine URLはHTTPS必須。Project-specific mappingにより、1つのRedmine serverが複数ProjectをhostingしていてもDIP Project間でdataが混ざらないようにする。

## Slack Sourceと署名付きEvent Ingestion

2つの経路を用意する。

- `conversations.history`で関連するrecent messageを読む
- `POST /api/v1/project-intelligence/hooks/slack/{projectId}`でSlack Events API callbackを受ける

History adapterはすべてのchat messageを取り込まない。Task marker、URL、progress percentage、completion/review/test/CI/PR/blocker表現、file Evidenceを含むmessageだけを残す。Project-specific channel allowlistでcollection範囲を制限する。

Slack Events requestは`X-Slack-Request-Timestamp`と`X-Slack-Signature`を必須とする。DIPは公式HMAC署名を検証し、5分のreplay windowを超えたtimestampを拒否する。Event channelもroute対象DIP Projectで許可されていなければならない。

必要なSlack scopeは利用channel typeによって異なる。最低限、botには構成されたconversation historyを読む権限が必要である。Tokenとsigning secretはserver-side secret storageへ置き、browser bundleへ含めない。

## Teams Evidence Bridge（Teams証拠連携）

署名付きnormalized bridgeを次で提供する。

`POST /api/v1/project-intelligence/hooks/teams/{projectId}`

Header:

`X-DIP-Ingest-Key: <server-side-key>`

Payload:

```json
{
  "externalId": "message-123",
  "channel": "rendering",
  "author": "lead-a",
  "text": "TASK-42 implementation complete; PR https://github.com/org/repo/pull/42",
  "observedAtUtc": "2026-08-16T06:00:00Z",
  "url": "https://teams.example/message/123",
  "references": ["TASK-42", "https://github.com/org/repo/pull/42"],
  "revision": "1"
}
```

1つのDIP deploymentで複数Projectを扱う場合は`ProjectIngestKeys`を使用する。Shared `IngestKey`はfallbackのみ。

## Source ProvenanceとDeduplication

各source observationでは次を保持する。

- provider
- external source ID
- revision
- observed/updated time
- source URL
- raw normalized SHA-256
- 必要に応じてchannel/author

Source node keyとrevisionを使い、変更のないpolling dataから重複Evidence recordが生成されることを防ぐ。Revisionを持たないcollaboration messageではtextとreferenceからdeterministic SHAを生成する。

## 計画対実績 / Evidenceに基づく進捗

現在のWBS itemごとに、独立したfieldとして次を表示する。

- `PlannedPercent` — imported planで宣言された値
- `OperationalPercent` — 最新canonical task / Redmine / collaboration report
- `EvidenceReadinessPercent` — 独立したmachine Evidenceを集約したheuristic
- `ReadinessConfidence` — `Unverified`、`Low`、`Medium`、`High`、`Verified`

典型的なcontradiction:

- WBSは80%だがRedmineは40%
- WBSは100%だがevidence readinessが低い
- Slackでは「complete」だがPR/CI/QA Evidenceが紐付かない
- 高いreported percentageとfailing CIが矛盾
- Due date経過後も独立したverified completionがない
- 7日間freshなprogress/evidence signalがない

Systemはこれらをsource/evidence gapとして説明し、従業員の報告が不正確だと非難しない。

## Management DigestとWBS/Gantt Viewer

`/dashboard/{projectId}/sources`では次を提供する。

- source adapter statusとdry-run/sync action
- Excel mapping preview/import
- plan-vs-actual table
- evidence detail
- contradiction/stale reason
- cross-source statusで色分けしたWBS/Gantt schedule bar
- unresolved entity suggestionとmanual confirmation
- executive management digest

Gantt viewはimportした企業planを読む・理解するためのsurfaceである。元のExcelはそのままplanning toolとして使用できる。

## API公開面

Authenticated project route:

- `GET /api/v1/project-intelligence/projects/{projectId}/overview`
- `GET /api/v1/project-intelligence/projects/{projectId}/gantt`
- `GET /api/v1/project-intelligence/projects/{projectId}/digest`
- `GET /api/v1/project-intelligence/projects/{projectId}/entity-resolution`
- `GET /api/v1/project-intelligence/projects/{projectId}/sources`
- `POST /api/v1/project-intelligence/projects/{projectId}/sources/{provider}/sync`
- `POST /api/v1/project-intelligence/projects/{projectId}/plans/analyze`
- `POST /api/v1/project-intelligence/projects/{projectId}/plans/import`
- `POST /api/v1/project-intelligence/projects/{projectId}/collaboration/ingest`
- `POST /api/v1/project-intelligence/projects/{projectId}/entity-bindings`

Signed public webhook route:

- `POST /api/v1/project-intelligence/hooks/slack/{projectId}`
- `POST /api/v1/project-intelligence/hooks/teams/{projectId}`

Read routeはViewer membershipを要求する。Pull/import/binding routeはMaintainer、authenticated normalized collaboration ingestionはMemberを要求する。Public webhook routeはbrowser/JWT identityを使わず、Slack HMACまたはProject固有bridge keyで独立認証する。

## 対象外

Project Intelligence Layerは意図的に次を行わない。

- Excel、Redmine、Slack、Teams、GitHubの置き換え
- 個人developerのskill score推定
- chat messageをmachine-verifiable truthとして扱うこと
- WBS import時のcanonical task data上書き
- low-confidence entity matchの強制確定
- 構成Project channel外の無関係なSlack conversation scan
