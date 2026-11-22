// Run explicitly: node test/integration.mjs. Uses the real local Workers runtime.
import { Miniflare, createFetchMock } from "miniflare";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
const fetchMock = createFetchMock(); fetchMock.disableNetConnect();
fetchMock.get("https://api.cloudflare.com").intercept({ path: "/client/v4/graphql", method: "POST" })
  .reply(200, { data: { viewer: { accounts: [{ turn: [{ sum: { egressBytes: 899000000000 } }], sfu: [{ sum: { egressBytes: 1000000000 } }] }] } } });
const mf = new Miniflare({ modules: true, scriptPath: fileURLToPath(new URL("../src/worker.js", import.meta.url)), fetchMock,
  modulesRules: [{ type: "ESModule", include: ["**/*.js"] }],
  compatibilityDate: "2025-11-01", durableObjects: { ROOMS: { className: "DuelRoom", useSQLite: true }, LIMITS: { className: "RequestLimit", useSQLite: true }, RELAY_BUDGET: { className: "RelayBudget", useSQLite: true } },
  bindings: { SFU_ENABLED: "true", SFU_APP_ID: "mock-sfu", SFU_APP_SECRET: "mock-sfu-secret", TURN_ENABLED: "true", CF_ACCOUNT_ID: "a".repeat(32), CF_ANALYTICS_TOKEN: "mock-analytics-token", TURN_BILLING_DAY: "1", TURN_BUDGET_BYTES: "900000000000", TURN_KEY_ID: "mock-key", TURN_API_TOKEN: "mock-generation-token" } });
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
  const ice = await (await post(`/rooms/${host.room}/ice`, {}, guest.seat)).json(); assert.equal(ice.relay, false); assert.equal(ice.relayReason, "budget");
  const hostIce = await (await post(`/rooms/${host.room}/ice`, {}, host.seat)).json(); assert.equal(hostIce.relayReason, "budget");
  fetchMock.assertNoPendingInterceptors();
  a.close(); b.close();
  console.log("PASS: real Workers runtime, SQLite room state, authenticated WebSockets, bidirectional signaling and shared TURN/SFU budget cutoff without contacting a real relay.");

  const skirmish = await (await post("/rooms", { name: "Host", fingerprint, skirmish: true })).json();
  const roomPath = `/rooms/${skirmish.room}`;
  async function connect(member) {
    const response = await mf.dispatchFetch(`https://local${roomPath}/socket`, { headers: { Upgrade: "websocket", Authorization: "Bearer " + member.seat } });
    const socket = response.webSocket;
    const firstRoster = nextMessage(socket);
    socket.accept();
    return { socket, roster: await firstRoster };
  }
  const creator = await connect(skirmish);
  assert.equal(creator.roster.players.length, 1);
  const guests = [];
  for (let id = 2; id <= 8; id++) {
    const hostRoster = nextMessage(creator.socket);
    const member = await (await post(roomPath + "/join", { name: `Guest ${id}`, fingerprint, skirmishProtocol: 1 })).json();
    const guest = await connect(member);
    guests.push(guest.socket);
    assert.equal(guest.roster.players.length, id);
    assert.equal(guest.roster.sfu, id > 2);
    const roster = await hostRoster;
    assert.equal(roster.players.length, id);
    assert.equal(roster.epoch, id > 2 ? 2 : 1);
    assert.equal(roster.sfu, guest.roster.sfu);
  }
  const iceDuringSfu = await post(roomPath + "/ice", { epoch: 2 }, skirmish.seat);
  assert.equal(iceDuringSfu.status, 409);
  for (let remaining = 7; remaining >= 2; remaining--) {
    const departed = nextMessage(creator.socket);
    guests.pop().close();
    const roster = await departed;
    assert.equal(roster.players.length, remaining);
    assert.equal(roster.sfu, remaining > 2);
    assert.equal(roster.epoch, remaining > 2 ? 2 : 3);
  }
  const staleIce = await post(roomPath + "/ice", { epoch: 1 }, skirmish.seat);
  assert.equal(staleIce.status, 409);
  const currentIce = await post(roomPath + "/ice", { epoch: 3 }, skirmish.seat);
  assert.equal((await currentIce.json()).relayReason, "budget");
  const directOffer = nextMessage(guests[0]);
  creator.socket.send(JSON.stringify({ type: "offer", epoch: 3, id: 2, sdp: "two-player-offer" }));
  assert.deepEqual(await directOffer, { type: "offer", id: 1, epoch: 3, sdp: "two-player-offer" });
  const directAnswer = nextMessage(creator.socket);
  guests[0].send(JSON.stringify({ type: "answer", epoch: 3, id: 1, sdp: "two-player-answer" }));
  assert.deepEqual(await directAnswer, { type: "answer", id: 2, epoch: 3, sdp: "two-player-answer" });

  const backToSfu = nextMessage(creator.socket);
  const returning = await (await post(roomPath + "/join", { name: "Returning", fingerprint, skirmishProtocol: 1 })).json();
  const returned = await connect(returning);
  assert.equal(returned.roster.sfu, true);
  assert.equal(returned.roster.epoch, 4);
  assert.equal((await backToSfu).players.length, 3);
  creator.socket.close();
  console.log("PASS: real Workers runtime, one invitation for eight humans, SFU transitions from two to three and back, and bidirectional direct signaling after a departure.");
} finally { await mf.dispose(); }
