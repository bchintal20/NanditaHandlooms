# Azure implementation

Hosted UI and .NET API for Nandita Handlooms. The owner has confirmed access and phone Chrome loading after the lazy-photo fix.

- [Architecture and API](../docs/ARCHITECTURE.md)
- [Personal Azure profile, code deployment, tests, recovery, troubleshooting](../docs/OPERATIONS.md)
- [Current handoff and outstanding limits](../docs/HANDOFF.md)
- [Implementation history](../docs/CHANGELOG.md)

Routine code updates use `cloud/redeploy.ps1` from the repo root after appropriate checks and deployment authorization. They preserve business records. Schema setup, restricted-user creation, initial import, and owner invitation are one-time/new-environment operations, not steps to repeat for a feature update.

Only the allowlisted four UI assets and compiled API are published. Private fixtures, credentials, backups, records, and repository documentation are not public site assets.
