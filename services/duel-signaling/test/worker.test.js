import { test } from "node:test";
import assert from "node:assert/strict";
import { webcrypto } from "node:crypto";
import { relayAllowed, validRoom, DuelRoom } from "../src/worker.js";
globalThis.crypto ??= webcrypto;
const fingerprint = "A".repeat(64);
const token = "private-" + "X".repeat(40);
const hash = Buffer.from(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(token))).toString("hex");
function context() {
  const entries = new Map();
  return { storage: { get: async k => structuredClone(entries.get(k)), put: async (k, v) => entries.set(k, structuredClone(v)), setAlarm: async () => {}, deleteAll: async () => entries.clear() },
    getWebSockets: () => [], blockConcurrencyWhile: fn => fn() };
}
function post(path, data, seat) {
  return new Request("https://internal/" + path, { method: "POST", headers: { "X-Room-Code": "OPNZ-" + "A".repeat(24), ...(seat ? { Authorization: "Bearer " + seat } : {}) }, body: JSON.stringify(data) });
}
test("private relay hashes are explicit and invalid codes fail closed", async () => {
  assert.equal(await relayAllowed(token, hash), true);
  assert.equal(await relayAllowed(token + "bad", hash), false);
  assert.equal(await relayAllowed(token, ""), false);
  assert.equal(await relayAllowed("", hash), false);
  assert.equal(validRoom("OPNZ-1234"), false);
  assert.equal(validRoom("OPNZ-" + "A".repeat(24)), true);
});
test("room capacity, version check, seat auth and private relay isolation", async () => {
  const room = new DuelRoom(context(), { PRIVATE_RELAY_HASHES: hash, TURN_ENABLED: "true" });
  const host = await (await room.fetch(post("create", { name: "Host", fingerprint, relayToken: token }))).json();
  assert.equal(host.secret.length, 32); assert.equal(host.seat.length, 32);
  assert.equal((await room.fetch(post("ice", {}))).status, 403);
  const noSeatSocket = await room.fetch(new Request("https://internal/socket", { headers: { Upgrade: "websocket" } }));
  assert.equal(noSeatSocket.status, 403);
  assert.equal((await room.fetch(post("join", { name: "Guest", fingerprint: "B".repeat(64) }))).status, 409);
  const guest = await (await room.fetch(post("join", { name: "Guest", fingerprint }))).json();
  assert.notEqual(host.seat, guest.seat);
  assert.equal((await room.fetch(post("join", { name: "Intruder", fingerprint }))).status, 409);
  assert.equal((await room.fetch(post("ice", {}, "wrong-seat"))).status, 403);
  for (const seat of [host.seat, guest.seat]) {
    const ice = await (await room.fetch(post("ice", {}, seat))).json();
    assert.equal(ice.relay, false); assert.equal(ice.iceServers.length, 1);
  }
});
test("TURN is disabled by default even with two private users", async () => {
  const room = new DuelRoom(context(), { PRIVATE_RELAY_HASHES: hash, TURN_ENABLED: "false" });
  const host = await (await room.fetch(post("create", { name: "Host", fingerprint, relayToken: token }))).json();
  await room.fetch(post("join", { name: "Guest", fingerprint, relayToken: token }));
  const ice = await (await room.fetch(post("ice", {}, host.seat))).json();
  assert.equal(ice.relay, false);
});
test("authorized credentials are cached, UDP-only and never include API secret", async () => {
  const originalFetch = globalThis.fetch; let calls = 0;
  globalThis.fetch = async () => { calls++; return new Response(JSON.stringify({ iceServers: { urls: ["turn:turn.cloudflare.com:3478?transport=udp", "turn:turn.cloudflare.com:3478?transport=tcp", "turns:turn.cloudflare.com:5349"], username: "short-lived-user", credential: "temporary-password" } })); };
  try {
    const room = new DuelRoom(context(), { PRIVATE_RELAY_HASHES: hash, TURN_ENABLED: "true", TURN_KEY_ID: "key", TURN_API_TOKEN: "NEVER-SHARE-THIS" });
    const host = await (await room.fetch(post("create", { name: "Host", fingerprint, relayToken: token }))).json();
    await room.fetch(post("join", { name: "Guest", fingerprint, relayToken: token }));
    const response = await (await room.fetch(post("ice", {}, host.seat))).json();
    assert.equal(response.relay, true); assert.equal(response.iceServers[1].urls.length, 1);
    assert.equal(JSON.stringify(response).includes("NEVER-SHARE-THIS"), false);
    await room.fetch(post("ice", {}, host.seat)); assert.equal(calls, 1);
  } finally { globalThis.fetch = originalFetch; }
});
