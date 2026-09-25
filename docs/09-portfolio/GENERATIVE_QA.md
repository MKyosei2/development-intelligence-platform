# Generative QA — Evidence-Grounded Autonomous Debugging

This patch turns the existing autonomous-debug pipeline into an opt-in OpenAI-compatible generative QA flow without allowing the model to verify itself.

## Runtime flow

```text
QA / CI / Runtime Evidence
        |
        v
Secret redaction
        |
        v
OpenAI-compatible provider
        |
        +--> grounded hypotheses
        +--> unified-diff candidate + regression-test draft
        |
        v
Structured-output / Evidence-ID / path / size validation
        |
        v
Existing VerifiedFixService
        |
        +--> isolated Git worktree
        +--> baseline QA
        +--> git apply --check
        +--> patch application
        +--> generated regression tests
        +--> post-patch QA
        +--> critical-finding / tool-coverage comparison
        |
        v
ReadyForHumanReview
```

The model never applies or merges a change into the source workspace. A model response is advisory until executable verification succeeds.

## Configuration

Keep `Enabled=false` by default in committed configuration. Enable it only in the environment used for the demo or controlled runtime.

Environment-variable example:

```powershell
$env:GenerativeQa__Enabled="true"
$env:GenerativeQa__Endpoint="https://api.openai.com/v1/chat/completions"
$env:GenerativeQa__Model="<your-model>"
$env:GenerativeQa__ApiKey="<secret>"
$env:GenerativeQa__TimeoutSeconds="90"
$env:GenerativeQa__MaxOutputTokens="4096"
```

Do not commit a real API key. ASP.NET Core configuration maps `GenerativeQa__ApiKey` to `GenerativeQa:ApiKey`.

When `GenerativeQa:Enabled=false`, DIP keeps `DeterministicLocalDebugAgent` as a local baseline. When enabled, incomplete provider configuration fails closed during service resolution instead of silently claiming an AI run.

## Safety boundaries

- Evidence and repository context are redacted before transmission.
- Model output must be strict JSON.
- Every hypothesis and patch candidate must cite an Evidence ID present in the input.
- Invented Evidence IDs are rejected.
- Confidence is normalized to `[0,1]`.
- Patch file count and changed-line count are bounded.
- Regression-test paths must be workspace-relative and cannot traverse `..`.
- Existing `VerifiedFixService` still rejects protected paths, test/diagnostic suppression, malformed diffs, missing QA coverage, and new Critical findings.
- Human review remains mandatory after successful verification.

## Portfolio claim boundary

Use the phrase "生成AIを組み込んだQAシステム" only after running the final portfolio revision with `GenerativeQa:Enabled=true` and retaining runtime evidence that records the provider/model response and the subsequent isolated verification result. Source presence alone is `Implemented`, not `Runtime Verified`.
