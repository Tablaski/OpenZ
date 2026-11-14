// OpenZ (GPL-3.0) - version 1.4, speed-up 4 (view side): whole-scene searches that ran every frame or every few seconds read small
// registries instead (View/OzLiveSet.cs), each giving exactly the answer the old search gave:
//  * the ZodObjects root, kept once found: GameObject.Find walked every active object for it (each level scene holds one);
//  * DayNight: AudioDirector's ambience asked the whole scene for it every frame where there is none (the Unit editor);
//  * the RockPillar count (RockSeeThru, every 3 s) and the VehicleMotion set (OzTreadSteady, every 1 s);
//  * the walk decks (OzBridgeWobble walked the whole ZodObjects tree, every unit's bones too, every 1.5 s);
//  * BridgeMarkings searches for nothing once every deck has its plate (every 0.5 s).
// Nothing is spread over frames: what each site gets, and when, is what it got before. View only: no rule of the game reads any of it.
// On (default true): the registries. Off: every site searches exactly as in 1.3 (the A-B check). Shadow (the counted check only):
// every site runs both ways, counts any difference, times both, and keeps the old answer.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public static class OzViewScans
    {
        /// <summary>The registries (true) or the old whole-scene searches (false, exactly as in 1.3).</summary>
        public static bool On = true;
        /// <summary>The counted check: every site runs the old search and the registry, counts a difference, keeps the old answer.</summary>
        public static bool Shadow;

        /// <summary>One converted site: how often it ran, how often (in Shadow) the two answers differed, and the time of each way.</summary>
        public sealed class Site
        {
            public readonly string Name;
            public long Calls, Diff, Timed, OldTicks, NewTicks;
            public string FirstDiff;
            internal Site(string name) { Name = name; }
            public double OldMs => Timed > 0 ? OldTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Timed : 0.0;
            public double NewMs => Timed > 0 ? NewTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Timed : 0.0;
        }
        public static readonly Site RootSite = new Site("ZodObjects root (ZGame.SpawnUnitView, BridgeMarkings, the walk decks)");
        public static readonly Site DaySite = new Site("DayNight (AudioDirector ambience, every frame while there is none)");
        public static readonly Site RockSite = new Site("RockPillar count (RockSeeThru, every 3 s)");
        public static readonly Site TreadSite = new Site("VehicleMotion set (OzTreadSteady, every 1 s)");
        public static readonly Site DeckSite = new Site("walk deck list (OzBridgeWobble, level 16, every 1.5 s)");
        public static readonly Site PlateSite = new Site("BridgeMarkings: every deck has its plate, nothing to search (every 0.5 s)");
        public static readonly Site[] Sites = { RootSite, DaySite, RockSite, TreadSite, DeckSite, PlateSite };

        public static void ResetCounts()
        {
            foreach (var s in Sites) { s.Calls = s.Diff = s.Timed = s.OldTicks = s.NewTicks = 0; s.FirstDiff = null; }
        }

        static long Stamp() => System.Diagnostics.Stopwatch.GetTimestamp();
        static void Took(Site s, long t0, long t1, long t2) { s.Timed++; s.OldTicks += t1 - t0; s.NewTicks += t2 - t1; }
        static void Differ(Site s, string what)
        {
            s.Diff++;
            if (s.FirstDiff == null) s.FirstDiff = what;
            if (s.Diff <= 5) Debug.LogWarning("[OpenZ view scans] DIFFERENT " + s.Name + ": " + what);
        }
        static string Nm(Object o) => o ? o.name + " #" + o.GetInstanceID() : "none";

        // ------------------------------------------------------------------ the ZodObjects root
        static GameObject zodRoot;

        /// <summary>= GameObject.Find("ZodObjects"): the kept root while it is alive, shown and still so named, else a new search
        /// (one level scene is loaded at a time and holds one; no code makes another at run time).</summary>
        public static GameObject ZodRoot()
        {
            RootSite.Calls++;
            if (!On) return GameObject.Find("ZodObjects");
            if (!Shadow) return RootNew();
            long t0 = Stamp(); var a = GameObject.Find("ZodObjects"); long t1 = Stamp(); var b = RootNew(); long t2 = Stamp();
            Took(RootSite, t0, t1, t2);
            if (a != b) Differ(RootSite, "the search found " + Nm(a) + ", the kept root is " + Nm(b));
            return a;
        }

        static GameObject RootNew()
        {
            if (!zodRoot || !zodRoot.activeInHierarchy || zodRoot.name != "ZodObjects") zodRoot = GameObject.Find("ZodObjects");
            return zodRoot;
        }

        // ------------------------------------------------------------------ registries of components (OzLiveSet<T>: Awake / OnDestroy)
        /// <summary>= Object.FindFirstObjectByType&lt;DayNight&gt;() in a frame's Update / LateUpdate (after the scene's Awake).</summary>
        public static DayNight DayNightNow()
        {
            DaySite.Calls++;
            if (!On) return Object.FindFirstObjectByType<DayNight>();
            if (!Shadow) return FirstNew<DayNight>();
            long t0 = Stamp(); var a = Object.FindFirstObjectByType<DayNight>(); long t1 = Stamp(); var b = FirstNew<DayNight>(); long t2 = Stamp();
            Took(DaySite, t0, t1, t2);
            if (a != b) Differ(DaySite, "the search found " + Nm(a) + ", the registry " + Nm(b));
            return a;
        }

        /// <summary>= Object.FindObjectsByType&lt;RockPillar&gt;(FindObjectsSortMode.None).Length.</summary>
        public static int RockPillarCount()
        {
            RockSite.Calls++;
            if (!On) return Object.FindObjectsByType<RockPillar>(FindObjectsSortMode.None).Length;
            if (!Shadow) return CountNew<RockPillar>();
            long t0 = Stamp(); int a = Object.FindObjectsByType<RockPillar>(FindObjectsSortMode.None).Length; long t1 = Stamp(); int b = CountNew<RockPillar>(); long t2 = Stamp();
            Took(RockSite, t0, t1, t2);
            if (a != b) Differ(RockSite, "the search counted " + a + ", the registry " + b);
            return a;
        }

        static readonly List<VehicleMotion> vmBuf = new List<VehicleMotion>(), vmCheck = new List<VehicleMotion>();
        static readonly HashSet<VehicleMotion> vmSet = new HashSet<VehicleMotion>();

        /// <summary>The same VehicleMotions as Object.FindObjectsByType&lt;VehicleMotion&gt;(FindObjectsSortMode.None), in no kept order
        /// (that search keeps none either; OzTreadSteady treats each vehicle on its own). A kept list: read it before the next call.</summary>
        public static List<VehicleMotion> VehicleMotions()
        {
            TreadSite.Calls++;
            if (!On) { vmBuf.Clear(); vmBuf.AddRange(Object.FindObjectsByType<VehicleMotion>(FindObjectsSortMode.None)); return vmBuf; }
            if (!Shadow) { AllNew(vmBuf); return vmBuf; }
            long t0 = Stamp(); var a = Object.FindObjectsByType<VehicleMotion>(FindObjectsSortMode.None); long t1 = Stamp(); AllNew(vmCheck); long t2 = Stamp();
            Took(TreadSite, t0, t1, t2);
            vmSet.Clear(); foreach (var v in vmCheck) vmSet.Add(v);
            bool same = vmSet.Count == vmCheck.Count && vmSet.Count == a.Length;
            if (same) foreach (var v in a) if (!vmSet.Contains(v)) { same = false; break; }
            if (!same) Differ(TreadSite, "the search found " + a.Length + ", the registry " + vmCheck.Count + " (" + vmSet.Count + " distinct)");
            vmBuf.Clear(); vmBuf.AddRange(a);
            return vmBuf;
        }

        // A component the engine's search may treat by its own rules (a hidden or not-saved object) sends the call to the engine itself.
        static bool Plain(Component c) => c.hideFlags == HideFlags.None && c.gameObject.hideFlags == HideFlags.None;

        static T FirstNew<T>() where T : Component
        {
            T one = null; int n = 0; bool dead = false;
            foreach (var c in OzLiveSet<T>.All)
            {
                if (!c) { dead = true; continue; }
                if (!c.gameObject.activeInHierarchy) continue;
                if (!Plain(c) || ++n > 1) { Prune<T>(dead); return Object.FindFirstObjectByType<T>(); }   // two or more shown: the engine picks, as before
                one = c;
            }
            Prune<T>(dead);
            return one;
        }

        static int CountNew<T>() where T : Component
        {
            int n = 0; bool dead = false;
            foreach (var c in OzLiveSet<T>.All)
            {
                if (!c) { dead = true; continue; }
                if (!c.gameObject.activeInHierarchy) continue;
                if (!Plain(c)) { Prune<T>(dead); return Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length; }
                n++;
            }
            Prune<T>(dead);
            return n;
        }

        static void AllNew<T>(List<T> into) where T : Component
        {
            into.Clear(); bool dead = false;
            foreach (var c in OzLiveSet<T>.All)
            {
                if (!c) { dead = true; continue; }
                if (!c.gameObject.activeInHierarchy) continue;
                if (!Plain(c)) { Prune<T>(dead); into.Clear(); into.AddRange(Object.FindObjectsByType<T>(FindObjectsSortMode.None)); return; }
                into.Add(c);
            }
            Prune<T>(dead);
        }

        static class Gone<T> where T : Component { public static readonly System.Predicate<T> Is = c => !c; }
        // An entry whose object went without its OnDestroy (only when the editor keeps statics between two Play runs) leaves the set.
        static void Prune<T>(bool dead) where T : Component { if (dead) OzLiveSet<T>.All.RemoveWhere(Gone<T>.Is); }

        // ------------------------------------------------------------------ the walk decks (ZGame.MakeWalkable, OzUePlatformView.Bind)
        static readonly List<GameObject> walkDecks = new List<GameObject>();
        static readonly List<Transform> deckOrder = new List<Transform>(), deckCheck = new List<Transform>();
        static readonly List<MeshCollider> deckColliders = new List<MeshCollider>();
        static readonly System.Predicate<GameObject> GoneGo = g => !g;

        /// <summary>A walk deck object was made (it carries the MeshCollider ZGame.GroundY rides on).</summary>
        public static void AddWalkDeck(GameObject go) { if (go && !walkDecks.Contains(go)) walkDecks.Add(go); }
        /// <summary>The walk decks made so far (the counted check).</summary>
        public static int WalkDecksKnown => walkDecks.Count;

        /// <summary>Appends exactly what this gave: every MeshCollider under the ZodObjects root on a shown object named WalkDeck, its
        /// transform, in the tree's own order (root.GetComponentsInChildren&lt;MeshCollider&gt;(false), depth first, children in order).</summary>
        public static void WalkDecks(List<Transform> into)
        {
            DeckSite.Calls++;
            if (!On) { WalkOld(into); return; }
            if (!Shadow) { WalkNew(into); return; }
            int at = into.Count;
            long t0 = Stamp(); WalkOld(into); long t1 = Stamp(); deckCheck.Clear(); WalkNew(deckCheck); long t2 = Stamp();
            Took(DeckSite, t0, t1, t2);
            bool same = into.Count - at == deckCheck.Count;
            for (int i = 0; same && i < deckCheck.Count; i++) same = into[at + i] == deckCheck[i];
            if (!same) Differ(DeckSite, "the walk found " + (into.Count - at) + " deck(s), the registry " + deckCheck.Count + (into.Count - at == deckCheck.Count ? " in another order" : ""));
        }

        static void WalkOld(List<Transform> into)
        {
            var root = GameObject.Find("ZodObjects");
            if (root) foreach (var mc in root.GetComponentsInChildren<MeshCollider>(false)) if (mc.name == "WalkDeck") into.Add(mc.transform);
        }

        static void WalkNew(List<Transform> into)
        {
            var root = RootNew(); if (!root) return;
            walkDecks.RemoveAll(GoneGo);                                                                   // an entry whose object went
            DecksUnder(root.transform, walkDecks, into);
        }

        /// <summary>The registry's answer for a root: of the deck objects given, those alive, shown, still named WalkDeck and under the root,
        /// in the order of a depth-first walk from it, once for each MeshCollider on them. Public for the counted check (order on random trees).</summary>
        public static void DecksUnder(Transform rt, List<GameObject> decks, List<Transform> into)
        {
            deckOrder.Clear();
            for (int i = 0; i < decks.Count; i++)
            {
                var go = decks[i];
                if (!go || !go.activeInHierarchy || go.name != "WalkDeck" || !go.transform.IsChildOf(rt)) continue;
                deckOrder.Add(go.transform);
            }
            if (deckOrder.Count > 1) { TreeOrder.Root = rt; deckOrder.Sort(TreeOrder.I); TreeOrder.Root = null; }
            for (int i = 0; i < deckOrder.Count; i++)
            {
                deckOrder[i].GetComponents(deckColliders);
                for (int k = 0; k < deckColliders.Count; k++) into.Add(deckOrder[i]);           // one entry per collider, as the walk gave
            }
            deckColliders.Clear(); deckOrder.Clear();
        }

        /// <summary>The order of a depth-first walk from Root: by the sibling indices from Root down, an ancestor before its children.</summary>
        public sealed class TreeOrder : IComparer<Transform>
        {
            public static readonly TreeOrder I = new TreeOrder();
            public static Transform Root;
            static readonly List<int> pa = new List<int>(16), pb = new List<int>(16);
            static void Path(Transform t, List<int> p) { p.Clear(); for (var x = t; x && x != Root; x = x.parent) p.Add(x.GetSiblingIndex()); p.Reverse(); }
            public int Compare(Transform a, Transform b)
            {
                if (a == b) return 0;
                Path(a, pa); Path(b, pb);
                int n = pa.Count < pb.Count ? pa.Count : pb.Count;
                for (int i = 0; i < n; i++) if (pa[i] != pb[i]) return pa[i] < pb[i] ? -1 : 1;
                return pa.Count.CompareTo(pb.Count);
            }
        }

        // ------------------------------------------------------------------ BridgeMarkings
        /// <summary>BridgeMarkings.Scan: true = nothing to search for (every deck already has its plate: the old search found the root and
        /// then skipped every deck). In Shadow it times one search and lets the old path run.</summary>
        public static bool SkipPlateSearch(bool everyDeckHasItsPlate)
        {
            PlateSite.Calls++;
            if (!On || !everyDeckHasItsPlate) return false;
            if (!Shadow) return true;
            long t0 = Stamp(); GameObject.Find("ZodObjects"); long t1 = Stamp();
            Took(PlateSite, t0, t1, t1);
            return false;
        }
    }
}
