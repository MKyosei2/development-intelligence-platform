# Commercial Project Management + Branch QA

This document defines the operational contract for the DIP Project Control Center and Branch Quality Center.

## 1. PM is a project manager, not a QA viewer

The PM surface must allow a project manager to control the complete delivery loop. DIP therefore treats the following as first-class project controls:

1. Plan and work breakdown: team, workstream, task, estimate, remaining work and ownership.
2. Schedule: planned dates, forecast finish, delay, slack and critical path.
3. Milestones: linked task progress, due dates, blocked state and readiness.
4. Dependencies: predecessor/successor relationships, cross-workstream dependencies and cycle detection.
5. Blockers: direct and downstream impact, age and required management action.
6. Risk register: probability, impact, exposure, trigger, mitigation, owner and review date.
7. Decision log: decision owner, due date, rationale, related work and open follow-up actions.
8. Action management: priority, owner, due date, completion and overdue state.
9. Scope control: baseline, included/deferred requirements, scope growth and incomplete high-priority requirements.
10. Change control: engineering/art/QA effort delta, schedule delta, risk delta and decision linkage.
11. Capacity and resource allocation: team/workstream load, buffer and explicit reallocation proposals.
12. RACI: responsible/accountable/consulted/informed roles, including missing-accountable detection.
13. Approvals: required roles, separation of duties, due date, responses and overdue state.
14. Quality: project operational QA plus branch-scoped promotion evidence.

The Project Control Center is the executive/read-and-decide surface. Durable edits continue through the existing Management Workspace and management APIs so DIP has one source of truth instead of a second dashboard-only project model.

## 2. GUI responsibilities

### Project Control Center

Routes:

- `/dashboard/{projectId}/project-control-center`
- `/dashboard/{projectId}/pm`

The page must answer, without opening a raw JSON artifact:

- Is the project on plan?
- What is the forecast finish and current delay?
- What is on the critical path?
- Which milestones are at risk or blocked?
- Where is capacity overloaded?
- What blockers and risks threaten delivery?
- What decisions and actions are overdue?
- What scope changes are awaiting a decision?
- Is there a dependency cycle?
- Are RACI/accountability and approvals complete?
- Is the default branch quality gate safe for promotion?
- What requires the PM's decision today?

### Management Workspace

Route:

- `/dashboard/{projectId}/management`

This remains the durable command surface for creating/updating teams, workstreams, tasks, dependencies, milestones, blockers, requirements, risks, decisions, actions, resource allocations, scope changes, RACI, role bindings, calendars and approvals.

### Branch Quality Center

Routes:

- `/dashboard/{projectId}/qa/branches`
- `/dashboard/{projectId}/branch-quality`

The page shows every branch DIP can establish from a Git working tree or from GitHub project provenance. For archive-backed GitHub workspaces it automatically merges the default branch, open pull-request heads, synchronized snapshot and persisted DIP branch/pipeline history. A manual branch field can verify any other valid branch directly, so a branch does not need an open pull request before QA. It displays:

- exact branch HEAD SHA;
- remote/local provenance;
- ahead/behind relative to the default branch;
- latest branch QA run and age;
- open critical/warning issues linked to branch QA evidence;
- `Ready`, `Hold`, `Blocked` or `QA required` promotion gate;
- a direct action to run isolated QA on the selected branch.

## 3. Branch QA isolation contract

DIP MUST NOT perform `git checkout` on the shared synchronized project workspace.

For a branch QA run DIP chooses one of two isolation paths:

**Git working-tree source**

1. validates the branch with Git;
2. optionally performs a bounded remote refresh;
3. resolves `origin/<branch>` first, then a local branch;
4. verifies requested commit ancestry when an explicit commit is supplied;
5. creates `git worktree add --detach` for the exact revision;
6. runs QA and removes the detached worktree after completion/failure.

**GitHub archive-backed source (the standard GitHub repository sync path)**

1. reads `.dip-sync.json` provenance instead of pretending the archive is a Git checkout;
2. validates the requested branch name with fail-closed portable ref rules;
3. uses the configured `IGitHubRepositoryCatalog` (GitHub App/token credentials) to download the requested branch into a run-specific temporary workspace;
4. records the exact branch-head SHA returned by GitHub;
5. refuses an explicitly requested SHA when it is not that downloaded branch head;
6. runs all language adapters, cross-cutting packs, media checks and reproduction against that isolated branch snapshot;
7. removes the snapshot after the run, including failure paths.

Both paths store the real branch name in `PipelineRun.Branch`, the exact SHA in `PipelineRun.CommitSha`, and branch/ref/isolation provenance in QA summary and `qa-intelligence.json`. The shared synchronized project workspace is never checked out or overwritten.

This prevents concurrent QA from changing the developer/shared checkout and prevents evidence for one branch from being mislabeled as evidence for another.

## 4. Promotion gate policy

Branch promotion is fail-closed:

- **QA required**: no branch-scoped evidence exists. Do not promote.
- **Blocked**: latest branch QA failed/timed out/cancelled, or an open critical issue is linked to the branch. Do not promote.
- **Hold**: evidence is stale, partial, or warnings remain. Triage/approve before promotion.
- **Ready**: latest branch QA succeeded, is fresh, and has no open critical/warning issue linked to its branch QA runs.

`Ready` is technical evidence, not an automatic merge authorization. Human review, approval, scope and release policy still apply.

## 5. Default-branch release truth

The commercial release gate is scoped to the project's configured default branch. A P0 on a feature branch stays visible in the project QA operations queue, but it is not silently treated as evidence that the current default-branch revision is broken.

Conversely, a clean feature branch is not evidence that the default branch is ready. The default branch needs its own branch-scoped QA evidence.

Legacy QA runs stored with branch `qa-workspace` are not automatically promoted to default-branch evidence because their branch provenance is ambiguous.

## 6. Remote freshness

Branch freshness depends on the workspace type and is explicit:

- `Fetched`: Git refs were refreshed from origin for this request.
- `Cached`: a Git working tree used locally cached refs after no refresh or a failed refresh.
- `GitHubApi`: archive-backed branch discovery came from authenticated GitHub metadata plus DIP history.
- `GitHubArchive`: the QA run downloaded a fresh authenticated branch snapshot and exact branch-head SHA.
- `Unavailable`: branch identity/catalog could not be established.

A Git fetch failure is never displayed as fresh remote evidence, and archive-backed QA does not claim ahead/behind counts that it cannot calculate locally.

## 7. Source of truth and AI boundary

- Git commit SHA + branch ref + tool outputs + reproduction artifacts are evidence.
- PipelineRun is the durable branch/evidence boundary for branch QA.
- Project management state is stored through the existing Development Graph / management commands.
- AI/debug hypotheses remain hypotheses until an experiment produces objective evidence.
- PM recommendation text is decision support, not an approval or a fabricated project fact.

## 8. Operational workflow

### Feature branch

1. Developer/QA opens Branch Quality Center.
2. Select the feature branch and refresh refs when remote currency is required.
3. Run branch QA.
4. Triage any P0/P1 findings in Quality Operations / QA Governance.
5. Re-run the exact branch revision after a fix.
6. Merge only when branch evidence, code review and required approval policy are satisfied.

### Release/default branch

1. PM opens Project Control Center.
2. Review schedule, critical path, milestones, blockers, risk, decisions, scope, capacity and approvals.
3. Open Branch Quality Center and verify the default/release branch head.
4. Resolve or explicitly disposition warnings; P0 remains fail-closed.
5. Confirm the exact SHA that passed QA is the SHA being promoted.
6. Record the release/approval decision through the existing approval/decision system.

## 9. Validation

Run:

```powershell
python tools/validate_pm_branch_qa.py
```

Then, on a machine with the .NET SDK and project dependencies:

```powershell
dotnet build
dotnet test
```

For GUI verification:

```powershell
cd tests/Dip.UiE2E
npm ci
npx playwright test tests/pm-branch-control.spec.mjs tests/commercial-quality-operations.spec.mjs
```
