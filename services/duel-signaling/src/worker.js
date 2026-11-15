const STUN = [{ urls: ["stun:stun.cloudflare.com:3478"], username: "", credential: "" }];
const ROOM_LIFETIME = 30 * 60 * 1000;
const MAX_BODY = 65536;
export function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json", "Cache-Control": "no-store" } });
}
export function randomToken(bytes = 24) {
  return btoa(String.fromCharCode(...crypto.getRandomValues(new Uint8Array(bytes)))).replaceAll("+", "-").replaceAll("/", "_").replaceAll("=", "");
}
export function validRoom(room) { return /^OPNZ-[A-F0-9]{24}$/.test(room); }
export async function relayAllowed(token, hashes) {
  if (typeof token !== "string" || token.length < 32 || token.length > 256) return false;
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(token));
  const actual = Array.from(new Uint8Array(digest), b => b.toString(16).padStart(2, "0")).join("");
  return (hashes || "").split(",").some(expected => {
    expected = expected.trim().toLowerCase();
    if (expected.length !== actual.length) return false;
    let difference = 0;
    for (let i = 0; i < actual.length; i++) difference |= expected.charCodeAt(i) ^ actual.charCodeAt(i);
    return difference === 0;
  });
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
      if (request.method === "GET" && url.pathname === "/health") return json({ service: "OpenZ Duel", version: 1 });
      const invitation = url.pathname.slice(1);
      if (request.method === "GET" && validRoom(invitation)) {
        return new Response(`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Join an OpenZ duel</title><style>body{background:#171d20;color:#edf0df;font:20px system-ui;max-width:620px;margin:12vh auto;padding:24px}a{display:block;background:#d8a33c;color:#171d20;padding:18px;text-align:center;border-radius:6px;text-decoration:none;font-weight:bold}code{overflow-wrap:anywhere}p{line-height:1.6}</style><h1>Your OpenZ invitation</h1><p>Your friend is waiting. Open the game to join.</p><a href="openz://join?room=${invitation}">Join in OpenZ</a><p>Already in the game? Paste this code in <strong>1VS1 → Play Online</strong>:</p><p><code>${invitation}</code></p><p>The invitation opens only with OpenZ installed and invitation links enabled.</p></html>`, { headers: { "Content-Type": "text/html;charset=UTF-8", "Cache-Control": "no-store", "Content-Security-Policy": "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'", "Referrer-Policy": "no-referrer", "X-Content-Type-Options": "nosniff" } });
      }
      const create = url.pathname === "/rooms" && request.method === "POST";
      const match = url.pathname.match(/^\/rooms\/(OPNZ-[A-F0-9]{24})\/(join|socket|ice)$/);
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
        room = { code: request.headers.get("X-Room-Code"), secret: randomToken(), fingerprint: data.fingerprint, expires: Date.now() + ROOM_LIFETIME,
          host: { seat: randomToken(), relay: await relayAllowed(data.relayToken, this.env.PRIVATE_RELAY_HASHES) }, guest: null };
        await this.ctx.storage.put("room", room); await this.ctx.storage.setAlarm(room.expires);
        return json({ room: room.code, secret: room.secret, seat: room.host.seat, role: "host" });
      }
      if (!room || Date.now() >= room.expires) return json({ error: "This invitation has expired. Ask your opponent for a new one." }, 404);
      if (path === "/join") {
        if (room.guest) return json({ error: "This room is already full. Ask for a new invitation." }, 409);
        if (!validPlayer(data) || data.fingerprint !== room.fingerprint) return json({ error: "Both players must use the same game version." }, 409);
        room.guest = { seat: randomToken(), relay: await relayAllowed(data.relayToken, this.env.PRIVATE_RELAY_HASHES) };
        await this.ctx.storage.put("room", room);
        return json({ room: room.code, secret: room.secret, seat: room.guest.seat, role: "guest" });
      }
      const seat = request.headers.get("Authorization")?.match(/^Bearer ([A-Za-z0-9_-]{32})$/)?.[1];
      if (!seat) return json({ error: "Invalid room access." }, 403);
      const role = seat === room.host.seat ? "host" : seat === room.guest?.seat ? "guest" : null;
      if (!role) return json({ error: "Invalid room access." }, 403);
      if (path === "/ice") {
        if (this.env.TURN_ENABLED !== "true" || !room.host.relay || !room.guest?.relay) return json({ iceServers: STUN, relay: false });
        const player = room[role];
        if (!player.ice) {
          if (!this.env.TURN_KEY_ID || !this.env.TURN_API_TOKEN) return json({ error: "Private relay is not configured. Contact the host." }, 503);
          const response = await fetch(`https://rtc.live.cloudflare.com/v1/turn/keys/${this.env.TURN_KEY_ID}/credentials/generate`, {
            method: "POST", headers: { Authorization: `Bearer ${this.env.TURN_API_TOKEN}`, "Content-Type": "application/json" },
            body: JSON.stringify({ ttl: Math.min(21600, Math.max(600, Number(this.env.TURN_TTL) || 21600)) })
          });
          if (!response.ok) return json({ error: "Private relay is temporarily unavailable. Please try again." }, 503);
          const credentials = await response.json();
          // libjuice supports TURN over UDP. Avoid advertising unsupported TCP/TLS paths.
          const source = Array.isArray(credentials.iceServers) ? credentials.iceServers : [credentials.iceServers];
          player.ice = source.filter(Boolean).map(server => ({ urls: (Array.isArray(server.urls) ? server.urls : [server.urls]).filter(url => /^turn:/.test(url) && !url.includes("transport=tcp")), username: server.username, credential: server.credential })).filter(server => server.urls.length);
          if (!player.ice.length) return json({ error: "No compatible relay is available." }, 503);
          await this.ctx.storage.put("room", room);
        }
        return json({ iceServers: [...STUN, ...player.ice], relay: true });
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
    const state = socket.deserializeAttachment();
    for (const peer of this.ctx.getWebSockets(state.role === "host" ? "guest" : "host")) {
      try { peer.send(JSON.stringify({ type: "left" })); } catch { /* The other player may already have left. */ }
    }
    try { socket.close(1000, "Closed"); } catch { /* Already closed. */ }
  }
  async webSocketError(socket) { await this.webSocketClose(socket); }
  async alarm() {
    for (const socket of this.ctx.getWebSockets()) socket.close(1000, "Invitation expired.");
    await this.ctx.storage.deleteAll();
  }
}
