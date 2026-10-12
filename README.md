# Nandita Handlooms

Private inventory, pricing, sales, and quarterly tax-preparation application for a small California saree boutique. Track purchases in India, freight and duties to the USA, individual colors within a SKU, Instagram links, product photos, customer shipping, and profit.

**Website:** [Open the boutique](https://gray-grass-0cab2cd10.6.azurestaticapps.net/). Sign in with the authorized personal Microsoft account. Cloud is the active shared ledger; the local app has a separate database.

## Documentation

| Read this | For |
| --- | --- |
| [Handoff](docs/HANDOFF.md) | Starting a new chat: current status, decisions, limits, and next steps. |
| [Requirements](docs/REQUIREMENTS.md) | Business rules, calculations, import decisions, and acceptance examples. |
| [Feature guide](docs/FEATURES.md) | What each screen does and how to use it. |
| [Architecture](docs/ARCHITECTURE.md) | Code map, storage, APIs, authentication, concurrency, and photos. |
| [Operations](docs/OPERATIONS.md) | Run locally, back up, test, deploy, and troubleshoot. |
| [Change history](docs/CHANGELOG.md) | Implemented milestones, incidents, and verification evidence. |
| [Agent instructions](AGENTS.md) | Context-reading and data-protection rules for coding agents. |

Markdown is the source format: readable directly in Git or an editor, searchable by future chats, and maintained alongside code. Requirements define intended business behavior; the handoff records current operational status.

## Local use

Python 3.10+ is required. Double-click **Start Boutique.bat** and enter the access code shown in its console. Keep that window open. The default address is `http://localhost:8765/`.

**Start Boutique WiFi.bat** enables same-network phone access while the computer stays awake. Company management and guest-network restrictions can block it; use the hosted website when appropriate. Detailed commands and recovery steps are in [Operations](docs/OPERATIONS.md).

## Data ownership

Git contains code and documentation, not business records. Local records live in `data/boutique.sqlite3`; hosted records live in Azure SQL. There is no automatic synchronization between them. Download full JSON backups from Settings & backup. A source clone alone cannot recover inventory or sales.

The application records payment-method labels; payments happen outside it. All sales, including cash, participate in reporting. Quarterly worksheets support filing preparation, and the owner supplies actual tax and documented tax treatment.
