# Source privacy and relay security review — 2026-11-15

This is a scoped source review, not a penetration-test certification or a
promise of zero billing. Do not publish local audit inputs or raw secret values.

## Checked

- 5,080 tracked working-tree paths and 5,962 unique historical file objects
  (260,902,293 bytes) reachable from all local Git refs at the start of the review.
  Text was checked for private-key headers, common provider token formats,
  credential literals, email addresses and the owner's known public identifiers.
- Exact matching against all four locally stored old private access/hash values;
  none were found in tracked source or historical text.
- No private key, recognized provider token or hard-coded credential literal was
  detected by these checks. Heuristic matching can miss unknown secret formats,
  encoded secrets or material outside the examined refs/text. GitHub-side secret
  scanning and review of Cloudflare's token permissions remain useful.
- Both distinct commit email identities are GitHub noreply addresses. The single
  contact email in the current README is present unchanged in upstream main; it
  is an upstream project contact, not evidence of the fork owner's Google email.
- The owner's public GitHub handle is visible in the README, repository URLs and
  workers.dev endpoint, including history. The repository is not anonymous.
  No personal name match was found for the local Windows username. Public account
  identifiers do not authenticate to Cloudflare. No Google credentials were found.

## Public-client boundary

Players get public connection URLs, their random room/seat secrets and temporary
TURN credentials. Account ID, analytics token, TURN generation API token and
billing usage remain on the server. Secrets are entered with Wrangler prompts;
never put them in source, build resources, command-line arguments or chat.

A public TURN credential can be copied and reused outside the game until it
expires. Room seat authorization, per-network request limiting, a shared rolling
issuance cap and the account-wide 900 GB cutoff reduce abuse. They cannot prove
zero abuse, limit an already-issued credential's bandwidth, or guarantee a zero
invoice. Analytics latency/sampling and already-running six-hour sessions can
consume the margin. The current native client does not renew credentials.

The legacy allowlist secret should be removed after deployment. Old local access
files remain ignored and unused; never include .local/, .wrangler/, .dev.vars,
.env files, application data or credential history in distributed archives.
Changing current files does not erase public identifiers from Git history.

## Activation and remaining validation

The guarded service must have its own account-scoped Account Analytics: Read
token and verified billing day configured before it can issue universal TURN
credentials. Missing or incorrect configuration returns STUN only. Live GraphQL
schema/permissions, billing alignment and actual relay connections are not
proven by mocked unit/integration checks. Deployment and player validation must
be recorded separately in PLAYER_VALIDATION.md.

Deployment update: live TURN/SFU analytics and universal credential issuance were
validated on 2026-11-15 after the user supplied a dedicated token through masked
local input. The configured monthly billing day is 14, matching the visible
Realtime renewal date. See PLAYER_VALIDATION.md for scope and remaining checks.
