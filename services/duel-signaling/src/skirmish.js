import { json, peerIce, randomToken } from "./worker.js";

const LIFETIME = 6 * 60 * 60 * 1000;
const MONITOR_MS = 60_000;
const CHANNELS = [
  { dataChannelName: "openz-game", ordered: true },
  { dataChannelName: "openz-voice", ordered: false, maxRetransmits: 0 }
];

export class SkirmishRoom {
  constructor(ctx, env) { this.ctx = ctx; this.env = env; }

  async create(code, player) {
    const host = { id: 1, name: player.name, seat: randomToken() };
    const room = { skirmish: true, code, secret: randomToken(), fingerprint: player.fingerprint,
      sfu: false, protocol: player.skirmishProtocol === 2 ? 2 : 1, epoch: 1, nextId: 2, expires: Date.now() + LIFETIME, players: [host] };
    await this.ctx.storage.put("room", room);
    await this.ctx.storage.setAlarm(room.expires);
    return this.credentials(room, host);
  }

  credentials(room, player) {
    return json({ room: room.code, secret: room.secret, seat: player.seat, role: player.id === 1 ? "host" : "guest", skirmish: true, id: player.id, skirmishProtocol: room.protocol ?? 1 });
  }

  sockets(id) { return this.ctx.getWebSockets(id == null ? "skirmish" : `skirmish-${id}`); }

  send(socket, message) {
    try { socket.send(JSON.stringify(message)); } catch { /* The endpoint may have just closed. */ }
  }

  connected(room) { return room.players.filter(player => this.sockets(player.id).length); }

  roster(room) {
    const players = this.connected(room)
      .map(({ id, name, published }) => ({ id, name, published: !!published }));
    for (const socket of this.sockets()) this.send(socket, { type: "roster", epoch: room.epoch, sfu: room.sfu, players });
  }

  async handle(room, path, data, request, validPlayer) {
    if (Date.now() >= room.expires) return json({ error: "This invitation has expired." }, 404);
    if (path === "/join") {
      if (!validPlayer(data) || data.fingerprint !== room.fingerprint || data.skirmishProtocol !== (room.protocol ?? 1))
        return json({ error: "Use the same game version as the host." }, 409);
      if (room.locked) return json({ error: "This match has already started." }, 409);
      if (room.hostClosed || !this.sockets(1).length) return json({ error: "The host has left the lobby." }, 409);
      const network = request.headers.get("X-Room-Network");
      if (network && room.bans?.includes(network)) return json({ error: "The host banned access to this lobby." }, 403);
      room.players = room.players.filter(player => player.id === 1 || this.sockets(player.id).length || player.joined > Date.now() - MONITOR_MS);
      if (room.players.length >= 8) return json({ error: "The lobby is full." }, 409);
      const player = { id: room.nextId++, name: data.name, seat: randomToken(), joined: Date.now(), network };
      room.players.push(player);
      await this.ctx.storage.put("room", room);
      return this.credentials(room, player);
    }
    const seat = request.headers.get("Authorization")?.match(/^Bearer ([A-Za-z0-9_-]{32})$/)?.[1];
    const player = seat && room.players.find(candidate => candidate.seat === seat);
    if (!player || room.hostClosed) return json({ error: "Invalid room access." }, 403);
    if (path === "/ice") {
      if (room.sfu || request.method !== "POST" || data?.epoch !== room.epoch ||
          !this.sockets(player.id).length || this.connected(room).length < 2)
        return json({ error: "Direct connections are unavailable for this roster." }, 409);
      return await peerIce(this.env, this.ctx.storage, room, player);
    }
    if (path === "/sfu") {
      if (!room.sfu || data?.epoch !== room.epoch || !this.sockets(player.id).length) return json({ error: "The lobby uses direct connections." }, 409);
      return await this.sfu(room, player, data);
    }
    if (path !== "/socket" || request.headers.get("Upgrade")?.toLowerCase() !== "websocket") return json({ error: "Expected a connection upgrade." }, 400);
    if (player.id !== 1 && player.network && room.bans?.includes(player.network)) return json({ error: "The host banned access to this lobby." }, 403);
    if (this.sockets(player.id).length) return json({ error: "This participant is already connected." }, 409);
    if (room.locked) return json({ error: "This match has already started." }, 409);
    const [client, server] = Object.values(new WebSocketPair());
    this.ctx.acceptWebSocket(server, ["skirmish", `skirmish-${player.id}`]);
    server.serializeAttachment({ skirmish: true, id: player.id });
    if (!await this.updateRoute(room)) this.roster(room);
    return new Response(null, { status: 101, webSocket: client });
  }

  async message(socket, message) {
    const state = socket.deserializeAttachment();
    if (typeof message !== "string" || message.length > 62000) { socket.close(1008, "Invalid signaling message."); return; }
    let data;
    try { data = JSON.parse(message); } catch { socket.close(1008, "Invalid signaling message."); return; }
    const room = await this.ctx.storage.get("room");
    if (!room || room.hostClosed || !data) return;
    if ((data.type === "lock" || data.type === "unlock") && state.id === 1) {
      room.locked = data.type === "lock";
      await this.ctx.storage.put("room", room);
      return;
    }
    if (data.type === "moderate" && state.id === 1 && Number.isInteger(data.id) && data.id > 1) {
      const target = room.players.find(player => player.id === data.id);
      if (!target) return;
      if (data.ban === true && target.network) {
        room.bans = [...new Set([...(room.bans ?? []), target.network])];
        // Invalidate previously issued, unused seats from the banned network too.
        room.players = room.players.filter(player => player.id === 1 || player.id === target.id ||
          player.network !== target.network || this.sockets(player.id).length);
        await this.ctx.storage.put("room", room);
      }
      const reason = data.ban === true ? "The host banned you from this lobby." : "The host removed you from this lobby.";
      const note = typeof data.note === "string" ? data.note.replace(/[\u0000-\u001f\u007f]/g, " ").slice(0, 160) : "";
      for (const peer of this.sockets(target.id)) {
        this.send(peer, { type: "closed", note: note ? `${reason} ${note}` : reason });
        peer.close(1008, reason);
      }
      return;
    }
    if (data.epoch !== room.epoch) return;
    if (data.type === "fallback" && room.sfu) { await this.fallback(room); return; }
    if (data.type === "drop" && state.id === 1 && Number.isInteger(data.id) && data.id > 1) {
      for (const peer of this.sockets(data.id)) peer.close(1008, "Connection to the host could not be established.");
      return;
    }
    if (room.sfu) return;
    const allowed = state.id === 1 ? data.type === "offer" && data.id > 1 : data.type === "answer" && data.id === 1;
    if (!allowed || !Number.isInteger(data.id) || typeof data.sdp !== "string" || data.sdp.length > 60000) return;
    if (state.epoch !== room.epoch) { state.seen = []; state.epoch = room.epoch; }
    if (state.seen.includes(data.id)) return;
    state.seen.push(data.id);
    socket.serializeAttachment(state);
    for (const peer of this.sockets(data.id)) this.send(peer, { type: data.type, id: state.id, epoch: room.epoch, sdp: data.sdp });
  }

  async left(socket) {
    const { id } = socket.deserializeAttachment();
    const room = await this.ctx.storage.get("room");
    try { socket.close(1000, "Closed"); } catch { /* The peer already closed. */ }
    if (!room || room.hostClosed) return;
    const player = room.players.find(candidate => candidate.id === id);
    if (!player) return;
    if (id === 1) {
      room.hostClosed = true;
      await this.ctx.storage.put("room", room);
      for (const peer of this.sockets()) {
        this.send(peer, { type: "closed" });
        try { peer.close(1000, "The host left."); } catch { /* Already closed. */ }
      }
      await this.closePublications(room.players);
      return;
    }
    room.players = room.players.filter(candidate => candidate !== player);
    if (!await this.updateRoute(room)) {
      await this.ctx.storage.put("room", room);
      this.roster(room);
      if (room.sfu) await this.closeSubscriptions(room, id);
    }
    await this.closePublications([player]);
  }

  async hasBudget(monitor) {
    try {
      const binding = this.env.RELAY_BUDGET;
      const result = await binding.get(binding.idFromName("account")).fetch(new Request(`https://internal/${monitor ? "monitor" : "check"}`));
      return result.ok && (await result.json()).allowed === true;
    } catch { return false; }
  }

  async api(path, data, method = "POST") {
    const response = await fetch(`https://rtc.live.cloudflare.com/v1/apps/${encodeURIComponent(this.env.SFU_APP_ID)}${path}`, {
      method, headers: { Authorization: `Bearer ${this.env.SFU_APP_SECRET}`, "Content-Type": "application/json" },
      body: data == null ? undefined : JSON.stringify(data), signal: AbortSignal.timeout(5000)
    });
    if (!response.ok) throw new Error("SFU request failed.");
    const result = await response.json();
    if (result.errorCode) throw new Error("SFU request rejected.");
    return result;
  }

  async sfu(room, player, data) {
    const suffix = player.session ? `/sessions/${encodeURIComponent(player.session)}` : null;
    try {
      if (data.type === "setup" && !suffix) {
        if (!await this.hasBudget(false)) { await this.fallback(room); return json({ error: "Relay allowance unavailable." }, 409); }
        const { sessionId } = await this.api("/sessions/new");
        if (typeof sessionId !== "string" || !sessionId) throw new Error();
        player.session = sessionId;
        const result = await this.api(`/sessions/${encodeURIComponent(sessionId)}/datachannels/establish`,
          { dataChannel: { location: "remote", dataChannelName: "server-events" } });
        if (!this.validId(result.dataChannel?.id) || result.dataChannel.errorCode || result.sessionDescription?.type !== "offer") throw new Error();
        player.setup = result.dataChannel.id;
        const firstSession = !room.budgetCheckAt;
        if (firstSession) room.budgetCheckAt = Date.now() + MONITOR_MS;
        await this.ctx.storage.put("room", room);
        if (firstSession) await this.ctx.storage.setAlarm(Math.min(room.expires, room.budgetCheckAt));
        return json({ sdp: result.sessionDescription.sdp, game: player.setup });
      }
      if (!suffix) return json({ error: "Missing SFU session." }, 409);
      if (data.type === "answer" && !player.answered && typeof data.sdp === "string" && data.sdp.length <= 60000) {
        await this.api(suffix + "/renegotiate", { sessionDescription: { type: "answer", sdp: data.sdp } }, "PUT");
        player.answered = true;
        await this.ctx.storage.put("room", room);
        return json({});
      }
      if (!player.answered) return json({ error: "The SFU offer is pending." }, 409);
      if (data.type === "publish") {
        if (!player.published) {
          player.published = await this.allocate(suffix, "local");
          await this.ctx.storage.put("room", room);
          this.roster(room);
        }
        return json(player.published);
      }
      if (data.type === "subscribe" && (player.id === 1 ? data.id > 1 : data.id === 1)) {
        const source = room.players.find(candidate => candidate.id === data.id);
        if (!source?.published) return json({ error: "The publisher has left." }, 404);
        player.subscriptions ??= {};
        if (!player.subscriptions[source.id]) {
          player.subscriptions[source.id] = await this.allocate(suffix, "remote", source.session);
          await this.ctx.storage.put("room", room);
        }
        return json(player.subscriptions[source.id]);
      }
      return json({ error: "Invalid SFU operation." }, 400);
    } catch {
      await this.fallback(room);
      return json({ error: "The relay could not be established. Trying a direct connection." }, 503);
    }
  }

  validId(id) { return Number.isInteger(id) && id >= 0 && id <= 65534; }

  async allocate(suffix, location, sessionId) {
    const dataChannels = CHANNELS.map(channel => ({ ...channel, location, sessionId }));
    const response = await this.api(suffix + "/datachannels/new", { dataChannels });
    const ids = CHANNELS.map(channel => response.dataChannels?.find(result => result.dataChannelName === channel.dataChannelName && !result.errorCode)?.id);
    if (ids.some(id => !this.validId(id)) || ids[0] === ids[1]) {
      await this.closeChannels(suffix, ids.filter(id => this.validId(id)));
      throw new Error("Incomplete SFU channel allocation.");
    }
    return { game: ids[0], voice: ids[1] };
  }

  async closeChannels(suffix, ids) {
    if (!ids.length) return;
    try { await this.api(suffix + "/datachannels/close", { dataChannels: ids.map(id => ({ id })) }, "PUT"); }
    catch { /* Endpoints also close their PeerConnections. */ }
  }

  closePublications(players) {
    return Promise.all(players.filter(player => player.session).map(player => {
      const ids = player.published ? [player.published.game, player.published.voice] : [];
      return this.closeChannels(`/sessions/${encodeURIComponent(player.session)}`, ids);
    }));
  }

  async closeSubscriptions(room, id) {
    const host = room.players.find(player => player.id === 1);
    const channels = host?.subscriptions?.[id];
    if (!channels) return;
    delete host.subscriptions[id];
    await this.ctx.storage.put("room", room);
    await this.closeChannels(`/sessions/${encodeURIComponent(host.session)}`, [channels.game, channels.voice]);
  }

  async updateRoute(room) {
    const sfu = !room.sfuUnavailable && this.env.SFU_ENABLED === "true" &&
      !!this.env.SFU_APP_ID && !!this.env.SFU_APP_SECRET && this.connected(room).length > 2;
    if (sfu === room.sfu) return false;
    await this.switchRoute(room, sfu);
    return true;
  }

  async switchRoute(room, sfu) {
    const previous = room.players.map(player => ({ ...player }));
    for (const player of room.players) {
      delete player.session;
      delete player.setup;
      delete player.answered;
      delete player.published;
      delete player.subscriptions;
    }
    room.sfu = sfu;
    room.epoch++;
    delete room.budgetCheckAt;
    await this.ctx.storage.put("room", room);
    this.roster(room);
    await this.ctx.storage.setAlarm(room.expires);
    await this.closePublications(previous);
  }

  async fallback(room) {
    if (!room.sfu) return;
    room.sfuUnavailable = true;
    await this.switchRoute(room, false);
  }

  async alarm(room) {
    if (room.expires <= Date.now()) {
      for (const socket of this.sockets()) socket.close(1000, "Invitation expired.");
      await this.closePublications(room.players);
      await this.ctx.storage.deleteAll();
    } else if (room.sfu && !room.hostClosed) {
      if (!await this.hasBudget(true)) await this.fallback(room);
      else await this.ctx.storage.setAlarm(Math.min(room.expires, Date.now() + MONITOR_MS));
    }
  }
}
