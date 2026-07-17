# Changelog

All notable changes to Development Intelligence Platform are documented here.

This project follows the structure of
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Semantic Versioning
will be applied when the first public version is released.

## [Unreleased]

### Added

- Initial layered architecture for Domain, Application, Infrastructure, API,
  Worker, Shared contracts, and Blazor Web.
- Collector report ingestion and validation foundations.
- PostgreSQL, Redis, MinIO, Docker Compose, quality gate, and dashboard
  foundations.
- Repository-wide development, security, contribution, formatting, and ignore
  policies.

### Fixed

- Replaced zero-byte root configuration files with valid XML, JSON, and text.
- Prevented MSBuild failures caused by empty `Directory.Build.props`,
  `Directory.Build.targets`, and `Directory.Packages.props`.
- Added a valid .NET 8 SDK selection policy through `global.json`.

### Security

- Added a private vulnerability reporting policy.
- Added ignore rules for local secrets, environment files, certificates, and
  generated output.

[Unreleased]: https://github.com/MKyosei2/development-intelligence-platform/commits/main
