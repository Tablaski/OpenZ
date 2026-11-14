using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>music_v3: OpenZ > Music v3. Check tracks = every added clip loads, is tagged "zopen" in its events, has a meta row and
    /// the right length (MUSIC V3 CHECK OK / FAIL, with a loud FAIL when music_v2's apply.py wiped the added clips); Run scenarios = the
    /// MusicCore timelines on this project's real playlists (EditMode is fine); Run fort reader scenarios (music_drama) = the fort
    /// reading on real headless worlds of L02 and L17 (units ordered at a fort, the walls rule, fights elsewhere; Edit mode only); in Play mode: Status, forced
    /// attacks (view-side reading only, the sim is untouched), a Zod line for the duck check, the overlay and Skip. In a level these replace OpenZ > Music v2 >
    /// Status / Skip (music_v2's A / B voices only play the main menu while music_v3 is on).</summary>
    public static class MusicV3Menu
    {
        static readonly string[] AddedEvents = { "mus_l02_calm", "mus_l17_calm", "mus_l02_attacking", "mus_l17_attacking", "mus_fort_danger" };
        const int WantAdded = 14 * 2 + 6 * 2 + 3;

        [Serializable] class Meta { public string n, src; public float len, tail, dramaIn, intensity; public float[] entries; }
        [Serializable] class MetaTable { public Meta[] tracks; }

        static (SoundTable table, Dictionary<string, Meta> meta) Load()
        {
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var mt = Resources.Load<TextAsset>(AudioDirector.MusicMetaPath);
            var meta = new Dictionary<string, Meta>();
            if (mt) { var m = JsonUtility.FromJson<MetaTable>(mt.text); if (m?.tracks != null) foreach (var t in m.tracks) meta[t.n] = t; }
            return (ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null, meta);
        }

        [MenuItem("OpenZ/Music v3/Check tracks")]
        public static void Check()
        {
            var (table, meta) = Load();
            var sb = new StringBuilder("[music_v3] track check\n"); int fails = 0;
            if (table == null) { Debug.LogError("[music_v3] sound table missing"); return; }
            var byId = new Dictionary<string, SoundEventDef>(); foreach (var e in table.events) byId[e.id] = e;
            int added = 0;
            foreach (var id in AddedEvents)
            {
                if (!byId.TryGetValue(id, out var e)) { sb.AppendLine($"  FAIL {id}: event missing"); fails++; continue; }
                int own = 0, guest = 0, z = 0, bad0 = fails;
                foreach (var c in e.clips ?? new SoundClipRef[0])
                {
                    var clip = Resources.Load<AudioClip>(c.res);
                    string n = c.res.Substring(c.res.LastIndexOf('/') + 1);
                    if (!clip) { sb.AppendLine($"  FAIL {id}: {c.res} does not load"); fails++; continue; }
                    if (!meta.TryGetValue(clip.name, out var m)) { sb.AppendLine($"  FAIL {id}: {clip.name} has no meta row"); fails++; }
                    else if (Mathf.Abs(m.len - clip.length) > 0.6f) { sb.AppendLine($"  FAIL {id}: {clip.name} meta length {m.len:0.0} s vs clip {clip.length:0.0} s"); fails++; }
                    bool isZ = n.StartsWith("zopen", StringComparison.Ordinal);
                    if (isZ != (c.line == "zopen")) { sb.AppendLine($"  FAIL {id}: {n} line '{c.line}'"); fails++; }
                    if (c.line == "zopen") { z++; added++; if (clip.loadType != AudioClipLoadType.Streaming) { sb.AppendLine($"  FAIL {n}: not streaming"); fails++; } }
                    else if (c.line == "guest") guest++; else own++;
                }
                sb.AppendLine($"  {(fails == bad0 ? "OK  " : "FAIL")} {id}: {own} own + {guest} guest + {z} added");
            }
            int zRows = 0; foreach (var kv in meta) if (kv.Value.src == "zopen") zRows++;
            foreach (var kv in meta) if (kv.Key.StartsWith("zopendramatic", StringComparison.Ordinal)) sb.AppendLine($"       {kv.Key}: drama entry {kv.Value.dramaIn:0.0} s, intensity {kv.Value.intensity:+0.00;-0.00}");
            sb.AppendLine($"  added clip slots {added} (want {WantAdded} = 14 calm x 2 + 6 battle x 2 + 3 drama), zopen meta rows {zRows} (want 23)");
            if (added == 0 || zRows == 0)
            {
                sb.AppendLine("  FAIL THE ADDED MUSIC IS GONE: the sound table / openz_music_meta.json have no zopen entries although music_v3's scripts are installed.");
                sb.AppendLine("       the import script was probably run again after music_v3 (it rebuilds every mus_* list from its own manifest).");
                sb.AppendLine("       Fix: run the import script (idempotent), refresh Unity, run this check again.");
                fails++;
            }
            else if (added != WantAdded || zRows != 23) { sb.AppendLine($"  FAIL {added} added clip slots / {zRows} rows"); fails++; }
            sb.AppendLine(fails == 0 ? "MUSIC V3 CHECK OK" : $"MUSIC V3 CHECK FAIL ({fails})");
            if (fails == 0) Debug.Log(sb.ToString()); else Debug.LogError(sb.ToString());
        }

        /// <summary>The playlists MusicCore gets in a level (AudioDirector.BuildMusicV3 rules), from the table + meta.</summary>
        public static List<MusicTrack> Catalog(string lv)
        {
            var (table, meta) = Load();
            var list = new List<MusicTrack>();
            if (table == null) return list;
            var byId = new Dictionary<string, SoundEventDef>(); foreach (var e in table.events) byId[e.id] = e;
            void Add(string id, MusicPool pool)
            {
                if (!byId.TryGetValue(id, out var e) || e.clips == null) return;
                foreach (var c in e.clips)
                {
                    string n = c.res.Substring(c.res.LastIndexOf('/') + 1);
                    if (!meta.TryGetValue(n, out var m)) continue;
                    float tail = Mathf.Clamp(Mathf.Min(m.tail, m.len * 0.25f), Mathf.Min(2.5f, m.len * 0.25f), 8f);
                    list.Add(new MusicTrack { id = list.Count, name = n, pool = pool, length = m.len, tail = tail, gainDb = e.volDb + c.gainDb, entries = m.entries,
                                              dramaIn = pool == MusicPool.Drama ? m.dramaIn : 0f, intensity = m.intensity, eventId = id, zopen = c.line == "zopen", guest = c.line == "guest",
                                              dramaDb = pool == MusicPool.Drama ? MusicDramaGain.For(lv, n) : null });   // music_drama: the measured drama lift, as AudioDirector.AddPool
                }
            }
            Add("mus_" + lv + "_calm", MusicPool.Calm); Add("mus_" + lv + "_attacking", MusicPool.Battle); Add("mus_fort_danger", MusicPool.Drama);
            Add("mus_victory_lap", MusicPool.Victory); Add("mus_menu", MusicPool.Menu);
            return list;
        }

        [MenuItem("OpenZ/Music v3/Run state machine scenarios")]
        public static void RunScenarios()
        {
            var sb = new StringBuilder();
            bool ok = MusicCoreScenarios.RunAll(Catalog("l02"), Catalog("l17"), s => sb.AppendLine(s), 20, 40, Catalog("l07"));   // music_drama: + L07 for the drama gain table
            string head = sb.ToString(); int cut = head.IndexOf("DETAIL", StringComparison.Ordinal);
            string summary = cut > 0 ? head.Substring(0, cut) : head;
            if (ok) Debug.Log("[music_v3] " + summary); else Debug.LogError("[music_v3] " + summary);
            try { var p = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "..", "..", "work", "tmp", "music_v3", "editor_scenarios.txt")); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p)); System.IO.File.WriteAllText(p, head); Debug.Log("[music_v3] timelines: " + p); }
            catch (Exception ex) { Debug.LogWarning("[music_v3] could not write the timelines: " + ex.Message); }
        }

        /// <summary>music_drama: MusicFortReaderScenarios on headless L02 / L17 worlds (Edit mode only: refused in Play mode, as the run sets the
        /// rules' static switches for its own worlds; nothing in the scene is touched).</summary>
        [MenuItem("OpenZ/Music v3/Run fort reader scenarios (walls, fort under fire)")]
        public static void RunReaderScenarios()
        {
            if (Application.isPlaying) { Debug.LogWarning("[music_drama] Run fort reader scenarios in Edit mode only: it sets the rules' static switches (put back afterwards) and builds worlds of its own, which a running match shares"); return; }
            var st = Resources.Load<TextAsset>("OpenZ/default_settings");
            if (!st) { Debug.LogError("[music_drama] OpenZ/default_settings missing"); return; }
            var sb = new StringBuilder();
            bool ok = MusicFortReaderScenarios.RunAll(st.text, lv => { var m = Resources.Load<TextAsset>($"OpenZ/Maps/{lv}_grid"); return m ? m.text : null; }, s => sb.AppendLine(s));
            if (ok) Debug.Log("[music_drama] " + sb); else Debug.LogError("[music_drama] " + sb);
        }

        /// <summary>music_drama Play proof: the drama order starts a fresh round (strongest first: zopendramatic3 opens it). Deletes only
        /// the two keys AudioDirector.MusicV3 keeps the order in (oz_music_drama_bag, oz_music_drama_last; read when a match builds its
        /// music), Edit mode only, so the next match starts a new round.</summary>
        [MenuItem("OpenZ/Music v3/Reset the drama order - next match starts strongest first (Edit mode)")]
        public static void ResetDramaOrder()
        {
            if (Application.isPlaying) { Debug.LogWarning("[music_drama] reset the drama order in Edit mode (a running match read it at its start)"); return; }
            string bag = PlayerPrefs.GetString("oz_music_drama_bag", ""), last = PlayerPrefs.GetString("oz_music_drama_last", "");
            PlayerPrefs.DeleteKey("oz_music_drama_bag"); PlayerPrefs.DeleteKey("oz_music_drama_last"); PlayerPrefs.Save();
            Debug.Log($"[music_drama] drama order reset (was: last '{last}', still to play '{bag}'): the next match's first drama is the strongest track");
        }

        static AudioDirector Live() { var ad = AudioDirector.I; if (!Application.isPlaying || !ad) { Debug.Log("[music_v3] enter Play mode in a level first"); return null; } return ad; }

        [MenuItem("OpenZ/Music v3/Status (Play mode)")]
        public static void Status() { var ad = Live(); if (ad) Debug.Log(ad.MusicV3Status() + "\n" + string.Join("\n", ad.MusicV3Core != null ? ad.MusicV3Core.History : new List<string>())); }

        [MenuItem("OpenZ/Music v3/Force big attack on OUR fort 20 s (Play mode)")]
        public static void ForceOurs() { var ad = Live(); if (ad) ad.MusicV3Force(1, 20f); }

        [MenuItem("OpenZ/Music v3/Force big attack on ENEMY fort 20 s (Play mode)")]
        public static void ForceTheirs() { var ad = Live(); if (ad) ad.MusicV3Force(2, 20f); }

        [MenuItem("OpenZ/Music v3/Force HEAVY attack on OUR fort 20 s (Play mode)")]
        public static void ForceHeavy() { var ad = Live(); if (ad) ad.MusicV3Force(4, 20f); }

        [MenuItem("OpenZ/Music v3/Force ONE 2-point unit at our fort while it is hit 30 s - must NOT start the drama (Play mode)")]
        public static void ForceOne() { var ad = Live(); if (ad) ad.MusicV3Force(3, 30f); }

        [MenuItem("OpenZ/Music v3/Force ONE light tank hitting our fort 20 s - drama after 3 s (Play mode)")]
        public static void ForceLight() { var ad = Live(); if (ad) ad.MusicV3Force(6, 20f); }

        [MenuItem("OpenZ/Music v3/Force a Tough squad rocketing our fort 20 s - drama after 3 s (Play mode)")]
        public static void ForceTough() { var ad = Live(); if (ad) ad.MusicV3Force(7, 20f); }

        [MenuItem("OpenZ/Music v3/Force idle units parked at our fort 60 s - must NOT start the drama (Play mode)")]
        public static void ForceIdle() { var ad = Live(); if (ad) ad.MusicV3Force(5, 60f); }

        /// <summary>music_drama Play proof (the lead's real L02 match): the player's own order, as a click would give it, to our nearest light tank
        /// and our nearest robot squad (one that can hurt the fort first: Toughs or grenades): attack THEIR fort. Nothing is made or moved by
        /// the menu itself; the drama must then come 3 s after their fort takes its first hit (log "[MusicV3] ... BIG ATTACK on their fort:
        /// fort under fire ..." or "big attack"). L02 starts with no light tank of ours: board one of the two neutral ones first.</summary>
        [MenuItem("OpenZ/Music v3/Play proof - send our light tank + a squad at THEIR fort (Play mode)")]
        public static void ProofAttackTheirs()
        {
            if (!Live()) return;
            var g = ZGame.I; var w = g ? g.World : null;
            if (w == null) { Debug.Log("[music_drama] no live world"); return; }
            Team me = g.player, them = me == Team.Red ? Team.Blue : Team.Red;
            OpenZ.Sim.ZObject fort = null; foreach (var o in w.Objects) if (o.IsFort && o.Owner == them && !o.Destroyed) { fort = o; break; }
            if (fort == null) { Debug.Log("[music_drama] their fort is gone"); return; }
            OpenZ.Sim.ZObject Near(System.Func<OpenZ.Sim.ZObject, bool> ok)
            {
                OpenZ.Sim.ZObject best = null; double bd = double.MaxValue;
                foreach (var o in w.Objects)
                {
                    if (o.Destroyed || o.Owner != me || o.IsMinion || !ok(o)) continue;
                    double d = (double)(o.CenterX - fort.CenterX) * (o.CenterX - fort.CenterX) + (double)(o.CenterY - fort.CenterY) * (o.CenterY - fort.CenterY);
                    if (d < bd) { bd = d; best = o; }
                }
                return best;
            }
            var tank = Near(o => o.Kind == OpenZ.Sim.ZKind.Vehicle && o.Type == "light");
            var squad = Near(o => o.IsRobot && o.HasExplosives()) ?? Near(o => o.IsRobot);                 // a squad that can hurt the fort first (Toughs, grenades)
            int sent = 0;
            foreach (var u in new[] { tank, squad })
                if (u != null && w.SendWaypoints(me, u, new[] { new OpenZ.Sim.ZWaypoint { mode = OpenZ.Sim.WpMode.Attack, refId = fort.Id, x = fort.CenterX, y = fort.CenterY } })) sent++;
            Debug.Log($"[music_drama] Play proof: {sent} order(s) to attack the {them} fort: {(tank != null ? "light tank " + tank.Id : "NO light tank of ours (L02 starts with none: first send a squad into one of the two neutral light tanks, then use this menu again)")}, " +
                      $"{(squad != null ? squad.Type + " squad " + squad.Id + (squad.HasExplosives() ? " (can hurt the fort)" : " (no grenades: it cannot hurt the fort; only its attack on the fort's guns / defender counts)") : "no squad")}");
        }

        [MenuItem("OpenZ/Music v3/Clear forced reading (Play mode)")]
        public static void ForceClear() { var ad = Live(); if (ad) ad.MusicV3Force(0, 0f); }

        [MenuItem("OpenZ/Music v3/Zod line now - duck check (Play mode)")]
        public static void ZodLine() { if (Live()) Debug.Log("[music_v3] " + ZodBriefing.DebugIdle()); }

        [MenuItem("OpenZ/Music v3/Toggle overlay")]
        public static void Overlay() { AudioDirector.MusicV3Overlay = !AudioDirector.MusicV3Overlay; Debug.Log("[music_v3] overlay " + (AudioDirector.MusicV3Overlay ? "on" : "off")); }

        [MenuItem("OpenZ/Music v3/Skip regular song (Play mode)")]
        public static void Skip() { var ad = Live(); if (ad) ad.MusicV3Skip(); }
    }
}
