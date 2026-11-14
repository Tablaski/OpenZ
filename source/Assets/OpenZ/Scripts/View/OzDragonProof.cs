using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>DV163: what the level 7 dragons' Play checks read (Editor/OzDragonPlayCheck.cs). Plain counts and the drawn numbers of each order and bite, written by the view at each event; nothing reads them in a game.</summary>
    public static class OzDragonProof
    {
        public struct Order { public double t; public int dragon, target; public string type; public bool ship; public float along, side, turnDeg; public Vector3 targetVp, fireVp; }
        public struct Bite { public float deg, mouthUp, endUp; public bool hit; public Vector3 mouth, to; }

        /// <summary>A check is recording (gaps measured each frame).</summary>
        public static bool Recording;
        /// <summary>A check holds the dragons' own looks while it stages something.</summary>
        public static bool NoAsks;
        public static int Encounters, OffScreen, NoPath, IntoLava, ClimbAways, ShieldHeld;
        public static float MinGap = 999f;
        public static readonly int[] Answers = new int[ZWorld.DragonWhyCount];
        public static readonly List<Order> Orders = new List<Order>();
        public static readonly List<Bite> Bites = new List<Bite>();
        public static readonly Dictionary<string, int> Shots = new Dictionary<string, int>(), Undrawn = new Dictionary<string, int>();
        public static readonly int[] Kinds = new int[16];

        public static void Reset()
        {
            Encounters = OffScreen = NoPath = IntoLava = ClimbAways = ShieldHeld = 0; MinGap = 999f;
            System.Array.Clear(Answers, 0, Answers.Length); System.Array.Clear(Kinds, 0, Kinds.Length);
            Orders.Clear(); Bites.Clear(); Shots.Clear(); Undrawn.Clear();
        }

        public static void NoteGap(float g) { if (g < MinGap) MinGap = g; }
        public static void NoteAnswer(int why) { if (why >= 0 && why < Answers.Length) Answers[why]++; }
        public static void NoteEvent(ZDragonKind k, ZEvent e) { Kinds[(int)k]++; }
        public static void NoteOrder(double t, int dragon, ZObject target, float along, float side, float turnDeg, Vector3 targetVp, Vector3 fireVp)
        {
            Orders.Add(new Order { t = t, dragon = dragon, target = target.Id, type = target.Type, ship = target.IsShuttle, along = along, side = side, turnDeg = turnDeg, targetVp = targetVp, fireVp = fireVp });
        }
        public static void NoteBite(float deg, bool hit, float mouthUp, float endUp, Vector3 mouth, Vector3 to)
        {
            if (Bites.Count < 512) Bites.Add(new Bite { deg = deg, hit = hit, mouthUp = mouthUp, endUp = endUp, mouth = mouth, to = to });
        }
        public static void NoteShot(string type, bool drawn)
        {
            var d = drawn ? Shots : Undrawn;
            d.TryGetValue(type, out int n); d[type] = n + 1;
        }
    }
}
