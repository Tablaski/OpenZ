using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Picked = the game's own selection (PlayerInput.Selection, both teams through View/PlayerInput.BothSides.cs). The inspector follows it
    /// (OzUeField.LateUpdate reconciles once a frame): a unit picked by a click = that unit; a box = the first picked; a squad's new leader
    /// after its leader died = the new leader; the selection emptied = back to the list. A dock tile, a list / balance / table row or a
    /// card shows a TYPE (the field selection stays as it is); a flag, an obstacle or empty hardware clicked on the field shows that
    /// object. Changed is raised once a frame at most, from LateUpdate.
    /// </summary>
    public static class OzUePick
    {
        static readonly List<ZObject> none = new List<ZObject>();
        static readonly List<ZObject> lastSel = new List<ZObject>();
        static readonly HashSet<object> holders = new HashSet<object>();
        static bool pending;

        static PlayerInput In => OzUnitEditor.I ? OzUnitEditor.I.Input : null;
        static ZWorld W => OzUnitEditor.I ? OzUnitEditor.I.World : null;

        /// <summary>PlayerInput.Selection (both teams; leaders, vehicles, guns, shuttles).</summary>
        public static IReadOnlyList<ZObject> Picked { get { var i = In; return i ? (IReadOnlyList<ZObject>)i.Selection : none; } }
        /// <summary>Picked units of a team, each robot of a squad counted.</summary>
        public static int PickedCount(Team team)
        {
            var i = In; if (!i) return 0;
            int n = 0;
            foreach (var u in i.Selection)
            {
                if (u == null || u.Destroyed || u.Owner != team || !u.IsUnit) continue;
                n++;
                foreach (var m in u.Minions) if (m != null && !m.Destroyed && m.KillAt < 0) n++;
            }
            return n;
        }
        /// <summary>Any unit / flag / obstacle under the pointer (a squad member = its leader), null over a panel (set by OzUeField every frame).</summary>
        public static ZObject Hover { get; internal set; }
        public static UeInspect Kind { get; private set; }
        public static ZObject Inspected { get; private set; }
        public static string InspectedId { get; private set; }
        public static Team InspectedTeam { get; private set; }
        public static event Action Changed;

        /// <summary>A tile / row / table / card asks for a type's inspector (the field selection is kept).</summary>
        public static void InspectType(string id, Team team)
        {
            if (OzUePlayground.KindOf(id) == UeKind.None || OzUePlayground.KindOf(id) == UeKind.Grenades || OzUePlayground.KindOf(id) == UeKind.Nuke || OzUePlayground.KindOf(id) == UeKind.Platform) return;   // a crate (1.3 item 2: and the NUKE, and the ramp platforms) has no stats card
            if (OzUePlayground.AlwaysNeutral(id)) team = Team.Neutral;
            Set(UeInspect.Type, null, id, team);
        }

        /// <summary>A field object: a flag, an obstacle, a grenade crate or empty hardware is shown as itself; a unit of a team is picked (its
        /// inspector follows).</summary>
        public static void InspectObject(ZObject o)
        {
            if (o == null) return;
            if (OzUePlayground.IsFlag(o)) Set(UeInspect.Flag, o, OzUePlayground.Flag, o.Owner);
            else if (OzUePlayground.IsObstacle(o)) Set(UeInspect.Obstacle, o, OzUePlayground.Obstacle, Team.Neutral);
            else if (OzUePlayground.IsPlatform(o)) Set(UeInspect.Platform, o, OzUePlayground.PlatformIdOf(o), Team.Neutral);   // UI/OzUeObjectPanel.Platform.cs
            else if (OzUePlayground.IsCrate(o)) Set(UeInspect.Crate, o, OzUePlayground.Grenades, Team.Neutral);   // the GRENADES crate (UI/OzUeObjectPanel.Crate.cs)
            else if (OzUePlayground.IsEmptyHardware(o)) Set(UeInspect.EmptyHardware, o, OzUePlayground.IdOf(o), Team.Neutral);
            else if (o.IsUnit && o.Owner != Team.Neutral) Pick(new[] { o }, false);
        }

        /// <summary>Back to the list (unpicks too).</summary>
        public static void ClearInspect()
        {
            Unpick();
            Set(UeInspect.None, null, null, Team.Neutral);
        }

        public static void Unpick()
        {
            var i = In;
            if (i && i.Selection.Count > 0) { i.Selection.Clear(); pending = true; }
        }

        /// <summary>Picks units of either team exactly as a click does (selection time, the selection cue and voice).</summary>
        public static void Pick(IEnumerable<ZObject> units, bool add)
        {
            var i = In; if (!i) return;
            i.UePick(units, add);
            pending = true;
        }

        /// <summary>PlayerInput stays off while an owner holds it (click-to-place). OzUeField applies it before PlayerInput runs.</summary>
        public static void HoldInput(object owner, bool hold)
        {
            if (owner == null) return;
            bool was = holders.Count > 0;
            if (hold) holders.Add(owner); else holders.Remove(owner);
            if (was != holders.Count > 0) Debug.Log($"{OzUnitEditor.Tag} field input {(holders.Count > 0 ? "held (placing)" : "released")}");
        }
        public static bool InputHeld => holders.Count > 0;

        // ------------------------------------------------------------------------------------------------ OzUeField
        internal static void Set(UeInspect k, ZObject o, string id, Team team)
        {
            if (Kind == k && Inspected == o && InspectedId == id && InspectedTeam == team) return;
            Kind = k; Inspected = o; InspectedId = id; InspectedTeam = team;
            pending = true;
            Debug.Log($"{OzUnitEditor.Tag} inspect {k}{(id != null ? " " + id : "")}{(o != null ? " #" + o.Id : "")}{(k == UeInspect.Type || k == UeInspect.Unit ? " (" + OzUeUi.TeamWord(team) + ")" : "")}");
        }

        static bool Live(ZObject o, ZWorld w) => o != null && w != null && w.Get(o.Id) == o && !o.Destroyed && o.KillAt < 0;

        /// <summary>Once a frame (OzUeField.LateUpdate, after PlayerInput): the inspector follows the selection. leftHeld = a left press on the
        /// field is still down: nothing is decided until it comes up (PlayerInput cleared the selection on the press and a box grows it every
        /// frame: the box shows no list flash and no inspector churn; the release decides once).</summary>
        internal static void Reconcile(bool leftHeld)
        {
            var i = In; var w = W;
            if (!i || w == null || leftHeld) return;
            var sel = i.Selection;
            bool changed = sel.Count != lastSel.Count;
            if (!changed) for (int k = 0; k < sel.Count; k++) if (!lastSel.Contains(sel[k])) { changed = true; break; }
            if (changed)
            {
                ZObject added = null; int nAdded = 0;
                foreach (var u in sel) if (!lastSel.Contains(u)) { added = u; nAdded++; }
                lastSel.Clear(); lastSel.AddRange(sel);
                pending = true;
                if (sel.Count > 0)
                {
                    ZObject t = nAdded == 1 ? added : Kind == UeInspect.Unit && sel.Contains(Inspected) ? Inspected : sel[0];
                    if (t != null) Set(UeInspect.Unit, t, OzUePlayground.IdOf(t), t.Owner);
                }
                else if (Kind == UeInspect.Unit || Kind == UeInspect.Type) Set(UeInspect.None, null, null, Team.Neutral);
            }
            switch (Kind)
            {
                case UeInspect.Unit:
                    if (!Live(Inspected, w) || !sel.Contains(Inspected))
                    {
                        ZObject t = null; foreach (var u in sel) if (Live(u, w)) { t = u; break; }
                        if (t != null) Set(UeInspect.Unit, t, OzUePlayground.IdOf(t), t.Owner); else Set(UeInspect.None, null, null, Team.Neutral);
                    }
                    else if (Inspected.Owner != InspectedTeam) Set(UeInspect.Unit, Inspected, InspectedId, Inspected.Owner);
                    break;
                case UeInspect.Flag:
                    if (!Live(Inspected, w)) Set(UeInspect.None, null, null, Team.Neutral);
                    else if (Inspected.Owner != InspectedTeam) Set(UeInspect.Flag, Inspected, InspectedId, Inspected.Owner);   // taken: the panel shows the new owner
                    break;
                case UeInspect.Obstacle: case UeInspect.Crate: case UeInspect.Platform:                                            // a crate: collected, blown up or removed = back to the list
                    if (!Live(Inspected, w)) Set(UeInspect.None, null, null, Team.Neutral);
                    break;
                case UeInspect.EmptyHardware:
                    if (!Live(Inspected, w) || Inspected.Owner != Team.Neutral) Set(UeInspect.None, null, null, Team.Neutral);   // destroyed, removed or a robot climbed in
                    break;
            }
        }

        /// <summary>OzUeField.LateUpdate: Changed once a frame at most.</summary>
        internal static void RaiseIfChanged()
        {
            if (!pending) return;
            pending = false;
            try { Changed?.Invoke(); }
            catch (Exception e) { Debug.LogError($"{OzUnitEditor.Tag} an OzUePick.Changed listener threw: {e}"); }
        }

        internal static void Reset()
        {
            Changed = null; holders.Clear(); lastSel.Clear(); pending = false; Hover = null;
            Kind = UeInspect.None; Inspected = null; InspectedId = null; InspectedTeam = Team.Neutral;
        }
    }
}
