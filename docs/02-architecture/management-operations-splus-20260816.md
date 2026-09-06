# Management Operations S+/S（運用管理）— 2026-08-16

## 目的

この拡張では、既存のDevelopment Graph / Management Analyticsデータを、PM・Producer・各領域Leadが日常運用で使える制御ループへ接続する。Evidence / Project Truthを置き換えるものではない。Management decision、responsibility、approval、alert、calendar、synchronization stateを既存graphと並べて永続化し、技術Evidenceと管理上のActionを切り離さない。

想定する運用ループは次のとおり。

```text
Detect -> Prioritize -> Decide -> Assign -> Approve -> Act -> Verify -> Close -> Review
```

## 実装済みS+機能

### Daily Cockpit / My Work（日次作業画面）

`GET /api/v1/management/projects/{projectId}/cockpit`

Cockpitはcurrent alert、open action、pending decision、approvalを統合する。`ownerRole`は**alert、action、decision、approval roleをまとめて**絞り込むため、Actionだけのfilterではない。Web画面は`/dashboard/{projectId}/management`。

### Management RegisterのBrowser CRUD

Management画面からRisk、Decision、Action、Scope Change、Resource Allocationを作成・更新・archiveできる。Update formでは、既存recordを縮小subsetで上書きしないよう、status、category、trigger、mitigation、related target、completion、decision link、scope key、allocation rationaleなど、運用に必要なmanagement fieldを維持する。

Archiveは非破壊である。`ManagementOperationsService.ArchiveAsync`はgraph nodeを保持し、archive actor/time/reason metadataを追加する。

### 通知配信 + Acknowledge / Snooze / Escalation

`ManagementNotificationWorker`はoperational alertを計算し、構成されたSlack、Teams、SMTP channelへroutingする。Delivery failureはWorker logで確認可能にし、成功したdelivery levelは`AlertState`へ保存する。

Alert stateは次を持つ。

- `Acknowledged`
- `Snoozed` + `SnoozeUntilUtc`
- `Resolved`
- `Open` / reopen

Escalationは保存済み`FirstSeenAtUtc`を基準にする。Acknowledgementでincident ageはresetしない。Acknowledgement時に到達済みescalation levelを記録し、同じlevelの再通知を抑制しつつ、後続のL2/L3 escalationは許可する。

既定ladder:

| Severity | L1 | L2 | L3 |
|---|---:|---:|---:|
| Critical | 即時 | 30分 | 2時間 |
| High | 即時 | 2時間 | 1日 |
| Other | 即時 | 2日 | — |

`ManagementNotifications:RoleRoutes`はRACI/owner roleをdelivery channel typeへ対応付ける。`ManagementNotifications:RoleDestinations:<role>`ではroleごとにSlack webhook、Teams webhook、email recipientをoverrideでき、`*`をfallbackに使用できる。Source itemにRACIがある場合はResponsibleを最優先し、Accountable、明示的owner metadata、category fallbackの順でroutingする。

### 正式な承認Workflow

Approval requestは既存management/release graph nodeを対象にし、1つ以上のmanagement roleを要求する。Project Ownerはauthenticated audit identity（`login#subject`）をmanagement roleへbindする。Approval response endpointはactorを`RequestActor.AuditIdentity(User)`で上書きするため、request bodyからauthenticated identityを指定できない。

既定では`AllowSameActorMultipleRoles=false`であり、同一人物が同一approvalで複数のrequired roleを満たせない。1件でもrejectされるとApprovalはRejected、全required roleがapproveするとApprovedになる。

Sensitive API role:

- role binding: Project **Owner**
- source authority: Project **Owner**
- approval create/respond: Project **Maintainer**
- issue tracker sync / conflict resolution: Project **Maintainer**
- alert disposition / RACI: Project **Member**

### 競合に安全な外部同期

GitHub、Jira、Linearはexternal work itemをDIP Management Actionへmappingする。Google Sheetsは、より広いeditable management workbookを対象にする。

Issue tracker synchronizationは双方向で、provider link/revisionを保持する。Provider adapterはbounded safety limit付きで100件を超えるpageも取得する。Managed external recordは明示的にProject scopeを持つ。GitHub/Linear descriptionには`dip-project:<ProjectId>` marker、Jiraには`dip-project-<ProjectId>` labelを付ける。Pull時には同じProjectIdを必須とし、1つのJira/Linear/GitHub accountを複数DIP Projectで共有してもActionが混在しないようにする。External descriptionへ書き込むmanagement metadataは、marker/field serialization前にbounded single lineへ正規化する。

Issue tracker synchronizationはconflict-safeである。Mismatchは、明示的Source Authorityが`ConflictPolicy=PreferAuthority`で解決しない限りdurable conflictになる。`Manual`では両側変更を黙って上書きしない。Conflict resolutionは次の遷移を取る。

```text
Open -> Resolved(DIP|External) -> next successful synchronization -> Applied
```

External-only itemはremote側から自動削除しない。External-only conflictでDIPを選ぶことは「変更のないremote itemをimportしない」という意味であり、local-only conflictでExternalを選んだ場合は、破壊的remote deleteを作るのではなくlocal DIP Actionをarchiveする。

Google Sheetsはsheetごとに`(LocalHash, ExternalHash, SyncedAtUtc)` snapshotを保存する。Last snapshot後に両側が変更された場合はconflictとしてsynchronizationを停止する。Snapshot/resolution stateは、選択したpull/pushが成功してから進める。

## 実装済みS機能

### Portfolio / Program Command Center（統括画面）

`GET /api/v1/management/portfolio`

Home画面は、アクセス可能なProjectについてproject health、schedule delay、peak capacity、critical risk、blocker、pending decision、overdue action、approval、synchronization conflictを集約する。CallerがAdmin/development modeでない限り、authenticated userから見えるProjectだけを返す。

### 明示的Cross-project Resource Planning

Cross-project resource planningでは、team nameが同じという理由だけでshared workforceを推測**しない**。各Teamは明示的に次を持てる。

- `SharedResourceKey`
- `SharedPoolCapacityHoursPerWeek`

Management画面は既存team capacity/name/statusを書き換えずにTeam membershipを追加・削除できる。Teams spreadsheetは両値をround-tripする。

Portfolio planningは明示的に一致するresource keyだけをgroup化し、Project横断のweekly demandを合算してshared pool limitと比較する。同じkeyで異なるlimitが構成された場合はconfiguration conflictを表示し、値が揃うまでは保守的に最小limitを使用する。

### RACI / 責任分担

RACI assignmentは既存graph itemを対象にし、Responsible、Accountable、Consulted、Informed roleを保存する。RACIはoperational alert routingにも使うため、単なるDocumentationではなく動作へ影響する。

### 日次 / 週次 / Release Review Mode

`GET /api/v1/management/projects/{projectId}/cadence/{daily|weekly|release}`

Meeting modeは同一のManagement Dashboard/Cockpit dataからbounded agendaを生成する。Release reviewでは、独立して追跡できないmeeting stateを作るのではなく、schedule gate、blocker、critical risk、pending approvalを明示的に表示する。

### Work Calendar / PTO / 休日 / Meeting Capacity

Work CalendarはProject-wideまたはTeam-specificで、次を持つ。

- Hours/dayとworking days
- recurring meeting hours/week
- support/on-call hours/week
- dated capacity exception（PTO、holiday、meeting、support、その他）

Calendar deductionはweekly capacity forecastへ直接適用する。複数weekにまたがるexceptionは各weekとの実際のoverlapで按分し、長期休暇をすべてのoverlap weekへ全量控除しない。

Google Calendar synchronizationはPTO/leave/holiday/meeting/support eventをcalendar exceptionへ分類する。OAuth access tokenはrequest内のみで扱い、graph metadataへserializeしない。All-day eventはUTC固定ではなくcalendar/event timezoneを使用する。複数日にまたがるall-day PTO/holidayは、構成されたworking dayにだけexceptionとして分割し、週末・非稼働日を誤ってcapacityから引かない。

### Source of Truth / 競合Policy

Source Authority ruleはProject scopeを持ち、`DataType`、`Authority`、`ConflictPolicy`、priorityを指定する。Supported conflict policyは意図的に次の2つへ限定する。

- `Manual`: 実際のmismatchを自動上書きしない
- `PreferAuthority`: 構成済みauthoritative sideを適用する

Issue trackerとGoogle Sheets synchronizationでこのpolicyを使用する。Manual mergeは選択した側を編集後、DIPまたはExternalとしてresolveする。DIPは決定的に実行できない曖昧な第三の`Merge` stateを保存しない。

## セキュリティ / 運用境界

- Project scope endpointは既存`ProjectScopeAuthorizationFilter`で保護し、route/bodyのProjectIdをcross-checkする。
- Formal approval identityはauthenticated claimからserver-sideで決定する。
- Slack/Teams webhook URLはHTTPS必須。
- Google Calendar/Sheets access tokenはこのlayerで永続化しない。
- Source synchronizationは破壊的remote deleteを自動実行しない。
- Notification channelが未構成ならinactiveとし、偽のdelivery successを記録しない。

## 検証

`tools/validate_management_operations.py`はDomain、Application、Infrastructure、API、Worker、Web、configurationを横断してintegration surfaceを静的検証する。このPASSは**.NET buildまたはlive connector PASSではない**。

Exact-revision runtime completionには引き続き次が必要である。

1. `dotnet build`と.NET unit/integration tests。
2. Authentication enabled状態でのBrowser/API smoke test。
3. Test destinationへのSlack/Teams/SMTP delivery。
4. Sandbox accountを使ったJira/Linear/GitHubとGoogle Sheets/Calendar test。
5. Worker + PostgreSQL persistence/restart verification。

生成されたvalidation Evidenceは`artifacts/management-operations-validation-20260816/`へ保存する。
