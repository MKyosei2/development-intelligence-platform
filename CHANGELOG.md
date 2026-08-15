# Changelog

All notable changes to Development Intelligence Platform are documented here.

This project follows the structure of
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Semantic Versioning
will be applied when the first public version is released.

## [Unreleased]

### Added

- GUI Phase 1: GitHub App repository connection, Repository Picker, GitHub-to-DIP import, scoped ProjectState, redesigned Project Overview, Repository/CI/QA GUI, shared loading/error/empty states, SignalR project events, and Playwright GUI E2E.
- Upgrade8-based Development Intelligence expansion: Intelligence Dashboard, deterministic game-session replay, target performance budgets, Unreal Engine collector, real-device hardware backtesting, Change Risk, automatic regression triage, Release Readiness gate, GitHub Issue work-item integration, release provenance/CycloneDX SBOM, OpenTelemetry observability, and multi-repository dependency impact.
- EF Core migration `202608120003_AddDevelopmentIntelligenceExpansion` for the expansion persistence model and global performance-budget presets.
- Initial layered architecture for Domain, Application, Infrastructure, API,
  Worker, Shared contracts, and Blazor Web.
- Collector report ingestion and validation foundations.
- PostgreSQL, Redis, MinIO, Docker Compose, quality gate, and dashboard
  foundations.
- Repository-wide development, security, contribution, formatting, and ignore
  policies.

### Fixed

- Removed the remaining zero-byte Web state placeholder and made optional GitHub workspace sync failures non-fatal after successful project import.
- Replaced zero-byte root configuration files with valid XML, JSON, and text.
- Prevented MSBuild failures caused by empty `Directory.Build.props`,
  `Directory.Build.targets`, and `Directory.Packages.props`.
- Added a valid .NET 8 SDK selection policy through `global.json`.

### Security

- Added a private vulnerability reporting policy.
- Added ignore rules for local secrets, environment files, certificates, and
  generated output.

[Unreleased]: https://github.com/MKyosei2/development-intelligence-platform/commits/main
