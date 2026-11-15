// Explicit owner-only test. Never prints permanent or temporary credentials.
import { readFile } from "node:fs/promises";
import assert from "node:assert/strict";
const service = process.env.OPENZ_DUEL_SERVICE_URL;
if (!service) throw new Error("Set OPENZ_DUEL_SERVICE_URL.");
const owner = (await readFile(new URL("../../../.local/duel-private/owner-access.txt", import.meta.url), "utf8")).trim();
const brother = (await readFile(new URL("../../../.local/duel-private/brother-access.txt", import.meta.url), "utf8")).trim();
async function post(path, data, seat) {
  const response = await fetch(service + path, { method: "POST", headers: { "Content-Type": "application/json", ...(seat ? { Authorization: "Bearer " + seat } : {}) }, body: JSON.stringify(data) });
  if (response.status !== 200) throw new Error("Private relay request failed with status " + response.status + ". No credentials were printed.");
  return response.json();
}
const fingerprint = "A".repeat(64);
const host = await post("/rooms", { name: "Private relay check host", fingerprint, relayToken: owner });
const guest = await post(`/rooms/${host.room}/join`, { name: "Private relay check guest", fingerprint, relayToken: brother });
for (const seat of [host.seat, guest.seat]) {
  const ice = await post(`/rooms/${host.room}/ice`, {}, seat);
  assert.equal(ice.relay, true);
  assert.ok(ice.iceServers.some(server => server.urls.some(url => url.startsWith("turn:")) && server.username && server.credential));
}
console.log("PASS: both distinct private codes accepted; real short-lived Cloudflare TURN credentials issued to both authorized seats.");
