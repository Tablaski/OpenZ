import { test } from "node:test";
import assert from "node:assert/strict";
import { webcrypto } from "node:crypto";
import { validRoom, billingPeriod, RelayBudget, DuelRoom } from "../src/worker.js";
globalThis.crypto ??= webcrypto;
const fingerprint = "A".repeat(64);
const config = { TURN_ENABLED: "true", CF_ACCOUNT_ID: "a".repeat(32), CF_ANALYTICS_TOKEN: "SERVER-ANALYTICS-SECRET", TURN_BILLING_DAY: "15", TURN_BUDGET_BYTES: "900000000000" };
function context() {
  const entries = new Map();
  return { storage: { get: async k => structuredClone(entries.get(k)), put: async (k, v) => entries.set(k, structuredClone(v)), setAlarm: async () => {}, deleteAll: async () => entries.clear() },
    getWebSockets: () => [], blockConcurrencyWhile: fn => fn() };
}
function post(path, data = {}, seat) {
  return new Request("https://internal/" + path, { method: "POST", headers: { "X-Room-Code": "OPNZ-" + "A".repeat(24), ...(seat ? { Authorization: "Bearer " + seat } : {}) }, body: JSON.stringify(data) });
}
const usage = (turn = 0, sfu = 0) => new Response(JSON.stringify({ data: { viewer: { accounts: [{ turn: [{ sum: { egressBytes: turn } }], sfu: [{ sum: { egressBytes: sfu } }] }] } } }));
async function withFetch(fn, run) { const old = globalThis.fetch; globalThis.fetch = fn; try { await run(); } finally { globalThis.fetch = old; } }
async function roomWithBudget(decision = { allowed: true }) {
  const env = { TURN_ENABLED: "true", TURN_KEY_ID: "key", TURN_API_TOKEN: "SERVER-TURN-SECRET", RELAY_BUDGET: { idFromName: name => { assert.equal(name, "account"); return name; }, get: () => ({ fetch: async () => new Response(JSON.stringify(decision)) }) } };
  const room = new DuelRoom(context(), env);
  const host = await (await room.fetch(post("create", { name: "Host", fingerprint }))).json();
  const guest = await (await room.fetch(post("join", { name: "Guest", fingerprint }))).json();
  return { room, env, host, guest };
}

test("one Skirmish invitation admits eight authenticated humans and routes direct SDP to one peer", async () => {
  const ctx = context(), sockets = new Map();
  ctx.getWebSockets = tag => tag === "skirmish" ? [...sockets.values()] : [sockets.get(Number(tag.replace("skirmish-", "")))].filter(Boolean);
  const room = new DuelRoom(ctx, {});
  const host = await (await room.fetch(post("create", { name: "Host", fingerprint, skirmish: true }))).json();
  function attach(id) {
    let state = { skirmish: true, id };
    const messages = [];
    const socket = { messages, deserializeAttachment: () => state, serializeAttachment: value => { state = value; },
      send: message => messages.push(JSON.parse(message)), close: () => sockets.delete(id) };
    sockets.set(id, socket);
    return socket;
  }
  const hostSocket = attach(host.id);
  assert.equal((await room.fetch(post("join", { name: "Old guest", fingerprint }))).status, 409);
  for (let id = 2; id <= 8; id++) {
    const guest = await (await room.fetch(post("join", { name: `Guest ${id}`, fingerprint, skirmishProtocol: 1 }))).json();
    assert.equal(guest.room, host.room);
    assert.equal(guest.id, id);
    attach(id);
  }
  assert.equal((await room.fetch(post("join", { name: "Ninth player", fingerprint, skirmishProtocol: 1 }))).status, 409);
  await room.webSocketMessage(hostSocket, JSON.stringify({ type: "offer", id: 8, epoch: 1, sdp: "offer" }));
  assert.deepEqual(sockets.get(8).messages, [{ type: "offer", id: 1, epoch: 1, sdp: "offer" }]);
  assert.deepEqual(sockets.get(2).messages, []);
  await room.webSocketMessage(sockets.get(8), JSON.stringify({ type: "answer", id: 1, epoch: 1, sdp: "answer" }));
  assert.deepEqual(hostSocket.messages, [{ type: "answer", id: 8, epoch: 1, sdp: "answer" }]);
});

test("an active SFU room closes its publications and signals direct fallback when its shared budget closes", async () => {
  const ctx = context(), signals = [], calls = [];
  const socket = { send: text => signals.push(JSON.parse(text)) };
  ctx.getWebSockets = () => [socket];
  let allowed = true;
  const room = new DuelRoom(ctx, { SFU_ENABLED: "true", SFU_APP_ID: "app", SFU_APP_SECRET: "server-secret",
    RELAY_BUDGET: { idFromName: key => key, get: () => ({ fetch: async () => new Response(JSON.stringify({ allowed })) }) } });
  const host = await (await room.fetch(post("create", { name: "Host", fingerprint, skirmish: true }))).json();
  await withFetch(async (url, request) => {
    assert.equal(request.headers.Authorization, "Bearer server-secret");
    calls.push({ url, method: request.method, body: request.body && JSON.parse(request.body) });
    if (url.endsWith("/sessions/new")) return Response.json({ sessionId: "host-session" });
    if (url.endsWith("/establish")) return Response.json({ dataChannel: { id: 0 }, sessionDescription: { type: "offer", sdp: "test offer" } });
    if (url.endsWith("/datachannels/new")) return Response.json({ dataChannels: [
      { dataChannelName: "openz-game", id: 2 }, { dataChannelName: "openz-voice", id: 4 }
    ] });
    return Response.json({});
  }, async () => {
    const api = (type, options = {}, seat = host.seat) => room.fetch(post("sfu", { type, epoch: 1, ...options }, seat));
    assert.equal((await api("setup", {}, "wrong-seat")).status, 403);
    assert.deepEqual(await (await api("setup")).json(), { sdp: "test offer", game: 0 });
    assert.equal((await api("answer", { sdp: "test answer" })).status, 200);
    assert.deepEqual(await (await api("publish")).json(), { game: 2, voice: 4 });
    allowed = false;
    await room.alarm();
    assert.equal(signals.at(-1).sfu, false);
    assert.equal(signals.at(-1).epoch, 2);
    assert.deepEqual(calls.at(-1).body, { dataChannels: [{ id: 2 }, { id: 4 }] });
    assert.equal((await api("setup")).status, 409);
  });
});

test("only the host can ban; the private note and reconnect restriction stay in the current room", async () => {
  const ctx = context(), sockets = new Map();
  ctx.getWebSockets = tag => tag === "skirmish" ? [...sockets.values()] : [sockets.get(Number(tag.replace("skirmish-", "")))].filter(Boolean);
  const room = new DuelRoom(ctx, {});
  const host = await (await room.fetch(post("create", { name: "Host", fingerprint, skirmish: true, skirmishProtocol: 2 }))).json();
  function attach(id) {
    const messages = [];
    const socket = { messages, deserializeAttachment: () => ({ skirmish: true, id }), send: message => messages.push(JSON.parse(message)), close: () => sockets.delete(id) };
    sockets.set(id, socket);
    return socket;
  }
  const creator = attach(host.id);
  function join(network) {
    const request = post("join", { name: "Guest", fingerprint, skirmishProtocol: 2 });
    request.headers.set("X-Room-Network", network);
    return room.fetch(request);
  }
  const guest = await (await join("guest-network")).json();
  const target = attach(guest.id);
  const action = JSON.stringify({ type: "moderate", id: guest.id, ban: true, note: "Private note" });
  await room.webSocketMessage(target, action);
  assert.equal(target.messages.length, 0);
  await room.webSocketMessage(creator, action);
  assert.equal(creator.messages.length, 0);
  assert.match(target.messages[0].note, /Private note$/);
  assert.equal((await join("guest-network")).status, 403);
  assert.equal((await join("other-network")).status, 200);
});
test("billing cycle clamps month ends, handles leap years and uses the configured day", () => {
  assert.equal(billingPeriod("15", Date.parse("2026-11-14T23:59:59Z")).start, "2026-10-15");
  assert.equal(billingPeriod("15", Date.parse("2026-11-15T00:00:00Z")).start, "2026-11-15");
  assert.equal(billingPeriod("31", Date.parse("2026-03-01T00:00:00Z")).start, "2026-02-28");
  assert.equal(billingPeriod("31", Date.parse("2024-03-01T00:00:00Z")).start, "2024-02-29");
  assert.equal(billingPeriod("1", Date.parse("2027-01-01T00:00:00Z")).start, "2027-01-01");
  for (const day of ["", "0", "32", "1.5", "15x"]) assert.throws(() => billingPeriod(day));
});
test("room capacity, fingerprint and seat authorization remain enforced without private codes", async () => {
  const { room, host } = await roomWithBudget({ allowed: false, reason: "budget" });
  assert.equal(validRoom("OPNZ-1234"), false);
  assert.equal(validRoom("OPNZ-" + "A".repeat(24)), true);
  assert.equal(host.secret.length, 32); assert.equal(host.seat.length, 32);
  assert.equal((await room.fetch(post("ice"))).status, 403);
  assert.equal((await room.fetch(post("ice", {}, "wrong-seat"))).status, 403);
  assert.equal((await room.fetch(post("join", { name: "Intruder", fingerprint }))).status, 409);
  const other = new DuelRoom(context(), {});
  await other.fetch(post("create", { name: "Host", fingerprint }));
  assert.equal((await other.fetch(post("join", { name: "Guest", fingerprint: "B".repeat(64) }))).status, 409);
  assert.equal((await room.fetch(new Request("https://internal/socket", { headers: { Upgrade: "websocket" } }))).status, 403);
});
test("account-wide TURN and SFU totals close the budget at the threshold", async () => {
  await withFetch(async (url, request) => {
    assert.equal(url, "https://api.cloudflare.com/client/v4/graphql");
    const data = JSON.parse(request.body);
    assert.equal(data.variables.account, config.CF_ACCOUNT_ID);
    assert.match(data.query, /callsTurnUsageAdaptiveGroups/); assert.match(data.query, /callsUsageAdaptiveGroups/);
    assert.doesNotMatch(data.query, /keyId|username|dimensions/);
    return usage(800000000000, 100000000000);
  }, async () => {
    const budget = new RelayBudget(context(), config);
    const result = await (await budget.fetch()).json();
    assert.deepEqual(result, { allowed: false, reason: "budget", relayRemainingBytes: "0" });
    assert.equal(JSON.stringify(result).includes(config.CF_ACCOUNT_ID), false);
  });
});
test("successful usage is cached for at most a minute; an old cached allowance never survives a failed refresh", async () => {
  const ctx = context(); let calls = 0;
  await withFetch(async () => { calls++; if (calls > 1) throw Error("offline"); return usage(100, 200); }, async () => {
    const budget = new RelayBudget(ctx, config);
    assert.equal((await (await budget.fetch()).json()).allowed, true);
    assert.equal((await (await budget.fetch()).json()).allowed, true); assert.equal(calls, 1);
    const state = await ctx.storage.get("budget"); state.checkedAt -= 61000; await ctx.storage.put("budget", state);
    assert.deepEqual(await (await budget.fetch()).json(), { allowed: false, reason: "unavailable" });
  });
});
test("threshold closure stays latched for the billing period even if later statistics decrease", async () => {
  let calls = 0;
  await withFetch(async () => { calls++; return usage(900000000000); }, async () => {
    const ctx = context(), budget = new RelayBudget(ctx, config);
    assert.equal((await (await budget.fetch()).json()).reason, "budget");
    const state = await ctx.storage.get("budget"); state.checkedAt -= 61000; await ctx.storage.put("budget", state);
    assert.equal((await (await budget.fetch()).json()).reason, "budget"); assert.equal(calls, 1);
  });
});
test("new billing period discards the previous closed budget and old grants", async () => {
  const ctx = context();
  await ctx.storage.put("budget", { identity: "previous-period", peak: 900000000000, checkedAt: Date.now(), grants: Array(120).fill(Date.now()) });
  await withFetch(async () => usage(), async () => assert.equal((await (await new RelayBudget(ctx, config).fetch()).json()).allowed, true));
});
test("all rooms share a rolling cap of 120 grants per hour", async () => {
  await withFetch(async () => usage(), async () => {
    const ctx = context(), budget = new RelayBudget(ctx, config);
    for (let i = 0; i < 120; i++) assert.equal((await (await budget.fetch()).json()).allowed, true);
    assert.equal((await (await budget.fetch()).json()).reason, "busy");
    const state = await ctx.storage.get("budget"); state.grants[0] -= 3600001; await ctx.storage.put("budget", state);
    assert.equal((await (await budget.fetch()).json()).allowed, true);
  });
});
test("missing or unsafe budget configuration refuses TURN without analytics requests", async () => {
  await withFetch(async () => { throw Error("must not query"); }, async () => {
    for (const change of [{ CF_ANALYTICS_TOKEN: "" }, { CF_ACCOUNT_ID: "" }, { TURN_BILLING_DAY: "" }, { TURN_BUDGET_BYTES: "0" }, { TURN_BUDGET_BYTES: "900000000001" }, { TURN_BUDGET_BYTES: "NaN" }])
      assert.equal((await (await new RelayBudget(context(), { ...config, ...change }).fetch()).json()).allowed, false);
    assert.equal((await (await new RelayBudget(context(), { ...config, TURN_ENABLED: "false" }).fetch()).json()).reason, "disabled");
  });
});
test("analytics HTTP, GraphQL, schema, account and malformed metric failures all refuse TURN", async () => {
  const samples = [
    () => new Response("unavailable", { status: 503 }),
    () => new Response("invalid-json"),
    () => new Response(JSON.stringify({ errors: [{ message: "permission denied" }], data: { viewer: { accounts: [] } } })),
    () => new Response(JSON.stringify({ data: { viewer: { accounts: [] } } })),
    () => new Response(JSON.stringify({ data: { viewer: { accounts: [{ turn: [], sfu: null }] } } })),
    () => usage(-1), () => usage("123"), () => usage(Number.MAX_SAFE_INTEGER + 1),
    () => { throw Error("network failure"); }
  ];
  for (const sample of samples) await withFetch(sample, async () => assert.deepEqual(await (await new RelayBudget(context(), config).fetch()).json(), { allowed: false, reason: "unavailable" }));
});
test("credentials are universal, expiring, UDP-only, and never contain permanent server secrets", async () => {
  let calls = 0;
  await withFetch(async (url, request) => {
    calls++; assert.match(url, /^https:\/\/rtc.live.cloudflare.com\//);
    assert.equal(JSON.parse(request.body).ttl, 21600);
    return new Response(JSON.stringify({ iceServers: { urls: ["turn:turn.cloudflare.com:3478?transport=udp", "turn:turn.cloudflare.com:3478?transport=tcp", "turns:turn.cloudflare.com:5349"], username: "temporary-user", credential: "temporary-password" } }));
  }, async () => {
    const { room, host, guest } = await roomWithBudget();
    for (const seat of [host.seat, guest.seat]) {
      const ice = await (await room.fetch(post("ice", {}, seat))).json();
      assert.equal(ice.relay, true); assert.equal(ice.iceServers[1].urls.length, 1);
      assert.equal(JSON.stringify(ice).includes("SERVER-"), false);
      assert.equal(JSON.stringify(ice).includes(config.CF_ACCOUNT_ID), false);
    }
    await room.fetch(post("ice", {}, host.seat)); assert.equal(calls, 2);
  });
});
test("budget closure suppresses even previously cached credentials", async () => {
  await withFetch(async () => new Response(JSON.stringify({ iceServers: { urls: ["turn:turn.cloudflare.com:3478"], username: "u", credential: "p" } })), async () => {
    const decision = { allowed: true }, { room, host } = await roomWithBudget(decision);
    assert.equal((await (await room.fetch(post("ice", {}, host.seat))).json()).relay, true);
    decision.allowed = false; decision.reason = "budget";
    const ice = await (await room.fetch(post("ice", {}, host.seat))).json();
    assert.equal(ice.relay, false); assert.equal(ice.relayReason, "budget"); assert.equal(ice.iceServers.length, 1);
  });
});
test("guard, missing binding and TURN provider failures preserve STUN-only play", async () => {
  await withFetch(async () => new Response(null, { status: 503 }), async () => {
    const { room, env, host } = await roomWithBudget();
    assert.equal((await (await room.fetch(post("ice", {}, host.seat))).json()).relay, false);
    delete env.RELAY_BUDGET;
    assert.equal((await (await room.fetch(post("ice", {}, host.seat))).json()).relay, false);
    env.TURN_ENABLED = "false";
    assert.equal((await (await room.fetch(post("ice", {}, host.seat))).json()).relayReason, "disabled");
  });
});
