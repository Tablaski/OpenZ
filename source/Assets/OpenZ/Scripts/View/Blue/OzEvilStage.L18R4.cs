// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game": ROUND 4 of the blue briefing's corner window (EvilZod's HQ window; the owner's notes
// of 12 Nov evening, kit level18_briefings/kit_r4; rows of kits/level18/OWNER_NOTES_TRACKER_20261112.md):
//  - 8.2 / 17 / 31  the two signs: NO posts. Each sign is mounted on a plain raised BACK BOARD at the stand's back edge (the stand's own dark
//                   painted metal, as wide as the two signs), its lower edge just over the panel behind its own button, leaning back toward
//                   the lens; the buttons stand in FRONT of them and lower. Nothing of the stand is in front of a sign.
//  - 32             a slight warm flash in the binoculars' lenses on each blast (rise 0.05 s, soft fade), on the stage's own flash beats.
//  - 36             head room: no corner picture cuts a head: EvilZod's hat top and BS-01's head top stay at least HeadRoomTop of the picture's
//                   height under its top edge (the lens pulls back along its own line, then aims a little higher; once a cut, never pumping).
//  - 8.1            the binocular close-up is three-quarter on HIM: its heading is taken from his head's own heading at the cut.
// WRITTEN BY READING ONLY: nothing of this file has run in the editor yet. Every number is a static: tuned in Play, no rebuild.
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        // ================================================================================================ 31: the signs on a back board
        /// <summary>Owner addition 31 (third complaint).
        /// SignsOnBoard false = round 3's free-standing plates on posts. The board: its foot's front edge (stand frame z; the panel's back rim
        /// stands at -0.23 .. -0.26), its thickness, its lean back (deg; the signs lean with it), the margin over the signs' top and beside
        /// them (m). The signs' lower edge over the panel (m along the board): over the domes' silhouette from every corner lens (down to 14 deg
        /// from below).</summary>
        public static bool SignsOnBoard = true;
        public static float BoardZ = -0.215f, BoardThick = 0.025f, SignLean = 17f, SignLift = 0.16f, BoardTop = 0.03f, BoardSide = 0.04f, SignFront = 0.004f;
        /// <summary>Play check: the board and the two signs were built (3 wanted), the signs' lower edge over the panel (m).</summary>
        public static int L18BoardPieces; public static float L18SignLowEdge;

        void L18BoardSigns(Material back)
        {
            if (!consoleTopT) return;
            float lean = SignLean * Mathf.Deg2Rad, c = Mathf.Cos(lean), s = Mathf.Sin(lean);
            Vector3 upL = new Vector3(0f, c, -s), nL = new Vector3(0f, s, c);                              // the board's own up and its face's normal (toward the lens), stand frame
            Vector3 foot = new Vector3(0f, ConsoleModelTop + 0.007f, BoardZ);                               // the middle of the board's foot, front edge, on the panel
            float boardH = SignLift + PlateSize + BoardTop, boardW = 2f * (ButtonHalf + PlateSize * 0.5f + BoardSide);
            var rot = Quaternion.Euler(-SignLean, 0f, 0f);
            // the board: the stand's own dark painted metal, flush with its back edge
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube); board.name = "L18SignBoard"; board.layer = Layer; var bc = board.GetComponent<Collider>(); if (bc) Destroy(bc);
            board.transform.SetParent(consoleTopT, false);
            board.transform.localPosition = foot + upL * (boardH * 0.5f) - nL * (BoardThick * 0.5f); board.transform.localRotation = rot; board.transform.localScale = new Vector3(boardW, boardH, BoardThick);
            var br = board.GetComponent<MeshRenderer>(); br.sharedMaterial = back ? back : Lit(new Color32(40, 43, 48, 255), 0.35f, 0.4f);
            br.renderingLayerMask = LightMask; br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false; br.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            l18Plates.Add(board); L18BoardPieces = 1;
            for (int i = 0; i < 2; i++)
            {
                bool nuke = i == 0; string res = nuke ? L18PlateNukeRes : L18PlateCoffeeRes;
                var tex = Resources.Load<Texture2D>(res); var glow = Resources.Load<Texture2D>(res + "_E");
                if (!tex) { Debug.LogError("[EvilZod] level 18: the button plate Resources/" + res + " is missing"); continue; }
                var m = Glow(new Color32(255, 255, 255, 255), Color.white, PlateGlow); if (!m) continue;
                m.SetTexture("_BaseMap", tex); m.SetTexture("_EmissionMap", glow ? glow : tex); m.SetFloat("_Smoothness", 0.3f); m.SetFloat("_Metallic", 0.1f);
                var go = new GameObject(nuke ? "L18PlateNuke" : "L18PlateCoffee") { layer = Layer }; go.transform.SetParent(consoleTopT, false);
                float px = (nuke ? NukeX : -NukeX) - PlateOut;                                               // behind its own button: the trefoil behind the bridge button, the cup behind the coffee one
                go.transform.localPosition = foot + new Vector3(px, 0f, 0f) + upL * (SignLift + PlateSize * 0.5f) + nL * SignFront;
                go.transform.localRotation = rot; go.transform.localScale = new Vector3(PlateSize, PlateSize, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = L18QuadMesh(); var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
                r.renderingLayerMask = LightMask; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                l18Plates.Add(go); L18BoardPieces++;
            }
            L18SignLowEdge = (foot + upL * SignLift).y - ConsoleModelTop;
            Debug.Log($"[EvilZod] level 18: the two signs on the back board ({L18BoardPieces} pieces), lower edge {L18SignLowEdge:F3} m over the panel, the board {boardW:F2} x {boardH:F2} m leaning {SignLean:F0} deg; no posts");
        }

        // ================================================================================================ 32: the blasts in the binoculars' lenses
        /// <summary>Owner addition 32 : on each blast's flash beat a
        /// warm glow comes up in both lenses (LensFlashRise s), dies over about LensFlashTau x 3 s; SLIGHT: the lens picture stays readable.</summary>
        public static float LensFlashK = 0.9f, LensFlashRise = 0.05f, LensFlashTau = 0.22f;
        public static Color LensFlashColor = new Color(1f, 0.52f, 0.22f);
        /// <summary>Play checks: the lens glow now (0 .. LensFlashK x the beat's strength), frames lit by it.</summary>
        public static float L18LensFlashNow; public static int L18LensFlashFrames;
        Material l18LensMat; Color l18LensBase;

        void L18LensKeep(Material glass) { l18LensMat = glass; if (glass && glass.HasProperty("_EmissionColor")) l18LensBase = glass.GetColor("_EmissionColor"); }

        void L18LensFlash()
        {
            if (!l18LensMat) return;
            float u = flashAt >= 0f ? clock - flashAt : 99f;
            float k = u >= 0f && u < 2f ? LensFlashK * flashK * Mathf.Clamp01(u / Mathf.Max(0.005f, LensFlashRise)) * Mathf.Exp(-u / Mathf.Max(0.02f, LensFlashTau)) : 0f;
            L18LensFlashNow = k; if (k > 0.05f) L18LensFlashFrames++;
            l18LensMat.SetVector("_EmissionColor", (Vector4)(l18LensBase + LensFlashColor * k));
        }

        // ================================================================================================ 8.1: the binocular close-up three-quarter on him
        /// <summary>Owner 8.1 (the close shot at 0:03-0:04 was head-on): ez_binoc's heading = his head's heading at the cut + BinocYaw (his
        /// three-quarter from BS-01's side), held for the cut (no swim with the sweep). The heading taken is kept within +- BinocFollowMax.</summary>
        public static bool BinocFollowHead = false; public static float BinocFollowMax = 60f;
        /// <summary>Play check: his head's heading taken at the last binocular cut (deg, stage frame).</summary>
        public static float L18BinocHeadYaw, L18BinocHeadRaw, L18BinocBodyYaw;
        float binocT0 = -99f, binocBase;

        float L18BinocYaw()
        {
            if (!BinocFollowHead) return BinocYaw;
            if (camT0 != binocT0)
            {
                binocT0 = camT0; binocBase = 0f;
                if (ez != null && ez.Ok)
                {
                    var r = ez.Rig;
                    Vector3 fwdW = r.HeadFront && r.Head ? Vector3.ProjectOnPlane(r.HeadFront.position - r.Head.position, Vector3.up) : r.Fwd;
                    Vector3 fl = transform.InverseTransformDirection(fwdW); fl.y = 0f;
                    Vector3 fb = transform.InverseTransformDirection(r.Fwd); fb.y = 0f;
                    float head = Mathf.Atan2(fl.x, fl.z) * Mathf.Rad2Deg, body = fb.sqrMagnitude > 1e-6f ? Mathf.Atan2(fb.x, fb.z) * Mathf.Rad2Deg : head;
                    L18BinocHeadRaw = head; L18BinocBodyYaw = body;
                    if (Mathf.Abs(Mathf.DeltaAngle(head, body)) > 90f) head = Mathf.DeltaAngle(0f, head + 180f);
                    if (fl.sqrMagnitude > 1e-6f) binocBase = Mathf.Clamp(head, -BinocFollowMax, BinocFollowMax);
                }
                L18BinocHeadYaw = binocBase;
            }
            return binocBase + BinocYaw;
        }

        // ================================================================================================ 36: head room in every corner picture
        /// <summary>Owner addition 36 : in every picture of the level 18 window EvilZod's hat top and
        /// BS-01's head top (when they are inside the picture's width) stay at least HeadRoomTop of the picture's height under its top edge
        /// (+ HeadRoomSafety for the hand-held sway, the push-in HeadRoomPush). Missing room: the lens pulls back along its own line (x 1.03 a
        /// step, to HeadRoomMaxPull), then aims higher (2 cm a step, to HeadRoomMaxLift m). Worked out once a cut and only ever more (no pumping).</summary>
        public static bool HeadRoomGuard = true;
        public static float HeadRoomTop = 0.06f, HeadRoomSafety = 0.025f, HeadRoomPush = 0.09f, HeadRoomMaxPull = 1.6f, HeadRoomMaxLift = 0.35f;
        /// <summary>Play checks: the last cut's pull-back and lift, the worst head top in it (share of the picture's height under its top edge).</summary>
        public static float L18HeadRoomPull = 1f, L18HeadRoomLift, L18HeadRoomWorst = 1f; public static string L18HeadRoomShot = "";
        float hrT0 = -99f, hrMul = 1f, hrLift; string hrName;

        Framing L18HeadRoom(string n, Framing f)
        {
            if (!HeadRoomGuard || !cam || !l18On || n == "ez_binoc") return f;
            hatK = EzHeight / 2.3f;
            if (camT0 != hrT0 || n != hrName) { hrT0 = camT0; hrName = n; hrMul = 1f; hrLift = 0f; }
            EzHead(out var ezTop, out _); SkPoints(out var skTop, out _, out _, out _);
            float aspect = cam.aspect > 0.1f ? cam.aspect : 1.333f, room = HeadRoomTop + HeadRoomSafety;
            for (int i = 0; i < 40; i++)
            {
                float worst = HeadsRoom(f, hrMul, hrLift, ezTop, skTop, aspect);
                if (worst >= room) break;
                if (n != "ez_binoc" && hrMul < HeadRoomMaxPull) hrMul = Mathf.Min(HeadRoomMaxPull, hrMul * 1.03f);
                else if (hrLift < HeadRoomMaxLift) hrLift = Mathf.Min(HeadRoomMaxLift, hrLift + 0.02f);
                else break;
            }
            f.dist *= hrMul; f.look.y += hrLift;
            L18HeadRoomPull = hrMul; L18HeadRoomLift = hrLift; L18HeadRoomShot = n; L18HeadRoomWorst = HeadsRoom(f, 1f, 0f, ezTop, skTop, aspect);
            return f;
        }

        /// <summary>fixes_r5 (36): the hat's outline round the crown point, measured on EvilZod.fbx (proof/hat_measure.txt): (reach round the
        /// socket, depth under it) in metres at his 2.3 m height; 8 points a ring. False = the crown point alone (round 4).</summary>
        public static bool HeadRoomHatRings = true;
        static readonly float[] HatRings = { 0.17f, 0.05f, 0.24f, 0.10f, 0.30f, 0.17f };
        static float hatK = 1f;
        static readonly Vector3[] hatPts = new Vector3[2 + 3 * 8];
        static Vector3[] HatPoints(Vector3 crown, Vector3 other)
        {
            hatPts[0] = crown; hatPts[1] = other; int n = 2;
            for (int r = 0; r < HatRings.Length; r += 2)
                for (int a = 0; a < 8; a++)
                {
                    float ang = a * Mathf.PI * 0.25f, rad = HeadRoomHatRings ? HatRings[r] * hatK : 0f;
                    hatPts[n++] = crown + new Vector3(Mathf.Cos(ang) * rad, -(HeadRoomHatRings ? HatRings[r + 1] * hatK : 0f), Mathf.Sin(ang) * rad);
                }
            return hatPts;
        }

        /// <summary>The least room over a head top in the picture this framing makes (share of the picture's height between the head top and
        /// the top edge; 1 = no head in the picture's width), stage frame, with the push-in at its most.</summary>
        static float HeadsRoom(Framing f, float mul, float lift, Vector3 top1, Vector3 top2, float aspect)
        {
            Vector3 look = f.look + new Vector3(0f, lift, 0f);
            float el = f.elev * Mathf.Deg2Rad, yw = f.yaw * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(yw) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(yw) * Mathf.Cos(el));
            Vector3 pos = look + dir * (f.dist * mul * (1f - HeadRoomPush)); if (pos.y < 0.28f) pos.y = 0.28f;
            Vector3 fwd = (look - pos).normalized, right = Vector3.Cross(Vector3.up, fwd).normalized, up = Vector3.Cross(fwd, right);
            float ty = Mathf.Tan(Mathf.Max(1f, f.fov) * 0.5f * Mathf.Deg2Rad), tx = ty * aspect, worst = 1f;
            foreach (var p in HatPoints(top1, top2))                                                   // fixes_r5 (36): the crown, the hat's rings and the brim
            {
                Vector3 v = p - pos; float z = Vector3.Dot(v, fwd); if (z < 0.05f) continue;
                float x = Vector3.Dot(v, right) / z / tx, y = Vector3.Dot(v, up) / z / ty;
                if (Mathf.Abs(x) > 1f) continue;                                                          // beside the picture: not in it
                worst = Mathf.Min(worst, (1f - y) * 0.5f);                                               // 0 = on the top edge, below 0 = cut
            }
            return worst;
        }
    }
}
