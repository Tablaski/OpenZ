// An explicit live smoke test. Creates one short-lived disposable room, never requests TURN access.
import WebSocket from "ws";
import assert from "node:assert/strict";
const service = process.env.OPENZ_DUEL_SERVICE_URL;
if (!service) throw new Error("Set OPENZ_DUEL_SERVICE_URL to the service to validate.");
const fingerprint = "A".repeat(64);
async function post(path, data, seat) {
  const response = await fetch(service + path, { method: "POST", headers: { "Content-Type": "application/json", ...(seat ? { Authorization: "Bearer " + seat } : {}) }, body: JSON.stringify(data) });
  assert.equal(response.status, 200, `Unexpected HTTP status ${response.status} at ${path.replace(/OPNZ-[A-F0-9]+/, "ROOM")}`);
  return response.json();
}
function messages(socket) {
  const queue = [], waiting = [];
  socket.on("message", bytes => { const value = JSON.parse(bytes); if (waiting.length) waiting.shift()(value); else queue.push(value); });
  return () => queue.length ? Promise.resolve(queue.shift()) : Promise.race([
    new Promise(resolve => waiting.push(resolve)),
    new Promise((_, reject) => setTimeout(() => reject(new Error("Signaling timeout.")), 10000).unref())
  ]);
}
const host = await post("/rooms", { name: "Network check host", fingerprint });
const noSeat = await fetch(service + `/rooms/${host.room}/ice`, { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" });
assert.equal(noSeat.status, 403); await noSeat.arrayBuffer();
const url = service.replace(/^http/, "ws") + "/rooms/" + host.room + "/socket";
const a = new WebSocket(url, { headers: { Authorization: "Bearer " + host.seat } });
const nextA = messages(a);
await new Promise((resolve, reject) => { a.once("open", resolve); a.once("error", reject); });
let b;
try {
  const guest = await post(`/rooms/${host.room}/join`, { name: "Network check guest", fingerprint });
  b = new WebSocket(url, { headers: { Authorization: "Bearer " + guest.seat } });
  const nextB = messages(b);
  await new Promise((resolve, reject) => { b.once("open", resolve); b.once("error", reject); });
  assert.equal((await nextA()).type, "ready"); assert.equal((await nextB()).type, "ready");
  a.send(JSON.stringify({ type: "offer", sdp: "network-check-offer" })); assert.equal((await nextB()).sdp, "network-check-offer");
  b.send(JSON.stringify({ type: "answer", sdp: "network-check-answer" })); assert.equal((await nextA()).sdp, "network-check-answer");
  for (const seat of [host.seat, guest.seat]) {
    const ice = await post(`/rooms/${host.room}/ice`, {}, seat);
    assert.equal(ice.relay, false); assert.equal(ice.iceServers.length, 1);
  }
  const invitation = await fetch(service + "/" + host.room); assert.equal(invitation.status, 200);
  assert.match(await invitation.text(), /Join in OpenZ/);
  console.log("PASS: live HTTPS invitations, authenticated WebSockets, offer/answer exchange, both ICE requests and public relay denial.");
} finally { a.close(); b?.close(); }
