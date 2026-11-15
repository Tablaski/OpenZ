# OpenZ online invitations

This service connects two players without exchanging IP addresses. It never
transports simulation or voice traffic. Those use encrypted libdatachannel
WebRTC connections, directly where possible and through TURN while the shared
allowance is available. No relay password or Google account is required.

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

## Universal TURN with preventive usage cutoff

Cloudflare Realtime currently includes 1,000 GB of shared TURN/SFU egress before
usage charges. This is an allowance, not a guaranteed spending cap. The guard
stops new credentials at **900,000,000,000 bytes** (900 decimal GB), leaving a
100 GB margin. Statistics may lag or be estimated, existing sessions can keep
using their credentials, and concurrent traffic can consume the margin. Zero
billing and abuse prevention are not guaranteed.

1. Keep the existing server-only TURN key and generation token, or create them
   following https://developers.cloudflare.com/realtime/turn/generate-credentials/.
2. Create a separate account-scoped token with **Account Analytics: Read** for
   https://developers.cloudflare.com/realtime/turn/analytics/.
3. Store these values with interactive Wrangler secret prompts. Never paste them
   into chat, source, a player, an issue, or a command-line argument:

   ~~~powershell
   npx wrangler secret put TURN_KEY_ID
   npx wrangler secret put TURN_API_TOKEN
   npx wrangler secret put CF_ACCOUNT_ID
   npx wrangler secret put CF_ANALYTICS_TOKEN
   npx wrangler secret put TURN_BILLING_DAY
   ~~~

4. Set the server-side TURN_BILLING_DAY secret to the actual **UTC day (1–31)** the
   account's billing period begins. This is deliberately absent by default; do not
   assume a calendar-month reset. Days 29–31 clamp to shorter months. Confirm the
   cycle against Cloudflare Billing before enabling the guard. For subscriptions
   whose boundaries do not match UTC midnight/monthly day arithmetic, leave TURN
   disabled until the cycle logic has been adapted. Account IDs stay server-side.
   The interactive helper tools/Configure-DuelRelayBudget.ps1 configures the
   account ID, analytics token and billing day without putting secrets in source.
5. Deploy with tools/Deploy-DuelService.ps1. TURN_ENABLED=true enables universal
   issuance only when all configuration and analytics checks succeed. Missing
   secrets/day, permission errors, malformed data and timeouts return STUN only.
6. Delete the obsolete PRIVATE_RELAY_HASHES Worker secret after deploying. Old
   private access files in .local/duel-private/ and duel-private-access.bin in
   the game's local data directory are no longer read or distributed. They are
   left in place to avoid changing personal local data. Never archive them with
   source or a player. Rotate the old TURN key to invalidate old credentials if
   needed; this can interrupt existing relayed games.

A singleton SQLite Durable Object shares the decision across every room. It
queries all TURN keys plus SFU egress, caches successful totals for at most 60
seconds, retains the highest observed total and latches cutoff until the next
billing period. A global rolling cap allows at most 120 successful budget grants
per hour, in addition to 120 HTTP attempts per network per hour. Repeated ICE
requests also count conservatively. Provider failures consume a grant. These
limits can reduce availability under abuse; they are not bandwidth limits.

The game receives only STUN/TURN URLs, temporary TURN credentials and a generic
availability reason. It receives no analytics data, account ID, permanent API
token, email, Google information or allowlist. A public client can extract and
reuse its temporary TURN credentials until expiry, including outside the game.
The game has no Google integration. Public service URLs and GitHub identity are
public identifiers, not authentication secrets. Direct WebRTC exposes peer IPs.

TURN_ENABLED=false blocks new issuance, including cached credentials, but does
not revoke already-issued credentials or disconnect active relays. Six-hour
credentials preserve the existing match-duration behavior; no mid-match renewal
is available. A new match can use direct STUN when the relay is unavailable.
Workers/Durable Objects Free limits can also make invitations unavailable.

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
- TURN credentials expire after six hours. No credential refresh or ICE
  restart during a match is implemented. Start a new invitation for another
  match after a failure; long relay sessions may disconnect at expiry.
- Room codes contain 96 random bits. Seats and peer handshake secrets contain
  192 random bits. Rooms have two seats, version checks, bounded messages,
  hibernating WebSockets, expiration, and per-network request limits. Application
  observability is disabled so credentials are not application logs.
- The endpoint is pinned in the build or explicit local settings. Invitations
  do not redirect room credentials to arbitrary servers.
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

Only when player testing is explicitly requested, run a real relay proof with
./tools/Test-DuelPlayers.ps1 -Online -RelayOnly against the configured public
service. No private access code is required. A local direct proof does not
validate Cloudflare TURN or Internet NAT traversal. Two separate Internet
networks and actual headset quality still require manual player testing.
