# Applying this full-source package

The ZIP is a complete source tree, not a patch-only archive. The safest application path is to keep your `.git` directory, replace the working-tree files from this package, then review `git status` before committing.

If the existing checkout still contains stale zero-byte Razor pages that are intentionally absent from the package, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Apply-PortfolioCleanup.ps1
```

The cleanup removes only zero-byte `*.razor` files under `src/Dip.Web/Pages` and immediately runs the portfolio readiness validator. It does not delete non-empty source files.

Recommended commit subject:

```text
refactor(portfolio): harden publication and reviewer evidence
```

Changing GitHub visibility or creating `v1.0.0-portfolio` is deliberately **not** part of extraction. Those external state changes are guarded by `Publish-Portfolio.ps1` and `Publish-PortfolioRelease.ps1` after the modified SHA has the required evidence.
