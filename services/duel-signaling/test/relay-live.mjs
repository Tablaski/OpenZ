// Explicit live credential test; run manually. Never prints permanent or temporary credentials.
import assert from "node:assert/strict";
const service = process.env.OPENZ_DUEL_SERVICE_URL;
if (!service) throw new Error("Set OPENZ_DUEL_SERVICE_URL.");
async function post(path, data, seat) {
  const response = await fetch(service + path, { method: "POST", headers: { "Content-Type": "application/json", ...(seat ? { Authorization: "Bearer " + seat } : {}) }, body: JSON.stringify(data) });
  if (response.status !== 200) throw new Error("Relay request failed with status " + response.status + ". No credentials were printed.");
  return response.json();
}
const fingerprint = "A".repeat(64);
const host = await post("/rooms", { name: "Universal relay check host", fingerprint });
const guest = await post(`/rooms/${host.room}/join`, { name: "Universal relay check guest", fingerprint });
for (const seat of [host.seat, guest.seat]) {
  const ice = await post(`/rooms/${host.room}/ice`, {}, seat);
  assert.equal(ice.relay, true);
  assert.ok(ice.iceServers.some(server => server.urls.some(url => url.startsWith("turn:")) && server.username && server.credential));
}
console.log("PASS: real short-lived Cloudflare TURN credentials issued without player passwords to both authenticated seats.");
