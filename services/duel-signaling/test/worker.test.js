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
    assert.deepEqual(result, { allowed: false, reason: "budget" });
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
