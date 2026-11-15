# OpenZ online invitations

This service connects two players without exchanging IP addresses. It never
transports simulation or voice traffic. Those use encrypted libdatachannel
WebRTC connections, directly where possible and through TURN when both players
have private access.

## Deploy the free invitation service

1. Create a Cloudflare account at https://dash.cloudflare.com/sign-up. No domain,
   Cloudflare Tunnel, Magic Network or paid Workers plan is needed.
   Open the Workers dashboard once to initialize a free workers.dev subdomain
   if the account is brand new.
2. From the repository root, run `./tools/Deploy-DuelService.ps1 -LoginOnly`.
   Authorize Wrangler in your browser. Do not paste API tokens into chat.
3. Run `./tools/Deploy-DuelService.ps1`. The script validates and deploys the
   Worker and SQLite Durable Objects, checks `/health`, and writes only the
   public endpoint into `DuelServiceConfiguration.cs`.
   A newly registered workers.dev subdomain can take several minutes to obtain
   its HTTPS certificate. If the initial health request fails during provisioning,
   wait for the certificate and rerun the helper; do not disable TLS validation.
4. Build and share the **entire** player output folder. On first normal launch,
   the Windows player registers per-user `openz://` invitation links. The link
   opens a web invitation with a Join button; copying the code works too.

The owner deployment enables private TURN. Public users still get STUN only:
TURN requires both players' codes to match the server-only allowlist. On a new
account, set `TURN_ENABLED` to `"false"` until its private credentials are ready.
Workers Free and SQLite Durable Objects Free have resource limits; exceeding
them causes affected operations to fail, rather than upgrading the account.
The invitation service can become unavailable at its free limits. It is not a
dedicated game server.

## Enable private Cloudflare TURN

Only do this when the owner accepts Cloudflare Realtime's usage-based billing.
The free TURN/SFU allowance is currently 1,000 GB per month; it is not a hard
spending cap. Restricting access does not guarantee a zero bill.

1. In Cloudflare Realtime / TURN, create a TURN key following
   https://developers.cloudflare.com/realtime/turn/generate-credentials/.
2. Run `./tools/New-DuelPrivateAccess.ps1` once. Private owner and brother codes
   go into ignored `.local/duel-private/`. Do not put those codes into Git,
   a release, source files or a chat message.
3. From `services/duel-signaling`, store secrets with Wrangler's interactive
   secret prompts:

   ```powershell
   npx wrangler secret put TURN_KEY_ID
   npx wrangler secret put TURN_API_TOKEN
   npx wrangler secret put PRIVATE_RELAY_HASHES
   ```

   The first two values come from Cloudflare. For the third, paste the contents
   of `.local/duel-private/PRIVATE_RELAY_HASHES.txt`, not either player's code.
4. Ensure `TURN_ENABLED` is `"true"` in `wrangler.jsonc` and deploy again. Keep the
   secrets server-side. Redeploying with `TURN_ENABLED="false"` disables new
   credentials; previously issued credentials last until their expiration.
5. Each private player opens **1VS1 > Online Settings** and pastes their own
   private access code once. On Windows it is protected with the current user's
   DPAPI and stored under the game's persistent data directory. No Cloudflare
   account or Google login is needed in the game.
   The owner can alternatively run `tools/Install-DuelPrivateAccess.ps1` to
   install their local code with Windows DPAPI. The brother receives only
   `brother-access.txt` separately and pastes that code once in the game.

Both room participants must be authorized. Private authorization is a bearer
code; someone who steals it can impersonate that private player. Revoke a code
by removing its SHA-256 hash from `PRIVATE_RELAY_HASHES` and uploading the
updated secret. Never trust a username, hidden button or client-side flag.

## Transport and operational limits

- Native dependency: libdatachannel **v0.24.6**, Mbed TLS **v3.6.5**, Windows x64.
  `tools/Build-DuelNative.ps1` builds the pinned upstream revisions, includes
  licenses, and creates a separate native-dependency backup in `builds/`.
  Binary dependencies are local, not committed. Restore that package archive
  under `source/Packages/org.openz.datachannel/`, or rebuild it on a fresh clone.
- Game frames use reliable ordered SCTP. Existing snapshots are fragmented into
  12,000-byte messages with bounded reassembly and queue limits. Simulation and
  host authority stay unchanged. The build fingerprint is checked before play.
- Voice retains the existing 16 kHz mu-law codec, carried on a separate lossy,
  unordered encrypted data channel. No raw voice UDP port is used online.
- This libjuice-backed build supports **TURN over UDP**, not TURN over TCP/TLS.
  Networks blocking all UDP can still fail. No universal connectivity guarantee
  is claimed. Adding TCP/TLS relay transport requires a compatible ICE backend.
- Full ICE gathering precedes SDP exchange. Signaling is closed once connected;
  no game or voice packets pass through Workers. Rooms expire after 30 minutes.
  Existing peer connections continue after room deletion.
- Private TURN credentials expire after six hours. No credential refresh or ICE
  restart during a match is implemented. Start a new invitation for another
  match after a failure; long relay sessions may disconnect at expiry.
- Room codes contain 96 random bits. Seats and peer handshake secrets contain
  192 random bits. Rooms have two seats, version checks, bounded messages,
  hibernating WebSockets, expiration, and per-network request limits. Application
  observability is disabled so private credentials are not application logs.
- The endpoint is pinned in the build or explicit local settings. Invitations
  do not redirect private access codes to arbitrary servers.
- Direct connections reveal network addresses to peers. STUN does not hide IPs.
- Advanced TCP/IP and its existing UDP voice are preserved as a separate mode.

## Validate

```powershell
cd services/duel-signaling
npm ci --ignore-scripts
npm test
node test/integration.mjs
npx wrangler deploy --dry-run
cd ../..
./tools/Build-DuelNative.ps1
dotnet run --project tests/WebRTC/WebRTC.Checks.csproj -c Release
./tools/Build-And-Test-Duel.ps1
```

To check an actual deployment without using paid relay traffic:

```powershell
$env:OPENZ_DUEL_SERVICE_URL = 'https://your-worker.your-subdomain.workers.dev'
node test/live.mjs
```

For an end-to-end local online proof, run `npx wrangler dev --local --port 8787`
in the service directory. In another shell:

```powershell
$env:OPENZ_DUEL_SERVICE_URL = 'http://127.0.0.1:8787'
./tools/Test-DuelPlayers.ps1 -Online
```

For a real private relay proof, set the public service URL and
`OPENZ_DUEL_RELAY_ACCESS` in the test process environment, then run
`./tools/Test-DuelPlayers.ps1 -Online -RelayOnly`. Do not print the private
environment variable. A local direct proof does **not** validate Cloudflare TURN
or Internet NAT traversal. Also test two machines on separate Internet networks
before calling the feature ready for those users.
