# Azure implementation

Hosted UI and .NET API for Nandita Handlooms. The owner confirmed phone Chrome loading after the lazy-photo fix. The hosted sign-in is now boutique email/password; the deployed account release and owner-confirmed password sign-in are tracked in HANDOFF.

- [Boutique accounts and additive schema/recovery](../docs/ACCOUNTS.md)
- [Architecture and API](../docs/ARCHITECTURE.md)
- [Personal Azure profile, code deployment, tests, recovery, troubleshooting](../docs/OPERATIONS.md)
- [Current handoff and outstanding limits](../docs/HANDOFF.md)
- [Implementation history](../docs/CHANGELOG.md)

Routine code updates use `cloud/redeploy.ps1` from the repo root after appropriate checks and deployment authorization. They preserve business records. Schema setup, restricted-user creation, initial import, and owner invitation are one-time/new-environment operations, not steps to repeat for a feature update.

Only the allowlisted four UI assets and compiled API are published. Private fixtures, credentials, backups, records, and repository documentation are not public site assets.
