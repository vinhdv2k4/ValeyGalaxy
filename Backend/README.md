# Game account API

The Unity startup prefabs call this ASP.NET Core API for registration, login, logout and farmer profiles. Accounts and profiles are stored in a real SQLite database at `GameBackend/data/game.db` by default. Passwords are stored as BCrypt hashes; bearer session tokens are stored as SHA-256 hashes and expire after seven days. All clients pointed at the same API and database share the account system. Each account owns its own character, wallet, inventory and placed home chests.

## Run locally

Install .NET 9 SDK, then run `./Backend/start-local.ps1` from PowerShell. Keep the server running while playing the Unity project. The startup prefab's `StartupMenuFlow.apiBaseUrl` defaults to `http://127.0.0.1:5055`; change this field in the prefab when using another server.

The file database is created on first start. `GAME_DB_PATH` can set a different SQLite file path. To use PostgreSQL instead, set the `ConnectionStrings__Postgres` environment variable to an Npgsql connection string before starting the API. Do not set both database options for the same account set; they are separate databases. Unity permits HTTP for local testing, and the startup client accepts HTTP only for loopback addresses.

To test on one computer, start the API and open `http://127.0.0.1:5055/api/health/ready`. Its `ready` response confirms that the API can reach the database. Sign in using two game clients pointed at that URL. The menu shows `Connected | 2 online` after both have sent a heartbeat. The client sends a heartbeat every 15 seconds in both the menu and gameplay. A player counts as online for 60 seconds after the last successful heartbeat, provided their session is still valid. Logout removes that session immediately. This is an API connection and account presence check; it does not synchronize player movement or put the players into the same live map.

## Demo with a friend on another network

Start the API with `./Backend/start-local.ps1` from the repository root. Confirm `http://127.0.0.1:5055/api/health/ready` returns `ready`. Then run `cloudflared tunnel --url http://127.0.0.1:5055` in a second terminal and keep both processes open. Send the generated HTTPS URL to the other player. A **Development Build** can use `Game.exe -api=https://YOUR-URL.trycloudflare.com`; only Editor and Development Builds remember that override in PlayerPrefs. Release players always use the URL shipped in `StartupMenu.prefab` and clear the old `ServerApiUrl` preference. Both players must log in and enter `GamePlay` in a public location such as Town to see each other. `/api/presence/online` counts signed-in clients with recent heartbeats; seeing another avatar verifies the world-presence path separately.

The temporary URL changes when a new Quick Tunnel starts. It is intended for a small development demo, not a stable 100-player host. Cloudflare documents a limit of 200 in-flight requests and no uptime guarantee for Quick Tunnels: https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/. The current game uses HTTP polling for positions at 10 requests per second per player; capacity and latency for 100 players have not been measured. No dedicated Unity game server is started by these commands.

World presence accepts client-reported positions, validates basic input, limits active players to the session capacity (16 by default), and sends only avatars in the same location. It does not validate movement against map collision or warp rules. If a separate host process uses `/api/world/host-heartbeat`, configure a strong `WORLD_HOST_SECRET` on the API host and keep it out of the Unity client and Git. Without that setting, the host-heartbeat endpoint is disabled. External host registrations expire after 30 seconds without a heartbeat.

## Put the API online

Follow the step-by-step Vietnamese guide [RAILWAY_DEPLOY.vi.md](RAILWAY_DEPLOY.vi.md). Railway must build from the **repository root `/`**, using `Dockerfile.railway` and `railway.toml`; the backend-only root omits the referenced Unity JSON. The root `.dockerignore` allows only backend source and the 17 referenced data files. The legacy `GameBackend/Dockerfile` also requires the repository root as its build context.

Use PostgreSQL reference variables, `ASPNETCORE_ENVIRONMENT=Production`, and a private `SECURITY_HASH_SECRET` of at least 32 characters. Production refuses to fall back to SQLite. The API binds `0.0.0.0:$PORT`; readiness is `/api/health/ready`. Keep one replica because avatar presence is held in process memory. Keep PostgreSQL private and enable volume backups. After generating the stable HTTPS domain, run `./Backend/set-production-url.ps1 -BaseUrl https://YOUR-DOMAIN.up.railway.app` to update both Unity URL fields, and build with Development Build off.

As of 2026-11-03, this project targets .NET 9, whose support ends on 2026-11-10. Upgrade the backend/test target frameworks, EF Core/Npgsql and other framework dependencies, SDK/runtime Docker images, and local SDK to .NET 10 LTS before long-term operation. No .NET 10 migration is claimed by these deployment changes.

See the [Railway ASP.NET Core guide](https://docs.railway.com/guides/aspnet-core) and [Railway PostgreSQL guide](https://docs.railway.com/databases/postgresql) for the current dashboard steps and plan limits.

`GET /api/game/state` returns the account's wallet, 36-slot inventory, and physical chests with their FarmHouse tile positions. Buying, selling, crafting and placing a chest, and transferring stacks use server-side rules and a `requestId` for safe retry. Clients cannot directly set money or replace inventory. Unity loads the server wallet and bag when entering gameplay. Inside FarmHouse, press **C** to enter chest placement, then click a nearby valid floor tile. The server consumes 50 wood and saves the chest position and contents to that account. Click a placed chest or press **E** beside it to open that chest and transfer items. Press **Esc** to close or cancel placement. The starter bag contains 100 wood so new accounts can try the flow. Chests saved by the earlier storage screen are placed first without charging wood again, preserving their contents. The current Unity world simulation still handles harvesting, fishing, and dropped items locally; those rewards need a trusted world-server flow before they can be safely granted to online inventory. These local rewards are not yet authoritative or persisted online.

## Item catalog and lookup

The current online catalog has **20 item types**, matching the 20 `ItemData` assets in `Assets/Resources/OnlineItems`. It is not the complete Stardew item set: `Assets/Content/Data/Objects.json` contains 807 keys with empty values, so names, prices, stack rules and gameplay behavior for those objects cannot be imported reliably. Server-side buy, sell and transfer rules come from `GameBackend/item-catalog.json`; the server validates and loads this file once into an immutable ID lookup. `ItemCatalogConsistencyTests` checks that every catalog ID and stack limit matches exactly one Unity asset. To add an item, add its Unity `ItemData` asset and server catalog record, then run the tests. Add the authoritative gameplay rule that grants or consumes that item before making it available online.

Account names, emails and tokens have database indexes. Game state is fetched by the player's primary key; the server now fetches only `UserId` from the indexed session token for game requests. Item definition lookup by ID uses the in-memory catalog, while searches inside one player's bag or chest scan at most 36 slots per container. The current state remains a versioned per-player JSON aggregate with at most 20 chests. This keeps ownership and atomic transfer simple at the current scope, but every mutation rewrites the aggregate. Before high-frequency shared-world gameplay or cross-player item searches, move inventory/chest slots to relational rows, add indexes on `(OwnerId, ItemId)` and `(ChestId, Slot)`, and load-test the intended concurrency. No item-name search endpoint exists yet.

For an internet deployment, host the API on a server with HTTPS, set the Unity prefab URL to that HTTPS address, provision and back up the database, and add email ownership verification and password reset delivery. The local starter has no email provider configured. `EnsureCreated` initializes a new database; schema migrations are needed before changing a database already in use.

Set a private, stable `SECURITY_HASH_SECRET` for any production deployment. The API now refuses to start in `Production` without it; changing this value later invalidates previously issued signed plot IDs. The development-only fallback is for local tests, including temporary tunnel demos.

## Routes

- `POST /api/auth/register`: username, email, password, confirmPassword.
- `POST /api/auth/login`: identity (username or email), password.
- `GET /api/auth/me`: bearer token.
- `POST /api/auth/logout`: bearer token.
- `GET /api/health/ready`: confirms database connectivity; no token required.
- `POST /api/presence/heartbeat`: bearer token; updates server-side presence and returns server time, session expiry and online player count.
- `GET /api/presence/online`: bearer token; returns distinct accounts seen in the past 60 seconds.
- `PUT /api/farmer`: bearer token and the character creation fields.
- `GET /api/game/state`: bearer token; wallet, inventory and chests.
- `POST /api/game/chests`: bearer token, requestId, name, location (`FarmHouse`), tileX, tileY; consumes 50 wood and rejects occupied tiles.
- `POST /api/game/chests/{id}/place`: bearer token, requestId, location, tileX, tileY; positions an old unplaced chest once.
- `POST /api/game/transfer`: bearer token, requestId, source/target chest IDs and slots, amount.
- `POST /api/game/buy`: bearer token, requestId, itemId, amount.
- `POST /api/game/sell`: bearer token, requestId, slot, amount.

The account and farmer persistence test is in `GameBackend.Tests/AccountFlowTests.cs` and runs with `dotnet test Backend/GameBackend.Tests/GameBackend.Tests.csproj`.
