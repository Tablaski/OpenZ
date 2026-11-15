// Run explicitly: node test/integration.mjs. Uses the real local Workers runtime.
import { Miniflare } from "miniflare";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
const mf = new Miniflare({ modules: true, scriptPath: fileURLToPath(new URL("../src/worker.js", import.meta.url)),
  compatibilityDate: "2025-11-01", durableObjects: { ROOMS: { className: "DuelRoom", useSQLite: true }, LIMITS: { className: "RequestLimit", useSQLite: true } },
  bindings: { TURN_ENABLED: "false" } });
const fingerprint = "A".repeat(64);
async function post(path, data, seat) {
  return mf.dispatchFetch("https://local" + path, { method: "POST", headers: { "Content-Type": "application/json", ...(seat ? { Authorization: "Bearer " + seat } : {}) }, body: JSON.stringify(data) });
}
function nextMessage(ws) { return new Promise((resolve, reject) => { ws.addEventListener("message", event => resolve(JSON.parse(event.data)), { once: true }); setTimeout(() => reject(new Error("WebSocket signaling timed out.")), 5000).unref(); }); }
try {
  const host = await (await post("/rooms", { name: "Host", fingerprint })).json();
  assert.match(host.room, /^OPNZ-[A-F0-9]{24}$/);
  const hostResponse = await mf.dispatchFetch(`https://local/rooms/${host.room}/socket`, { headers: { Upgrade: "websocket", Authorization: "Bearer " + host.seat } });
  const a = hostResponse.webSocket; a.accept();
  const hostReady = nextMessage(a);
  const guest = await (await post(`/rooms/${host.room}/join`, { name: "Guest", fingerprint })).json();
  const guestResponse = await mf.dispatchFetch(`https://local/rooms/${host.room}/socket`, { headers: { Upgrade: "websocket", Authorization: "Bearer " + guest.seat } });
  const b = guestResponse.webSocket; const guestReady = nextMessage(b); b.accept();
  assert.equal((await hostReady).type, "ready"); assert.equal((await guestReady).type, "ready");
  const offer = nextMessage(b); a.send(JSON.stringify({ type: "offer", sdp: "test-offer" })); assert.equal((await offer).sdp, "test-offer");
  const answer = nextMessage(a); b.send(JSON.stringify({ type: "answer", sdp: "test-answer" })); assert.equal((await answer).sdp, "test-answer");
  const ice = await (await post(`/rooms/${host.room}/ice`, {}, guest.seat)).json(); assert.equal(ice.relay, false);
  a.close(); b.close();
  console.log("PASS: real Workers runtime, SQLite room state, authenticated WebSockets, bidirectional signaling and public STUN-only access.");
} finally { await mf.dispose(); }
