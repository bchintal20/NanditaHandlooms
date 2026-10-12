# Boutique accounts

## Requirement and release status

Owner authorized October 12, 2026: invite-only email/password accounts independent of Microsoft/GitHub sessions, remember-me, and password recovery. Existing financial records, local PIN mode, free hosting plans, and no-email-service preference remain intact. Deployed October 12, 2026 after isolated account/UI/ledger validation. Financial data was preserved and anonymous/forged-session/origin checks were verified live. The owner confirmed password setup and successful sign-in; detailed verification status is tracked in HANDOFF.

One owner manages up to 20 accounts. Members have full ledger read/write/backup/restore access but cannot manage accounts. There is no public registration, granular sales-only role, email verification service, or automatic reset email. The owner shares private invitations/reset links directly through their usual channel. Account changes are separate from the financial ledger and its backups.

## Owner and member workflows

The first owner receives a private setup link, sets a 15–128-character password/passphrase, saves a recovery code in their password manager, then signs in with email/password. A setup/reset link expires after 24 hours and works once. A new link invalidates the previous link for that person. Links put tokens in URL fragments; the UI immediately removes the fragment before requesting inventory.

Sign in with Remember me for a 30-day persistent session. Without it the cookie is browser-session scoped and server access expires after 12 hours. These are absolute lifetimes, not sliding sessions. Use only trusted personal devices for remembered access.

Settings & backup contains Change password, Manage accounts (owner), and Sign out. Account management requires signing in within the last 15 minutes. The owner creates member invitations, generates reset links, disables accounts, or reinvites disabled accounts. Invitees see the same boutique data. Disabling revokes existing sessions and tokens; reinvitation requires a new password. The owner cannot disable their own account.

Forgot password uses email + saved recovery code + new password. Recovery rotates the code, invalidates all sessions/links, and requires sign-in again. Changing password requires the current password and also rotates recovery/revokes all sessions. If the owner loses both password and recovery code, an authorized operator can issue a reset link using private SQL tooling; no work/personal browser account switching is required for routine use.

## Architecture and protections

`BoutiqueAccounts.cs` owns pure transitions; `SqlAccountStore.cs` persists them; `AccountEndpoints.cs` exposes account operations; `cloud/account-ui.js` is merged into hosted `app.js` by the UI builder. Local server remains unchanged.

Additive `dbo.BoutiqueAccounts` stores one JSON document with users, hashed sessions, hashed invitation/reset tokens, and rate counters. It is separate from BoutiqueLedger/History. SQL app user receives only SELECT/UPDATE on this initialized singleton. Writes use serializable row locks, so one-use tokens and rate counters work across Functions instances. Failed credential attempts still commit counters; unexpected failures roll back.

Passwords use randomly salted PBKDF2-HMAC-SHA256 with 600,000 iterations. Sessions, invitations, and recovery codes use random 256-bit tokens; only SHA-256 digests are stored. Login does a dummy password verification for nonexistent accounts. Auth responses are private/no-store. No passwords or session tokens go in ledger JSON, browser storage, docs, telemetry, or public assets.

Session cookie is `__Host-boutique_session`, Secure, HttpOnly, SameSite=Strict, Path=/, no Domain. There are at most ten sessions per user. Every ledger, restore, backup, and photo request verifies the session and current enabled account. Changing/resetting password or disabling an account invalidates access on all devices.

Every account mutation requires exact configured Origin and JSON, with 8-KB body limit. Public auth has persistent limits: 100 attempts globally and five per email/action or setup token in 15 minutes. Password-change attempts are separately limited. Abuse can exhaust a short shared window; this deliberately small private deployment does not have distributed edge bot protection or MFA yet.

SWA routes allow requests through to the backend without requiring a Microsoft platform login. This route change does **not** make ledger/photo data public: backend session validation is mandatory before reading data. Public health does not access account or ledger data. Missing/malformed session cookies fail without opening SQL.

Microsoft platform headers do not authorize any boutique endpoint. `BoutiqueOwnerEmail` remains only for operator owner setup/recovery, not routine sign-in. Initial owner activation is issued by private operator tooling.

## API

| Method/path | Access and purpose |
| --- | --- |
| GET `/api/account/me` | Session identity only. |
| POST `/api/account/login` | Email/password, optional remember; sets cookie. |
| POST `/api/account/activate` | One-use invite/reset token and new password; returns new recovery code once. |
| POST `/api/account/recover` | Email/recovery code/new password; rotates recovery and revokes sessions. |
| POST `/api/account/logout` | Revokes current token and expires cookie. |
| POST `/api/account/password` | Session + current password; rotates/revokes. |
| GET `/api/account/users` | Owner; metadata only, no hashes/tokens. |
| POST `/api/account/invite`, `/reset`, `/disable`, `/reinvite` | Recently authenticated owner. |

Ledger/photo endpoints now return 401 for missing/expired boutique sessions. Platform headers alone cannot grant data access. Financial APIs/backup/lazy-photo protocol are otherwise unchanged.

## Migration and operations

1. Run isolated account/UI tests and normal ledger regressions.
2. With an operator connection (private environment variable `BoutiqueSqlConnection`), run `dotnet run --project cloud/Tools -- setup-accounts`. This creates/initializes only the account table and grants app SELECT/UPDATE; it does not touch business tables or import records.
3. Set `BoutiqueOwnerEmail` privately; run `dotnet run --project cloud/Tools -- owner-invite <private-token-file>`. Store its output file in ignored private artifacts. The command prints no token. Build a private website `/#account=TOKEN` setup link for the owner without putting it into Git/logs.
4. Deploy code and route changes together using the existing personal-account redeploy workflow. Verify anonymous ledger/backup/photo denial and authenticated account workflows. Remove temporary SQL diagnostic/operator firewall access afterward.
5. Owner activates and confirms normal sign-in. Delete private token files when no longer needed. Record actual validation/deployment status in HANDOFF/CHANGELOG.

Initial migration is additive and must run before new deployed APIs. SQL free tier/hosting plan is unchanged. Authentication adds a small SQL metadata read on authorized requests. Auth records are outside downloadable ledger backups: protect them with operator-only database backups and securely reissue accounts after financial-only recovery. Never restore financial JSON as an account backup.

Operator owner reset: privately configure the operator connection and matching `BoutiqueOwnerEmail`, then run `dotnet run --project cloud/Tools -- owner-reset <private-token-file>`. This verifies the activated owner, produces a one-use 24-hour reset token, and invalidates sessions on activation. Keep the generated link private; do not modify password hashes manually.

Tests: `dotnet run --project cloud/AccountTests` runs synthetic account checks without private ledger fixtures. Add `-- --sql` only for isolated localhost SQL: creates/drops a uniquely guarded disposable test database and verifies token concurrency and persisted failed-attempt counters. Browser account UI checks use mocked APIs/synthetic records; no live business writes.

Security references: [OWASP password storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html), [session management](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html).
