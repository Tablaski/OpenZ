import { SkirmishRoom } from "./skirmish.js";

const STUN = [{ urls: ["stun:stun.cloudflare.com:3478"], username: "", credential: "" }];
const ROOM_LIFETIME = 30 * 60 * 1000;
const CONNECTED_ROOM_LIFETIME = 6 * 60 * 60 * 1000;
const MAX_BODY = 65536;
export function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json", "Cache-Control": "no-store" } });
}
export function randomToken(bytes = 24) {
  return btoa(String.fromCharCode(...crypto.getRandomValues(new Uint8Array(bytes)))).replaceAll("+", "-").replaceAll("/", "_").replaceAll("=", "");
}
export async function peerIce(env, storage, room, player) {
  let budget;
  const direct = reason => json({ iceServers: STUN, relay: false, relayReason: reason, relayRemainingBytes: budget?.relayRemainingBytes });
  if (env.TURN_ENABLED !== "true") return direct("disabled");
  if (!env.TURN_KEY_ID || !env.TURN_API_TOKEN) return direct("unavailable");
  // Expired credentials are never reused. Cached credentials do not authorize
  // a new allocation after the budget has closed.
  try {
    const response = await env.RELAY_BUDGET.get(env.RELAY_BUDGET.idFromName("account")).fetch(new Request("https://internal/check"));
    if (!response.ok) return direct("unavailable");
    budget = await response.json();
  } catch { return direct("unavailable"); }
  if (budget.allowed !== true) return direct(["budget", "busy", "disabled"].includes(budget.reason) ? budget.reason : "unavailable");
  if (!player.ice || !(player.iceExpires > Date.now())) {
    const ttl = Math.min(21600, Math.max(600, Number(env.TURN_TTL) || 21600));
    let response;
    try {
      response = await fetch(`https://rtc.live.cloudflare.com/v1/turn/keys/${env.TURN_KEY_ID}/credentials/generate`, {
        method: "POST", headers: { Authorization: `Bearer ${env.TURN_API_TOKEN}`, "Content-Type": "application/json" },
        body: JSON.stringify({ ttl }), signal: AbortSignal.timeout(8000)
      });
      if (!response.ok) return direct("unavailable");
      const credentials = await response.json();
      // libjuice supports TURN over UDP. Avoid advertising unsupported TCP/TLS paths.
      const source = Array.isArray(credentials.iceServers) ? credentials.iceServers : [credentials.iceServers];
      player.ice = source.filter(server => server && typeof server.username === "string" && server.username && typeof server.credential === "string" && server.credential).map(server => ({ urls: (Array.isArray(server.urls) ? server.urls : [server.urls]).filter(url => typeof url === "string" && /^turn:/.test(url) && !url.includes("transport=tcp")), username: server.username, credential: server.credential })).filter(server => server.urls.length);
      if (!player.ice.length) return direct("unavailable");
      player.iceExpires = Date.now() + ttl * 1000;
      await storage.put("room", room);
    } catch { return direct("unavailable"); }
  }
  return json({ iceServers: [...STUN, ...player.ice], relay: true, relayRemainingBytes: budget.relayRemainingBytes });
}

export function validRoom(room) { return /^OPNZ-[A-F0-9]{24}$/.test(room); }
// All rooms share one guard. Only the remaining allowance is exposed to authenticated seats.
export function billingPeriod(day, now = Date.now()) {
  if (!/^([1-9]|[12][0-9]|3[01])$/.test(String(day))) throw new Error("Missing billing day.");
  const date = new Date(now);
  const boundary = (year, month) => new Date(Date.UTC(year, month,
    Math.min(Number(day), new Date(Date.UTC(year, month + 1, 0)).getUTCDate())));
  let start = boundary(date.getUTCFullYear(), date.getUTCMonth());
  if (+start > now) start = boundary(date.getUTCFullYear(), date.getUTCMonth() - 1);
  const end = boundary(start.getUTCFullYear(), start.getUTCMonth() + 1);
  return { start: start.toISOString().slice(0, 10), end: +end };
}

export class RelayBudget {
  constructor(ctx, env) { this.ctx = ctx; this.env = env; }
  fetch(request) { return this.ctx.blockConcurrencyWhile(() => this.check(request && new URL(request.url).pathname === "/monitor")); }
  async check(monitor = false) {
    const unavailable = { allowed: false, reason: "unavailable" };
    try {
      const env = this.env, now = Date.now();
      if (env.TURN_ENABLED !== "true" && env.SFU_ENABLED !== "true") return json({ allowed: false, reason: "disabled" });
      const limit = Number(env.TURN_BUDGET_BYTES);
      // Require explicit configuration; never silently guess the billing cycle.
      if (!/^[a-f0-9]{32}$/i.test(env.CF_ACCOUNT_ID || "") || !env.CF_ANALYTICS_TOKEN ||
          !Number.isSafeInteger(limit) || limit <= 0 || limit > 900000000000) return json(unavailable);
      const period = billingPeriod(env.TURN_BILLING_DAY, now);
      const identity = `${env.CF_ACCOUNT_ID}:${period.start}:${limit}`;
      let state = await this.ctx.storage.get("budget");
      if (!state || state.identity !== identity) state = { identity, peak: 0, grants: [] };
      if (state.peak >= limit) return json({ allowed: false, reason: "budget", relayRemainingBytes: "0" });
      if (!(state.checkedAt <= now && now - state.checkedAt < 60000)) {
        // Account-wide TURN plus SFU: both consume the shared free allowance.
        // No dimensions means one total, rather than a truncated list of users/keys.
        const query = `query($account: string, $from: Date, $to: Date) {
          viewer { accounts(filter: { accountTag: $account }) {
            turn: callsTurnUsageAdaptiveGroups(limit: 1, filter: { date_geq: $from, date_leq: $to }) { sum { egressBytes } }
            sfu: callsUsageAdaptiveGroups(limit: 1, filter: { date_geq: $from, date_leq: $to }) { sum { egressBytes } }
          } }
        }`;
        const response = await fetch("https://api.cloudflare.com/client/v4/graphql", {
          method: "POST", headers: { Authorization: `Bearer ${env.CF_ANALYTICS_TOKEN}`, "Content-Type": "application/json" },
          body: JSON.stringify({ query, variables: { account: env.CF_ACCOUNT_ID, from: period.start, to: new Date(now).toISOString().slice(0, 10) } }),
          signal: AbortSignal.timeout(8000)
        });
        if (!response.ok) return json(unavailable);
        const result = await response.json(), accounts = result.data?.viewer?.accounts;
        if ((result.errors && (!Array.isArray(result.errors) || result.errors.length)) || !Array.isArray(accounts) || accounts.length !== 1) return json(unavailable);
        let used = 0;
        for (const name of ["turn", "sfu"]) {
          const groups = accounts[0][name];
          if (!Array.isArray(groups) || groups.length > 1) return json(unavailable);
          for (const group of groups) {
            const bytes = group?.sum?.egressBytes;
            if (!Number.isSafeInteger(bytes) || bytes < 0) return json(unavailable);
            used += bytes;
          }
        }
        if (!Number.isSafeInteger(used)) return json(unavailable);
        state.peak = Math.max(state.peak, used); state.checkedAt = now;
      }
      // Global issuance cap, in addition to the per-network request limit.
      // Temporary credentials can be extracted from any public client.
      state.grants = state.grants.filter(time => time > now - 3600000);
      const reason = state.peak >= limit ? "budget" : !monitor && state.grants.length >= 120 ? "busy" : "";
      if (!reason && !monitor) state.grants.push(now);
      await this.ctx.storage.put("budget", state);
      await this.ctx.storage.setAlarm(period.end);
      return json({ allowed: !reason, reason, relayRemainingBytes: String(Math.max(0, limit - state.peak)) });
    } catch { return json(unavailable); }
  }
  async alarm() { await this.ctx.storage.deleteAll(); }
}
async function body(request) {
  if (Number(request.headers.get("Content-Length")) > MAX_BODY) throw new Error("Request too large.");
  const reader = request.body?.getReader();
  if (!reader) throw new Error("Missing request.");
  let size = 0, chunks = [];
  for (;;) {
    const { value, done } = await reader.read(); if (done) break;
    size += value.length; if (size > MAX_BODY) { await reader.cancel(); throw new Error("Request too large."); }
    chunks.push(value);
  }
  const bytes = new Uint8Array(size); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
  return JSON.parse(new TextDecoder().decode(bytes));
}
function validPlayer(data) {
  return typeof data.name === "string" && data.name.trim().length > 0 && data.name.length <= 32 && !/[\u0000-\u001f\u007f]/.test(data.name) && /^[A-F0-9]{64}$/.test(data.fingerprint);
}
export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);
      if (request.method === "GET" && url.pathname === "/health") return json({ service: "OpenZ Duel", version: 1, skirmishProtocol: 2 });
      const invitation = url.pathname.slice(1);
      if (request.method === "GET" && validRoom(invitation)) {
        return new Response(`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Join an OpenZ game</title><style>body{background:#171d20;color:#edf0df;font:20px system-ui;max-width:620px;margin:12vh auto;padding:24px}a{display:block;background:#d8a33c;color:#171d20;padding:18px;text-align:center;border-radius:6px;text-decoration:none;font-weight:bold}code{overflow-wrap:anywhere}p{line-height:1.6}</style><h1>Your OpenZ invitation</h1><p>Open the game to join the lobby.</p><a href="openz://join?room=${invitation}">Join in OpenZ</a><p>Already in the game? Paste this code in <strong>Multiplayer</strong>.</p><p><code>${invitation}</code></p><p>The invitation opens only with OpenZ installed and invitation links enabled.</p></html>`, { headers: { "Content-Type": "text/html;charset=UTF-8", "Cache-Control": "no-store", "Content-Security-Policy": "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'", "Referrer-Policy": "no-referrer", "X-Content-Type-Options": "nosniff" } });
      }
      const create = url.pathname === "/rooms" && request.method === "POST";
      const match = url.pathname.match(/^\/rooms\/(OPNZ-[A-F0-9]{24})\/(join|socket|ice|sfu)$/);
      if (!create && !match) return json({ error: "Unknown invitation or endpoint." }, 404);
      // Bound room creation, failed joins and socket handshakes by network origin.
      const ip = request.headers.get("CF-Connecting-IP") || "local";
      const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(ip));
      const key = Array.from(new Uint8Array(digest), b => b.toString(16).padStart(2, "0")).join("");
      const limit = await env.LIMITS.get(env.LIMITS.idFromName(key)).fetch(new Request("https://internal/check", { method: "POST" }));
      if (!limit.ok) return json({ error: "Too many attempts. Please try again later." }, 429);
      const code = create ? "OPNZ-" + Array.from(crypto.getRandomValues(new Uint8Array(12)), b => b.toString(16).padStart(2, "0")).join("").toUpperCase() : match[1];
      const endpoint = create ? "create" : match[2];
      if ((endpoint === "socket" && request.method !== "GET") || (endpoint !== "socket" && request.method !== "POST")) return json({ error: "Method not allowed." }, 405);
      const headers = new Headers(request.headers); headers.set("X-Room-Code", code);
      headers.set("X-Room-Network", key);
      headers.delete("Content-Length"); headers.delete("Transfer-Encoding");
      const forwarded = new Request("https://internal/" + endpoint, { method: request.method, headers, body: request.method === "GET" ? undefined : JSON.stringify(await body(request)) });
      return await env.ROOMS.get(env.ROOMS.idFromName(code)).fetch(forwarded);
    } catch { return json({ error: "Invalid request. Please try again." }, 400); }
  }
};

export class RequestLimit {
  constructor(ctx) { this.ctx = ctx; }
  async fetch() {
    const now = Date.now(), window = Math.floor(now / 3600000);
    const entry = await this.ctx.storage.get("limit") || { window, count: 0 };
    if (entry.window !== window) { entry.window = window; entry.count = 0; }
    if (++entry.count > 120) return new Response(null, { status: 429 });
    await this.ctx.storage.put("limit", entry); await this.ctx.storage.setAlarm(now + 3600000);
    return new Response("OK");
  }
  async alarm() { await this.ctx.storage.deleteAll(); }
}

export class DuelRoom {
  constructor(ctx, env) { this.ctx = ctx; this.env = env; }
  fetch(request) { return this.ctx.blockConcurrencyWhile(() => this.handle(request)); }
  async handle(request) {
    try {
      const path = new URL(request.url).pathname;
      const data = request.method === "POST" ? await body(request) : null;
      let room = await this.ctx.storage.get("room");
      if (path === "/create") {
        if (room) return json({ error: "Room already exists." }, 409);
        if (!validPlayer(data)) return json({ error: "Invalid player or game version." }, 400);
        if (data.skirmish === true) return new SkirmishRoom(this.ctx, this.env).create(request.headers.get("X-Room-Code"), data);
        room = { code: request.headers.get("X-Room-Code"), secret: randomToken(), fingerprint: data.fingerprint, expires: Date.now() + ROOM_LIFETIME,
          host: { seat: randomToken() }, guest: null };
        await this.ctx.storage.put("room", room); await this.ctx.storage.setAlarm(room.expires);
        return json({ room: room.code, secret: room.secret, seat: room.host.seat, role: "host" });
      }
      if (room?.skirmish) return await new SkirmishRoom(this.ctx, this.env).handle(room, path, data, request, validPlayer);
      if (!room || Date.now() >= room.expires) return json({ error: "This invitation has expired. Ask your opponent for a new one." }, 404);
      if (path === "/join") {
        if (room.guest) return json({ error: "This room is already full. Ask for a new invitation." }, 409);
        if (!validPlayer(data) || data.fingerprint !== room.fingerprint) return json({ error: "Both players must use the same game version." }, 409);
        room.guest = { seat: randomToken() };
        await this.ctx.storage.put("room", room);
        return json({ room: room.code, secret: room.secret, seat: room.guest.seat, role: "guest" });
      }
      const seat = request.headers.get("Authorization")?.match(/^Bearer ([A-Za-z0-9_-]{32})$/)?.[1];
      if (!seat) return json({ error: "Invalid room access." }, 403);
      const role = seat === room.host.seat ? "host" : seat === room.guest?.seat ? "guest" : null;
      if (!role) return json({ error: "Invalid room access." }, 403);
      if (path === "/ice") {
        if (room.guest && !room.reconnectEnabled) {
          room.reconnectEnabled = true;
          room.expires = Date.now() + CONNECTED_ROOM_LIFETIME;
          await this.ctx.storage.put("room", room);
          await this.ctx.storage.setAlarm(room.expires);
        }
        if (!room.guest) return json({ iceServers: STUN, relay: false, relayReason: "waiting" });
        return await peerIce(this.env, this.ctx.storage, room, room[role]);
      }
      if (path !== "/socket" || request.headers.get("Upgrade")?.toLowerCase() !== "websocket") return json({ error: "Expected a connection upgrade." }, 400);
      if (this.ctx.getWebSockets(role).length) return json({ error: "This player is already connected." }, 409);
      const pair = new WebSocketPair(), client = pair[0], server = pair[1];
      this.ctx.acceptWebSocket(server, [role]); server.serializeAttachment({ role, count: 0 });
      if (this.ctx.getWebSockets("host").length && this.ctx.getWebSockets("guest").length)
        for (const socket of this.ctx.getWebSockets()) socket.send(JSON.stringify({ type: "ready" }));
      return new Response(null, { status: 101, webSocket: client });
    } catch { return json({ error: "Invalid room request." }, 400); }
  }
  async webSocketMessage(socket, message) {
    if (socket.deserializeAttachment()?.skirmish) return this.ctx.blockConcurrencyWhile(() => new SkirmishRoom(this.ctx, this.env).message(socket, message));
    const state = socket.deserializeAttachment();
    if (typeof message !== "string" || message.length > 62000 || ++state.count > 6) { socket.close(1008, "Invalid signaling message."); return; }
    socket.serializeAttachment(state);
    try {
      const data = JSON.parse(message);
      if ((state.role === "host" && data.type !== "offer") || (state.role === "guest" && data.type !== "answer") || typeof data.sdp !== "string" || data.sdp.length > 60000) throw new Error();
      for (const peer of this.ctx.getWebSockets(state.role === "host" ? "guest" : "host")) peer.send(JSON.stringify({ type: data.type, sdp: data.sdp }));
    } catch { socket.close(1008, "Invalid connection offer."); }
  }
  async webSocketClose(socket) {
    if (socket.deserializeAttachment()?.skirmish) return this.ctx.blockConcurrencyWhile(() => new SkirmishRoom(this.ctx, this.env).left(socket));
    const state = socket.deserializeAttachment();
    for (const peer of this.ctx.getWebSockets(state.role === "host" ? "guest" : "host")) {
      try { peer.send(JSON.stringify({ type: "left" })); } catch { /* The other player may already have left. */ }
    }
    try { socket.close(1000, "Closed"); } catch { /* Already closed. */ }
  }
  async webSocketError(socket) { await this.webSocketClose(socket); }
  alarm() {
    return this.ctx.blockConcurrencyWhile(async () => {
      const room = await this.ctx.storage.get("room");
      if (room?.skirmish) return new SkirmishRoom(this.ctx, this.env).alarm(room);
      for (const socket of this.ctx.getWebSockets()) socket.close(1000, "Invitation expired.");
      await this.ctx.storage.deleteAll();
    });
  }
}
