// OpenZ (GPL-3.0) - version 1.2, the evil HQ: the wall behind BS-01 dressed as a military base (OzEvilStage).
// Owner item 178.
// (Item 147 before it: dim lights, HQ blueprints, a map
// with offensive blue arrows, not too bright, not too distracting.) BS-01's shots look down at him from above (+14-17 deg) from EvilZod's
// side of the lens: behind him the frame shows the floor and the back wall up to about 1.3 m, from the doors to x 4.6, the console in the
// middle. EvilZod's side of the room (his wall, the banners, the flags, the doors' wall) is not touched. Now, in the bays between the wall
// ribs (x 1.40 .. 5.88; dark, cool steel like the rest of the HQ, lit low, darker and softer than the robots):
//  - riveted steel wall panels with batten straps over their seams, a dark kick plate, a trim at 2.95 m;
//  - the console as a military instrument cabinet: a riveted panel face with toggle switches, two gauges, a dim scope, an access door, a
//    louvre band; a steel hand rail where the bright strip was; small dim amber / dull green indicator lamps (OzEvilStage.SteelTex.cs);
//  - a bundle of three pipes along the wall over the console, rising to the ceiling beside it, flanged and clamped; a junction box with a
//    breaker lever, a pilot lamp and conduits under them;
//  - a heavy bulkhead hatch (cut-corner coaming, riveted leaf, hand wheel, dogs, hinges, a hazard band on its sill and header) in the next bay;
//  - a weapons rack with three rifles and an ammo crate in the last bay, a small dim campaign map (item 147's, blue arrows) in a steel frame
//    over it; the tank and the World-Domination-inator blueprints stay spread on the console's top;
//  - two caged bulkhead lamps on the ribs (warm, dim) instead of the picture lamp, the sconce and the blue floor strip; over the tactical
//    board a steel hood with a dim warm strip instead of the bright bar (OzEvilStage.cs BuildBoardHood).
// Everything is merged per material (OzEvilStage.Deck.cs: a few draws), no shadows cast, no text, no logos.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        static Texture2D texMap, texBpTank, texBpBomb;
        /// <summary>The caged wall lamps' light (intensity, range m), the glow of their bulbs, of the console's scope and of the board hood's
        /// strip, the console lamps' brightest blink, the map's brightness. Live: change, then ApplyMilitaryLook() on the stage.</summary>
        public static float CageLampI = 1.6f, CageLampRange = 1.9f, BulbGlow = 0.8f, ScopeGlow = 0.12f, BoardHoodGlow = 0.45f, ConsoleLampMax = 0.55f, MapTint = 0.6f;
        static readonly Color BulbColor = new Color(1f, 0.72f, 0.42f), ScopeColor = new Color(0.45f, 0.85f, 0.5f), HoodColor = new Color(1f, 0.86f, 0.66f);
        static readonly Color LampAmber = new Color(1f, 0.6f, 0.2f), LampGreen = new Color(0.4f, 0.85f, 0.35f);
        Material matFrame, matDark, matPipe, matWheel, matGun, matOlive, matBulb, matScope, matHoodGlow, mapMat;
        readonly List<Light> cageLights = new List<Light>();

        void EnsureDressingMats()
        {
            if (matFrame) return;
            EnsureSteelMaps();
            matFrame = Lit(new Color32(52, 57, 64, 255), 0.45f, 0.7f); KitName(matFrame, "Frame");             // the wall ribs' steel
            matDark = Lit(new Color32(24, 26, 30, 255), 0.35f, 0.5f); KitName(matDark, "Dark");
            matPipe = Lit(new Color32(58, 64, 60, 255), 0.4f, 0.5f); KitName(matPipe, "Pipes");
            matWheel = Lit(new Color32(92, 34, 28, 255), 0.3f, 0.3f); KitName(matWheel, "Wheel");
            matGun = Lit(new Color32(30, 32, 35, 255), 0.45f, 0.7f); KitName(matGun, "Gunmetal");
            matOlive = Lit(new Color32(62, 66, 44, 255), 0.25f, 0.1f); KitName(matOlive, "Olive");
            matBulb = Glow(new Color32(90, 70, 40, 255), BulbColor, BulbGlow); KitName(matBulb, "Bulbs");
            matScope = Glow(new Color32(6, 14, 10, 255), ScopeColor, ScopeGlow); KitName(matScope, "Scope");
            matHoodGlow = Glow(new Color32(60, 52, 40, 255), HoodColor, BoardHoodGlow); KitName(matHoodGlow, "HoodStrip");
            wallPbrMat = SteelPbr(mapsWall); KitName(wallPbrMat, "Panels");
            consoleMat = SteelPbr(mapsConsole); KitName(consoleMat, "ConsoleFace");
            KitName(HazardMat(), "Hazard");
        }

        void BuildDressing()
        {
            EnsureDressingMats();
            if (!texMap) texMap = DrawMap();
            if (!texBpTank) texBpTank = DrawBlueprint(1);
            if (!texBpBomb) texBpBomb = DrawBlueprint(2);
            mapMat = Paper(texMap, MapTint); KitName(mapMat, "Map");
            BuildWallSkin();
            BuildConduits();
            BuildHatch();
            BuildRack();
            CageLamp(new Vector3(3.90f, 1.60f, -3.22f));                                                  // on the rib between the pipes and the hatch
            CageLamp(new Vector3(5.20f, 1.95f, -3.22f));                                                  // on the rib between the hatch and the rack
            // two blueprints spread on the console's top (the front half, clear of the lamp row)
            var b1 = Quad("BlueprintDeskA", new Vector3(2.02f, 1.025f, -2.80f), new Vector2(0.42f, 0.31f), Paper(texBpTank, 0.7f));
            b1.transform.localRotation = Quaternion.Euler(90f, 8f, 0f);
            var b2 = Quad("BlueprintDeskB", new Vector3(2.70f, 1.027f, -2.78f), new Vector2(0.42f, 0.31f), Paper(texBpBomb, 0.7f));
            b2.transform.localRotation = Quaternion.Euler(90f, -6f, 0f);
            FlushKit();
            ApplyMilitaryLook();
        }

        /// <summary>The live values onto this stage's materials and lamps (also at the end of the build).</summary>
        public void ApplyMilitaryLook()
        {
            TunePbr(deckMat, DeckTint, DeckSheen, DeckBump, 0.35f); TunePbr(gratingMat, DeckTint, DeckSheen, DeckBump, 0.35f);
            TunePbr(wallPbrMat, WallTint, WallSheen, WallBump, 0.32f); TunePbr(consoleMat, WallTint, WallSheen, WallBump, 0.32f);
            foreach (var l in cageLights) if (l) { l.intensity = CageLampI; l.range = CageLampRange; }
            if (matBulb) matBulb.SetVector("_EmissionColor", (Vector4)(BulbColor * BulbGlow));
            if (matScope) matScope.SetVector("_EmissionColor", (Vector4)(ScopeColor * ScopeGlow));
            if (matHoodGlow) matHoodGlow.SetVector("_EmissionColor", (Vector4)(HoodColor * BoardHoodGlow));
            if (mapMat) mapMat.SetColor("_BaseColor", new Color(MapTint, MapTint, MapTint, 1f));
        }

        // ------------------------------------------------------------------------------------------------ the console (called from BuildSet)
        /// <summary>The console in the old box's place and size (x 1.45 .. 3.25, top 1.02 m: the LOSE beacons and the blueprints stand on it):
        /// a riveted cabinet, its instrument face, the top slab, a hand rail, a dim scope, six small indicator lamps in dark bezels.</summary>
        void BuildConsole()
        {
            EnsureDressingMats();
            KitBox(new Vector3(2.35f, 0.5f, -2.95f), new Vector3(1.8f, 1.0f, 0.7f), wallPbrMat, 1.3f);
            KitQuad(new Vector3(2.35f, 0.51f, -2.598f), Vector3.forward, Vector3.left, 1.8f, 0.9f, 1.8f, 0.9f, new Vector2(-3.25f, 0.06f), consoleMat);
            KitBox(new Vector3(2.35f, 1.0f, -2.95f), new Vector3(1.84f, 0.04f, 0.74f), matDark);                 // the top slab
            KitBox(new Vector3(2.35f, 0.03f, -2.592f), new Vector3(1.76f, 0.06f, 0.012f), matDark);              // the kick recess
            KitCyl(new Vector3(1.52f, 0.935f, -2.53f), new Vector3(3.18f, 0.935f, -2.53f), 0.014f, matFrame);    // the hand rail
            foreach (float x in new[] { 1.62f, 3.08f }) KitCyl(new Vector3(x, 0.935f, -2.60f), new Vector3(x, 0.935f, -2.53f), 0.009f, matFrame);
            KitCyl(new Vector3(2.35f, 0.62f, -2.598f), new Vector3(2.35f, 0.62f, -2.590f), 0.104f, matScope);   // the scope (dim)
            for (int i = 0; i < 6; i++)
            {
                float x = 1.75f + i * 0.24f;
                KitBox(new Vector3(x, 1.026f, -3.1f), new Vector3(0.07f, 0.012f, 0.07f), matDark);
                var lm = Glow(new Color32(30, 30, 30, 255), i % 2 == 0 ? LampAmber : LampGreen, 0.3f);
                lamps.Add(lm);
                Box("Lamp" + i, new Vector3(x, 1.034f, -3.1f), new Vector3(0.045f, 0.01f, 0.045f), lm, null, false);
            }
        }

        /// <summary>A console lamp changes now and then (the stage's TickSet picks which): dim amber or a dull green. The same two random draws
        /// as before, so everything else that draws from the shared random numbers (the camera's re-frames) runs as before.</summary>
        void ConsoleLampBlink(Material m)
        {
            bool amber = UnityEngine.Random.value < 0.6f;
            m.SetVector("_EmissionColor", (Vector4)((amber ? LampAmber : LampGreen) * UnityEngine.Random.Range(0.1f, ConsoleLampMax)));
        }

        /// <summary>Over the tactical board (called from BuildSet in the old bright bar's place): a steel hood, a dim warm strip under it,
        /// steel corner plates on the board's frame.</summary>
        void BuildBoardHood()
        {
            EnsureDressingMats();
            KitBox(new Vector3(2.35f, 2.80f, -3.165f), new Vector3(1.62f, 0.06f, 0.16f), matFrame);
            KitBox(new Vector3(2.35f, 2.765f, -3.11f), new Vector3(1.40f, 0.012f, 0.025f), matHoodGlow);
            foreach (float x in new[] { 1.62f, 3.08f })
                foreach (float y in new[] { 1.42f, 2.68f }) KitBox(new Vector3(x, y, -3.215f), new Vector3(0.07f, 0.07f, 0.02f), matFrame);
        }

        // ------------------------------------------------------------------------------------------------ the wall (x 1.40 .. 5.88, its face z -3.30)
        void BuildWallSkin()
        {   // the panels' seams on the ribs (x 2.6, 3.9, 5.2) and at 0.30 / 1.60 / 2.90 m; the ribs stand in front of them
            KitQuad(new Vector3(3.64f, 1.625f, -3.297f), Vector3.forward, Vector3.left, 4.48f, 2.65f, 1.3f, 1.3f, new Vector2(-2.6f, 0.30f), wallPbrMat);
            KitBox(new Vector3(3.64f, 0.15f, -3.288f), new Vector3(4.48f, 0.30f, 0.024f), matDark);              // the kick plate
            KitBox(new Vector3(3.64f, 2.97f, -3.282f), new Vector3(4.48f, 0.05f, 0.036f), matFrame);             // the trim
        }

        /// <summary>Three pipes along the wall over the console (under the board), rising to the ceiling between the console and the hatch's
        /// rib; flanges, clamps; the junction box under the bend with its conduits.</summary>
        void BuildConduits()
        {
            float[] ys = { 1.12f, 1.22f, 1.31f }, rs = { 0.034f, 0.026f, 0.02f }, zs = { -3.152f, -3.165f, -3.172f }, turn = { 3.74f, 3.64f, 3.56f };
            for (int i = 0; i < 3; i++)
            {
                var a = new Vector3(1.40f, ys[i], zs[i]); var c = new Vector3(turn[i], ys[i], zs[i]); var top = new Vector3(turn[i], 5.25f, zs[i]);
                KitCyl(a, c, rs[i], matPipe); KitSph(c, rs[i] * 1.02f, matPipe); KitCyl(c, top, rs[i], matPipe);
                foreach (float x in new[] { 1.43f, 2.35f, 3.2f }) KitCyl(new Vector3(x - 0.012f, ys[i], zs[i]), new Vector3(x + 0.012f, ys[i], zs[i]), rs[i] * 1.35f, matFrame);
                foreach (float y in new[] { 2.4f, 3.9f }) KitCyl(new Vector3(turn[i], y - 0.012f, zs[i]), new Vector3(turn[i], y + 0.012f, zs[i]), rs[i] * 1.35f, matFrame);
            }
            foreach (float x in new[] { 1.75f, 2.95f }) KitBox(new Vector3(x, 1.215f, -3.20f), new Vector3(0.035f, 0.30f, 0.18f), matDark);
            foreach (float y in new[] { 2.0f, 3.3f, 4.6f }) KitBox(new Vector3(3.65f, y, -3.20f), new Vector3(0.30f, 0.035f, 0.18f), matDark);
            // the junction box: its body, its panel door, a small warning plate, the breaker lever, a dim pilot lamp, conduits up and down
            KitBox(new Vector3(3.52f, 0.63f, -3.235f), new Vector3(0.30f, 0.46f, 0.13f), matDark);
            KitBox(new Vector3(3.52f, 0.63f, -3.166f), new Vector3(0.26f, 0.42f, 0.008f), wallPbrMat, 1.3f);
            KitBox(new Vector3(3.52f, 0.80f, -3.160f), new Vector3(0.12f, 0.05f, 0.004f), HazardMat(), 0.2f);
            KitBox(new Vector3(3.695f, 0.62f, -3.20f), new Vector3(0.025f, 0.13f, 0.03f), matFrame, 0f, Quaternion.Euler(0f, 0f, -18f));
            KitSph(new Vector3(3.43f, 0.48f, -3.158f), 0.011f, matBulb);
            KitCyl(new Vector3(3.47f, 0.86f, -3.215f), new Vector3(3.47f, 1.12f, -3.215f), 0.013f, matPipe);
            KitCyl(new Vector3(3.47f, 0.0f, -3.215f), new Vector3(3.47f, 0.40f, -3.215f), 0.013f, matPipe);
        }

        /// <summary>The bulkhead hatch in the bay between the ribs at x 3.9 and 5.2 (x 4.05 .. 5.05, y 0.11 .. 1.71, under the blue banner's hem).</summary>
        void BuildHatch()
        {
            const float hx = 4.55f, hy = 0.91f, fz = -3.255f;
            // the coaming: a heavy frame with cut corners
            foreach (float s in new[] { 1f, -1f })
            {
                KitBox(new Vector3(hx + s * 0.44f, hy, fz), new Vector3(0.12f, 1.24f, 0.09f), matFrame);
                KitBox(new Vector3(hx, hy + s * 0.74f, fz), new Vector3(0.64f, 0.12f, 0.09f), matFrame);
                foreach (float t in new[] { 1f, -1f })
                    KitBox(new Vector3(hx + s * 0.40f, hy + t * 0.70f, fz), new Vector3(0.24f, 0.12f, 0.09f), matFrame, 0f, Quaternion.Euler(0f, 0f, -45f * s * t));
            }
            // the leaf (riveted panels, a strap cross behind the wheel), two stiffeners, the hinges on one side, the dogs round the others
            KitBox(new Vector3(hx, hy, -3.195f), new Vector3(0.78f, 1.38f, 0.05f), wallPbrMat, 1.3f);
            foreach (float s in new[] { 1f, -1f }) KitBox(new Vector3(hx, hy + s * 0.42f, -3.16f), new Vector3(0.66f, 0.045f, 0.02f), matFrame);
            foreach (float s in new[] { 1f, -1f }) KitBox(new Vector3(hx - 0.42f, hy + s * 0.45f, -3.19f), new Vector3(0.07f, 0.17f, 0.05f), matDark);
            foreach (float y in new[] { -0.45f, 0f, 0.45f })
            {
                KitBox(new Vector3(hx + 0.43f, hy + y, -3.168f), new Vector3(0.11f, 0.032f, 0.03f), matDark);
                KitCyl(new Vector3(hx + 0.40f, hy + y, -3.20f), new Vector3(hx + 0.40f, hy + y, -3.15f), 0.012f, matFrame);
            }
            foreach (float s in new[] { 1f, -1f }) KitBox(new Vector3(hx, hy + s * 0.72f, -3.168f), new Vector3(0.032f, 0.11f, 0.03f), matDark);
            // the hand wheel: a rim of 16 segments, three spokes, the hub on its spindle
            var wc = new Vector3(hx, hy, -3.125f);
            for (int k = 0; k < 16; k++)
            {
                float a = k * 22.5f;
                KitBox(wc + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * 0.16f, new Vector3(0.068f, 0.024f, 0.024f), matWheel, 0f, Quaternion.Euler(0f, 0f, a + 90f));
            }
            for (int k = 0; k < 3; k++)
            {
                float a = 90f + k * 120f;
                KitBox(wc + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * 0.08f, new Vector3(0.15f, 0.018f, 0.018f), matWheel, 0f, Quaternion.Euler(0f, 0f, a));
            }
            KitCyl(wc + new Vector3(0f, 0f, -0.03f), wc + new Vector3(0f, 0f, 0.02f), 0.03f, matWheel);
            KitCyl(new Vector3(hx, hy, -3.17f), wc, 0.012f, matFrame);
            // the sill step under it (steel, a thin band of warning stripes along its front), a band on the header
            KitBox(new Vector3(hx, 0.055f, -3.22f), new Vector3(1.12f, 0.11f, 0.16f), matFrame);
            KitBox(new Vector3(hx, 0.06f, -3.138f), new Vector3(1.0f, 0.04f, 0.004f), HazardMat(), 0.2f);
            KitBox(new Vector3(hx, hy + 0.74f, -3.207f), new Vector3(0.56f, 0.06f, 0.008f), HazardMat(), 0.2f);
        }

        /// <summary>The last bay (x 5.26 .. 5.88): a weapons rack with three rifles over an ammo crate, the campaign map framed over it.</summary>
        void BuildRack()
        {
            const float rx = 5.57f;
            KitBox(new Vector3(rx, 0.82f, -3.285f), new Vector3(0.52f, 1.16f, 0.03f), matDark);                  // the back board
            KitBox(new Vector3(rx, 0.30f, -3.215f), new Vector3(0.50f, 0.07f, 0.11f), matFrame);                 // the butt trough
            KitBox(new Vector3(rx, 1.12f, -3.225f), new Vector3(0.50f, 0.035f, 0.09f), matFrame);                // the clip bar
            for (int i = 0; i < 3; i++) Rifle(new Vector3(rx - 0.16f + 0.16f * i, 0.30f, -3.215f));
            KitBox(new Vector3(rx, 0.13f, -3.05f), new Vector3(0.46f, 0.26f, 0.28f), matOlive);                  // the ammo crate
            KitBox(new Vector3(rx, 0.205f, -2.909f), new Vector3(0.46f, 0.012f, 0.004f), matDark);               // its lid's seam
            foreach (float s in new[] { -0.236f, 0.236f }) KitBox(new Vector3(rx + s, 0.17f, -3.05f), new Vector3(0.012f, 0.03f, 0.12f), matDark);
            // the map (item 147: blue arrows into the red), small and dim, in a steel frame with corner plates
            KitBox(new Vector3(rx, 1.84f, -3.288f), new Vector3(0.56f, 0.44f, 0.024f), matFrame);
            KitQuad(new Vector3(rx, 1.84f, -3.272f), Vector3.forward, Vector3.left, 0.50f, 0.375f, 0.50f, 0.375f, new Vector2(-(rx + 0.25f), 1.84f - 0.1875f), mapMat);
            foreach (float s in new[] { 1f, -1f })
                foreach (float t in new[] { 1f, -1f }) KitBox(new Vector3(rx + s * 0.255f, 1.84f + t * 0.195f, -3.268f), new Vector3(0.045f, 0.045f, 0.008f), matDark);
        }

        /// <summary>A rifle standing in the rack, its side to the room (butt down at `butt`): stock, receiver, grip, magazine, hand guard, barrel.</summary>
        void Rifle(Vector3 butt)
        {
            KitBox(butt + new Vector3(0f, 0.11f, 0f), new Vector3(0.07f, 0.22f, 0.035f), matOlive);
            KitBox(butt + new Vector3(0f, 0.37f, 0f), new Vector3(0.05f, 0.30f, 0.04f), matGun);
            KitBox(butt + new Vector3(0.04f, 0.30f, 0f), new Vector3(0.03f, 0.09f, 0.03f), matGun, 0f, Quaternion.Euler(0f, 0f, 15f));
            KitBox(butt + new Vector3(0.055f, 0.45f, 0f), new Vector3(0.035f, 0.13f, 0.025f), matGun, 0f, Quaternion.Euler(0f, 0f, -12f));
            KitBox(butt + new Vector3(0f, 0.62f, 0f), new Vector3(0.042f, 0.20f, 0.04f), matOlive);
            KitBox(butt + new Vector3(0f, 0.83f, 0f), new Vector3(0.016f, 0.22f, 0.016f), matGun);
            KitBox(butt + new Vector3(-0.012f, 0.92f, 0f), new Vector3(0.01f, 0.03f, 0.012f), matGun);
        }

        /// <summary>A caged bulkhead lamp on a rib's face (`at`): wall plate, socket, a dim warm bulb, four cage bars and a cross over it, and
        /// its small warm light 0.22 m out from the wall.</summary>
        void CageLamp(Vector3 at)
        {
            KitBox(at + new Vector3(0f, 0f, 0.012f), new Vector3(0.10f, 0.15f, 0.024f), matDark);
            KitCyl(at + new Vector3(0f, 0f, 0.02f), at + new Vector3(0f, 0f, 0.06f), 0.03f, matFrame);
            KitSph(at + new Vector3(0f, 0f, 0.085f), 0.034f, matBulb);
            for (int k = 0; k < 4; k++)
            {
                float a = (45f + k * 90f) * Mathf.Deg2Rad; var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.045f;
                KitCyl(at + o + new Vector3(0f, 0f, 0.05f), at + o + new Vector3(0f, 0f, 0.13f), 0.0035f, matDark);
            }
            KitBox(at + new Vector3(0f, 0f, 0.132f), new Vector3(0.10f, 0.006f, 0.006f), matDark, 0f, Quaternion.Euler(0f, 0f, 45f));
            KitBox(at + new Vector3(0f, 0f, 0.132f), new Vector3(0.10f, 0.006f, 0.006f), matDark, 0f, Quaternion.Euler(0f, 0f, -45f));
            cageLights.Add(AddLight("CageLamp", LightType.Point, at + new Vector3(0f, 0f, 0.22f), at + new Vector3(0f, 0f, 0.22f), BulbColor, CageLampI, CageLampRange));
        }

        Material Paper(Texture2D tex, float tint)
        {
            var m = Lit(new Color32(255, 255, 255, 255), 0.12f, 0f); if (!m) return null;
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(tint, tint, tint, 1f));
            return m;
        }

        // ------------------------------------------------------------------------------------------------ the pictures (drawn once)
        sealed class Canvas2D
        {
            public readonly int W, H; public readonly Color32[] Px;
            public Canvas2D(int w, int h, Color32 fill) { W = w; H = h; Px = new Color32[w * h]; for (int i = 0; i < Px.Length; i++) Px[i] = fill; }
            public void Set(int x, int y, Color32 c, float a = 1f)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x; var o = Px[i];
                Px[i] = new Color32((byte)(o.r + (c.r - o.r) * a), (byte)(o.g + (c.g - o.g) * a), (byte)(o.b + (c.b - o.b) * a), 255);
            }
            public void Line(float x0, float y0, float x1, float y1, float th, Color32 c, float a = 1f)
            {
                int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - th - 1), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + th + 1);
                int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - th - 1), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + th + 1);
                Vector2 p0 = new Vector2(x0, y0), d = new Vector2(x1 - x0, y1 - y0); float L2 = Mathf.Max(1e-4f, d.sqrMagnitude);
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f); float t = Mathf.Clamp01(Vector2.Dot(p - p0, d) / L2);
                        float dist = (p - (p0 + d * t)).magnitude; float k = Mathf.Clamp01(th * 0.5f + 0.5f - dist);
                        if (k > 0f) Set(x, y, c, a * k);
                    }
            }
            public void Rect(float x0, float y0, float x1, float y1, float th, Color32 c, float a = 1f) { Line(x0, y0, x1, y0, th, c, a); Line(x1, y0, x1, y1, th, c, a); Line(x1, y1, x0, y1, th, c, a); Line(x0, y1, x0, y0, th, c, a); }
            public void Circle(float cx, float cy, float r, float th, Color32 c, float a = 1f)
            {
                int n = Mathf.Max(12, (int)(r * 1.5f)); float px = cx + r, py = cy;
                for (int i = 1; i <= n; i++) { float ang = i * Mathf.PI * 2f / n, nx = cx + Mathf.Cos(ang) * r, ny = cy + Mathf.Sin(ang) * r; Line(px, py, nx, ny, th, c, a); px = nx; py = ny; }
            }
            public void Poly(Vector2[] pts, Color32 c, float a = 1f)
            {
                float minX = 1e9f, maxX = -1e9f, minY = 1e9f, maxY = -1e9f;
                foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                for (int y = (int)minY; y <= (int)maxY; y++)
                    for (int x = (int)minX; x <= (int)maxX; x++)
                    {
                        bool inside = false; float px = x + 0.5f, py = y + 0.5f;
                        for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                            if ((pts[i].y > py) != (pts[j].y > py) && px < (pts[j].x - pts[i].x) * (py - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
                        if (inside) Set(x, y, c, a);
                    }
            }
            public Texture2D Tex(string name)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.DontSave };
                t.SetPixels32(Px); t.Apply(true, false); return t;
            }
        }

        /// <summary>An arrow from a to b (texture pixels): shaft width w, head length hl and width hw.</summary>
        static Vector2[] Arrow(Vector2 a, Vector2 b, float w, float hl, float hw)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x), hb = b - d * hl;
            return new[] { a + n * w * 0.5f, hb + n * w * 0.5f, hb + n * hw * 0.5f, b, hb - n * hw * 0.5f, hb - n * w * 0.5f, a - n * w * 0.5f };
        }

        static Texture2D DrawMap()
        {
            var cv = new Canvas2D(512, 384, new Color32(112, 102, 82, 255));
            for (int y = 0; y < 384; y++)                                                            // contour shading of the terrain
                for (int x = 0; x < 512; x++)
                {
                    float h = Mathf.PerlinNoise(x * 0.012f + 3.1f, y * 0.012f + 7.7f);
                    if (Mathf.Abs(Mathf.Repeat(h * 9f, 1f) - 0.5f) < 0.04f) cv.Set(x, y, new Color32(86, 78, 62, 255), 0.6f);
                }
            var redT = new Color32(132, 62, 56, 255); var blueT = new Color32(62, 82, 128, 255);
            cv.Poly(new[] { new Vector2(330, 20), new Vector2(495, 20), new Vector2(495, 365), new Vector2(345, 365), new Vector2(305, 250), new Vector2(325, 140) }, redT, 0.45f);
            cv.Poly(new[] { new Vector2(17, 20), new Vector2(140, 20), new Vector2(160, 120), new Vector2(130, 250), new Vector2(150, 365), new Vector2(17, 365) }, blueT, 0.45f);
            for (int i = 0; i < 9; i++) { float x0 = 64 * i; cv.Line(x0, 0, x0, 384, 1f, new Color32(96, 88, 72, 255), 0.5f); }
            for (int i = 0; i < 7; i++) { float y0 = 64 * i; cv.Line(0, y0, 512, y0, 1f, new Color32(96, 88, 72, 255), 0.5f); }
            float rx = 250f, ry = 0f;                                                                  // a river winding down the middle
            for (int i = 0; i < 24; i++) { float nx = 240f + 30f * Mathf.Sin(i * 0.55f), ny = (i + 1) * 16f; cv.Line(rx, ry, nx, ny, 5f, new Color32(66, 92, 108, 255)); rx = nx; ry = ny; }
            cv.Rect(392, 172, 432, 212, 4f, new Color32(60, 22, 20, 255)); cv.Poly(new[] { new Vector2(394, 174), new Vector2(430, 174), new Vector2(430, 210), new Vector2(394, 210) }, new Color32(150, 40, 34, 255), 0.85f);   // the red fort
            var arrow = new Color32(46, 92, 196, 255); var edge = new Color32(22, 40, 90, 255);
            foreach (var ab in new[] { (new Vector2(70, 95), new Vector2(360, 160)), (new Vector2(60, 200), new Vector2(380, 196)), (new Vector2(80, 310), new Vector2(355, 235)) })
            {
                var poly = Arrow(ab.Item1, ab.Item2, 20f, 42f, 54f);
                for (int i = 0; i < poly.Length; i++) cv.Line(poly[i].x, poly[i].y, poly[(i + 1) % poly.Length].x, poly[(i + 1) % poly.Length].y, 4f, edge);
                cv.Poly(poly, arrow);
            }
            cv.Rect(3, 3, 509, 381, 6f, new Color32(58, 52, 42, 255));
            foreach (var p in new[] { new Vector2(14, 14), new Vector2(498, 14), new Vector2(14, 370), new Vector2(498, 370) }) cv.Poly(Arrow(p + new Vector2(-5, 0), p + new Vector2(5, 0), 10f, 0.1f, 10f), new Color32(170, 160, 140, 255));   // pins
            return cv.Tex("evil_hq_map");
        }

        static Texture2D DrawBlueprint(int kind)
        {
            var paper = new Color32(30, 62, 118, 255); var grid = new Color32(46, 84, 148, 255); var ink = new Color32(196, 210, 232, 255);
            var cv = new Canvas2D(256, kind == 0 ? 352 : 188, paper);
            int W = cv.W, H = cv.H;
            for (int x = 8; x < W; x += 16) cv.Line(x, 0, x, H, 1f, grid, 0.7f);
            for (int y = 8; y < H; y += 16) cv.Line(0, y, W, y, 1f, grid, 0.7f);
            cv.Rect(6, 6, W - 6, H - 6, 2f, ink, 0.9f);
            if (kind == 0)
            {   // a walker robot: head, body, arms, legs, a circle joint, dimension lines, a title block
                cv.Rect(100, 40, 156, 84, 2f, ink); cv.Circle(116, 60, 6, 1.5f, ink); cv.Circle(140, 60, 6, 1.5f, ink);
                cv.Rect(84, 92, 172, 196, 2f, ink); cv.Line(84, 120, 172, 120, 1.5f, ink, 0.8f);
                cv.Rect(52, 96, 78, 180, 2f, ink); cv.Rect(178, 96, 204, 180, 2f, ink);
                cv.Rect(92, 204, 120, 290, 2f, ink); cv.Rect(136, 204, 164, 290, 2f, ink); cv.Circle(128, 150, 14, 1.5f, ink);
                cv.Line(40, 40, 40, 290, 1f, ink, 0.7f); cv.Line(36, 40, 44, 40, 1f, ink, 0.7f); cv.Line(36, 290, 44, 290, 1f, ink, 0.7f);
                cv.Rect(150, 306, 246, 342, 1.5f, ink, 0.9f); cv.Line(150, 324, 246, 324, 1f, ink, 0.7f);
            }
            else if (kind == 1)
            {   // a tank seen from the side: hull, turret, barrel, road wheels
                cv.Poly(new[] { new Vector2(40, 110), new Vector2(210, 110), new Vector2(226, 126), new Vector2(24, 126) }, paper);
                cv.Line(40, 110, 210, 110, 2f, ink); cv.Line(210, 110, 226, 126, 2f, ink); cv.Line(24, 126, 226, 126, 2f, ink); cv.Line(24, 126, 40, 110, 2f, ink);
                cv.Rect(30, 126, 220, 150, 2f, ink);
                for (int i = 0; i < 6; i++) cv.Circle(48 + i * 31, 150, 10, 1.5f, ink);
                cv.Rect(92, 82, 160, 110, 2f, ink); cv.Line(160, 94, 236, 90, 3f, ink);
                cv.Line(20, 40, 236, 40, 1f, ink, 0.6f); cv.Line(20, 36, 20, 44, 1f, ink, 0.6f); cv.Line(236, 36, 236, 44, 1f, ink, 0.6f);
            }
            else
            {   // the World-Domination-inator: a fat bomb with fins, its section lines
                cv.Circle(118, 94, 46, 2f, ink); cv.Line(118, 48, 206, 60, 2f, ink); cv.Line(118, 140, 206, 128, 2f, ink);
                cv.Line(206, 60, 206, 128, 2f, ink); cv.Line(206, 60, 236, 40, 2f, ink); cv.Line(206, 128, 236, 148, 2f, ink); cv.Line(236, 40, 236, 148, 1.5f, ink, 0.8f);
                cv.Circle(118, 94, 16, 1.5f, ink, 0.8f); cv.Line(150, 50, 150, 138, 1f, ink, 0.6f); cv.Line(178, 55, 178, 133, 1f, ink, 0.6f);
                cv.Line(30, 170, 236, 170, 1f, ink, 0.6f);
            }
            return cv.Tex("evil_hq_blueprint_" + kind);
        }
    }
}
