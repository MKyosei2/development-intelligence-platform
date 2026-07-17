# Security Policy

## Supported versions

This project is currently under active development. Security fixes are applied
to the latest revision of the default branch.

| Version | Supported |
|---|---|
| Default branch / unreleased | Yes |
| Older snapshots and forks | No |

## Reporting a vulnerability

Do not disclose a suspected vulnerability in a public issue, discussion, pull
request, log, screenshot, or sample payload.

Use GitHub Private Vulnerability Reporting for this repository when it is
available. Otherwise, contact the repository owner privately through GitHub
and provide:

- a concise description of the issue;
- affected component, endpoint, worker, collector, or configuration;
- reproduction steps or a minimal proof of concept;
- expected and actual behavior;
- potential impact;
- suggested mitigation, when known.

Do not include live credentials, access tokens, webhook secrets, personal
information, or production data.

## Response process

The maintainer will attempt to:

1. acknowledge the report;
2. reproduce and assess the impact;
3. prepare a fix and regression test;
4. coordinate disclosure after users have a reasonable opportunity to update.

Response times are not guaranteed while the project remains unreleased.

## Security expectations

Contributors must:

- store secrets in environment variables, user secrets, or a secret manager;
- never commit `.env`, tokens, passwords, private keys, or production payloads;
- validate collector and webhook input;
- preserve webhook and message idempotency;
- use parameterized database access;
- keep dependencies and container images patched;
- avoid logging sensitive request bodies or credentials.
