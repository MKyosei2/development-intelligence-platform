## Purpose

Describe the engineering problem this pull request solves. Avoid feature-list-only descriptions.

## Changes

- 
- 
- 

## Affected areas

- [ ] Domain
- [ ] Application
- [ ] Infrastructure
- [ ] API
- [ ] Web dashboard
- [ ] Worker
- [ ] Python analysis service
- [ ] Collector / engine integration
- [ ] Database migration
- [ ] CI/CD
- [ ] Documentation / portfolio evidence

## Technical decision

Explain the important trade-off or invariant introduced/changed by this PR. If a threshold, retry count, timeout, score, or performance target changed, explain why.

## Verification

List only checks that actually executed for this revision.

```text
Commands/workflows:
- dotnet build ...
- dotnet test ...
- pytest ...
- Integration and E2E Tests
- Security Scan
```

If a runner or dependency was unavailable, record it as unavailable rather than PASS.

## Quality and risk

- Expected quality impact:
- Performance impact:
- Security impact:
- Backward compatibility:
- Database/configuration impact:
- Rollback method:

## Evidence

Add screenshots, logs, benchmark results, trace IDs, sample collector reports, or artifact links. State the exact commit/SHA for measured results.

## Checklist

- [ ] The change has a single clear purpose.
- [ ] Commit subjects describe intent using the repository's Conventional Commit rules.
- [ ] Tests were added or updated for behavior changes.
- [ ] Numeric thresholds/penalties are named, documented, or configurable rather than unexplained magic numbers.
- [ ] Public contracts and schemas remain backward compatible, or the breaking change is documented.
- [ ] Secrets and personal information are not included.
- [ ] Error handling and logging were reviewed.
- [ ] Documentation was updated when behavior changed.
- [ ] Generated files and local artifacts are not committed unless they are intentional evidence.
- [ ] Historical evidence is not presented as proof for a newer revision.
