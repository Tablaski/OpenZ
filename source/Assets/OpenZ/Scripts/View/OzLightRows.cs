// OpenZ (GPL-3.0)
// the processor's cost there is drawing: every renderer is one draw call per pass (the forward pass, the depth-normals pass for the
// every row below removes whole passes or whole renderers; none of them lowers pixels.
// Every row starts on today's look: with no saved value nothing in the game is touched (every Apply returns at its first lines, no object
// is made, no event is hooked), none of them is ever switched on by the game itself, and a row set back puts back exactly the value it
// found. View only: no rule of the game reads any of it (nothing under Scripts/Sim names this file or its keys); what can be clicked,
// hit, blocked or seen through by the rules is the same on every value, because only drawing changes.
//  * SHADOWS             ON (default) / OFF                       oz_lite_shadows 0 / 1   live
//      OFF: the game camera draws no shadows (URP's own camera switch, UniversalAdditionalCameraData.renderShadows, the switch the
//      test mode already uses): no sun, moon or lamp shadow pass and no culling of shadow casters. The value the camera had is put back.
//  * AMBIENT OCCLUSION   ON (default) / OFF                       oz_lite_ao 0 / 1        live
//      OFF: the SSAO renderer feature of the preset in force is switched off (as LOW and the test mode have it), and with it the
//      depth-normals pass that draws every object in view once more for it. A preset change moves the switch to the new preset's
//      renderer (OzQuality.Apply calls ApplyAo); the old one gets its feature back.
//  * TREES AND PLANTS    FULL (default) / LIGHT / LIGHTEST        oz_lite_trees 0 / 1 / 2 live (level 16: the other levels have no plant cells)
//      Through OzL16DrawAhead's own two numbers (its rule is unchanged): LIGHT = the thinned far version of the plant cells from 80 m
//      camera height (110 m today; the normal 70 m view keeps every full plant) and full plants only 16 m past the picture's edge
//      (32 m today); LIGHTEST = the far version at every height (what the level shows today above 116 m: the plant cells far, the
//      rocks, ruins and buildings in view on their second version) with today's 32 m margin kept, so a rock or ruin never changes its
//      version at the picture's edge. OzL16FarShadows, OzL16Outskirts.FarLook / RimFix / Trees read the same numbers, so the far
//      versions keep their shadows and the outskirts follow.
//  * WRECKS AND DEBRIS   FULL (default) / LIGHT                   oz_lite_wrecks 0 / 1    live
//      LIGHT: at most 4 wrecks / 160 drawn parts wait on camera before they sink (WreckSinkGate 12 / 480), and at most 36 broken-up
//      wreck pieces live at once (WreckPieces 72). The sink itself is unchanged (no popping).
//  * LIGHTEST FOR SLOW PCS   OFF / ON: a helper with no saved value of its own. It shows ON only while the four rows above are all on
//      their lightest value; switching it ON sets them so, switching it OFF puts all four back on the default look.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public static class OzLightRows
    {
        // ------------------------------------------------------------------ labels, keys, tips (GRAPHICS tab, after MASS BATTLES: UI/OzSettingTips.cs)
        public const string LightestLabel = "LIGHTEST FOR SLOW PCS";
        public const string ShadowsLabel = "SHADOWS", ShadowsPref = "oz_lite_shadows";
        public const string AoLabel = "AMBIENT OCCLUSION", AoPref = "oz_lite_ao";
        public const string TreesLabel = "TREES AND PLANTS", TreesPref = "oz_lite_trees";
        public const string WrecksLabel = "WRECKS AND DEBRIS", WrecksPref = "oz_lite_wrecks";
        public const string LightestTip = "LIGHTEST FOR SLOW PCS|One click for a slower computer: SHADOWS off, AMBIENT OCCLUSION off, TREES AND PLANTS lightest, WRECKS AND DEBRIS light. Off puts those four rows back on the default look. It has no setting of its own and never changes the rules.|";
        public const string ShadowsTip = "SHADOWS|On (default). Off: no shadows from the sun, the moon or the lamps (it also overrides SUN SHADOW STEPS and NIGHT LIGHT SHADOWS). The biggest saving on a slower processor: every unit part, tree and building is drawn one or more times less per frame. The picture is flatter; units, shots and the rules are the same.|";
        public const string AoTip = "AMBIENT OCCLUSION|On (default): soft dark shading where things meet the ground and each other. Off: everything in view is drawn one time less per frame, a good saving on a slower processor. GRAPHICS QUALITY LOW already has it off.|";
        public const string TreesTip = "TREES AND PLANTS|Level 16, the Jungle. Full (default). Light: the trees switch to their simpler far look sooner when you zoom out. Lightest: always the simpler look (rocks and ruins too), far fewer things to draw. Trees block and fall in the rules exactly as before.|";
        public const string WrecksTip = "WRECKS AND DEBRIS|Full (default): up to 12 burnt wrecks stay on screen before they sink. Light: up to 4, and half as many flying wreck pieces at once. Units, shots and the rules are the same.|";
        public static readonly string[] TreesNames = { "FULL", "LIGHT", "LIGHTEST" }, WrecksNames = { "FULL", "LIGHT" };

        // ------------------------------------------------------------------ the lighter numbers
        /// <summary>TREES AND PLANTS LIGHT: the far versions from this camera height (OzL16DrawAhead.FarAbove, 110 today) and the full
        /// versions this far past the picture's edge (OzL16DrawAhead.Margin, 32 today). LIGHTEST: FarAbove 0 = the far versions always; its
        /// margin stays today's 32 (review: 0 saved next to nothing, the plant cells are far anyway, but let rocks and ruins change their
        /// version right at the picture's edge while scrolling).</summary>
        public const float TreesLightFarAbove = 80f, TreesLightMargin = 16f, TreesLightestFarAbove = 0f, TreesLightestMargin = 32f;
        /// <summary>WRECKS AND DEBRIS LIGHT: wrecks and drawn parts that wait on camera (WreckSinkGate 12 / 480 today).</summary>
        public const int WrecksLightWrecks = 4, WrecksLightParts = 160;

        // ------------------------------------------------------------------ the saved values (read once, -1 = not read yet)
        static int shadows = -1, ao = -1, trees = -1, wrecks = -1;
        static int Read(ref int cache, string key, int max) { if (cache < 0) { int v = PlayerPrefs.GetInt(key, 0); cache = v >= 1 && v <= max ? v : 0; } return cache; }
        static void Save(ref int cache, string key, int v) { cache = v; PlayerPrefs.SetInt(key, v); PlayerPrefs.Save(); }

        /// <summary>SHADOWS: on = the default (oz_lite_shadows 0).</summary>
        public static bool ShadowsOn
        {
            get => Read(ref shadows, ShadowsPref, 1) == 0;
            set { Save(ref shadows, ShadowsPref, value ? 0 : 1); Hook(); ApplyShadows(); Debug.Log($"[OpenZ Options] shadows: {(value ? "ON" : "OFF")} ({ShadowsPref} {shadows}, saved, live)"); }
        }
        /// <summary>AMBIENT OCCLUSION: on = the default (oz_lite_ao 0).</summary>
        public static bool AoOn
        {
            get => Read(ref ao, AoPref, 1) == 0;
            set { Save(ref ao, AoPref, value ? 0 : 1); Hook(); ApplyAo(); Debug.Log($"[OpenZ Options] ambient occlusion: {(value ? "ON" : "OFF")} ({AoPref} {ao}, saved, live)"); }
        }
        /// <summary>TREES AND PLANTS: 0 FULL (default), 1 LIGHT, 2 LIGHTEST.</summary>
        public static int Trees
        {
            get => Read(ref trees, TreesPref, 2);
            set { int v = value == 1 || value == 2 ? value : 0; Save(ref trees, TreesPref, v); Hook(); ApplyTrees(); Debug.Log($"[OpenZ Options] trees and plants: {TreesNames[v]} ({TreesPref} {v}, saved, live)"); }
        }
        public static string TreesName => TreesNames[Trees];
        /// <summary>The row's arrows: FULL -> LIGHT -> LIGHTEST -> FULL (dir &lt; 0 the other way).</summary>
        public static void TreesStep(int dir) => Trees = (Trees + (dir < 0 ? 2 : 1)) % 3;
        /// <summary>WRECKS AND DEBRIS: false = FULL (default), true = LIGHT.</summary>
        public static bool WrecksLight
        {
            get => Read(ref wrecks, WrecksPref, 1) == 1;
            set { Save(ref wrecks, WrecksPref, value ? 1 : 0); Hook(); ApplyWrecks(); Debug.Log($"[OpenZ Options] wrecks and debris: {WrecksName} ({WrecksPref} {wrecks}, saved, live)"); }
        }
        public static string WrecksName => WrecksNames[WrecksLight ? 1 : 0];
        public static void WrecksStep(int dir) => WrecksLight = !WrecksLight;

        /// <summary>The helper switch: on only while all four rows are on their lightest; ON sets them so, OFF sets all four back to the default.</summary>
        public static bool Lightest
        {
            get => !ShadowsOn && !AoOn && Trees == 2 && WrecksLight;
            set
            {
                if (ShadowsOn == value) ShadowsOn = !value;
                if (AoOn == value) AoOn = !value;
                if (Trees != (value ? 2 : 0)) Trees = value ? 2 : 0;
                if (WrecksLight != value) WrecksLight = value;
                Debug.Log($"[OpenZ Options] lightest for slow PCs: {(value ? "ON (the four rows on their lightest)" : "OFF (the four rows back on the default look)")}");
            }
        }
        /// <summary>The checks: read the saved values again (after a check put the player's own back) and put them in force.</summary>
        public static void Reload()
        {
            shadows = ao = trees = wrecks = -1;
            if (!AllDefault) Hook();
            ApplyTrees(); ApplyWrecks(); ApplyAo(); ApplyShadows();
        }
        /// <summary>Every row on its default (nothing of this file is in force).</summary>
        public static bool AllDefault => ShadowsOn && AoOn && Trees == 0 && !WrecksLight;

        // ------------------------------------------------------------------ start, scenes, the end of the game
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            shadows = ao = trees = wrecks = -1; hooked = false;
            shCam = null; shData = null; aoAsset = null; aoOff.Clear(); treesTouched = wrTouched = false; driver = null;
        }

        /// <summary>A saved lighter value is put in force before the first scene; with every row on its default nothing happens here.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (AllDefault) return;
            Hook(); ApplyTrees(); ApplyWrecks(); ApplyAo(); ApplyShadows();
            Debug.Log($"[OpenZ Options] lighter rows in force: {Describe()}");
        }

        /// <summary>The scene and quit events, once, from the first row that leaves its default.</summary>
        static void Hook()
        {
            if (hooked || !Application.isPlaying) return;
            hooked = true;
            SceneManager.sceneLoaded += (s, m) => ApplyShadows();                                         // a new scene has its own game camera
            Application.quitting += RestoreAll;                                                             // the editor: Play stop puts the shared assets back
        }

        /// <summary>One line for the log and the checks.</summary>
        public static string Describe() => $"shadows {(ShadowsOn ? "on" : "OFF")}, ambient occlusion {(AoOn ? "on" : "OFF")}, trees and plants {TreesName}, wrecks and debris {WrecksName}";

        /// <summary>Every value this file changed back to what it found (the game ends, or a check). Safe to call twice.</summary>
        public static void RestoreAll()
        {
            RestoreShadows(); RestoreAo(); RestoreTrees(); RestoreWrecks();
            if (driver) { Object.Destroy(driver.gameObject); driver = null; }
        }

        /// <summary>For the checks: what this file holds changed now (empty = nothing).</summary>
        public static string Touched()
        {
            var l = new List<string>();
            if (shData) l.Add("camera shadows");
            if (aoOff.Count > 0) l.Add(aoOff.Count + " SSAO feature(s)");
            if (treesTouched) l.Add("plant cell distances");
            if (wrTouched) l.Add("wreck caps");
            if (driver) l.Add("driver");
            return string.Join(", ", l);
        }

        // ------------------------------------------------------------------ SHADOWS: the game camera's own switch
        static Camera shCam; static UniversalAdditionalCameraData shData; static bool shWas;
        static OzLightRowsDriver driver;

        /// <summary>The camera that draws the game (the level's or the menu's main camera).</summary>
        static Camera GameCamera() => Camera.main;

        /// <summary>Puts SHADOWS in force on the game camera now (and on the next camera after a scene change), or puts the camera back.
        /// The test mode drops shadows by itself (GameBoot.TestModeRuntime): the row leaves it alone there.</summary>
        public static void ApplyShadows()
        {
            if (!Application.isPlaying) return;
            bool off = !ShadowsOn && !GameBoot.TestMode;
            var cam = off ? GameCamera() : null;
            if (shData != null && shCam != cam) RestoreShadows();
            if (off && !driver)
            {
                var go = new GameObject("OZ_LightRows"); Object.DontDestroyOnLoad(go);
                driver = go.AddComponent<OzLightRowsDriver>();
            }
            else if (!off && driver) { Object.Destroy(driver.gameObject); driver = null; }
            if (!off || !cam || shCam == cam) return;
            var d = cam.GetComponent<UniversalAdditionalCameraData>();
            if (!d) return;                                                                                // a camera without URP data has nothing to switch
            shCam = cam; shData = d; shWas = d.renderShadows;
            d.renderShadows = false;
        }

        static void RestoreShadows()
        {
            if (shData) shData.renderShadows = shWas;                                                      // the value it had (a camera that is gone needs nothing)
            shData = null; shCam = null;
        }

        /// <summary>The driver's look, twice a second while SHADOWS is OFF: a new game camera gets the switch too.</summary>
        internal static void Watch() { if (!ShadowsOn && GameCamera() != shCam) ApplyShadows(); }

        // ------------------------------------------------------------------ AMBIENT OCCLUSION: the preset's SSAO renderer feature
        static UniversalRenderPipelineAsset aoAsset; static readonly List<ScriptableRendererFeature> aoOff = new List<ScriptableRendererFeature>();
        static System.Reflection.FieldInfo rendererList;

        /// <summary>The renderers of a URP asset (the same private list GameBoot's test mode reads).</summary>
        static ScriptableRendererData[] Renderers(UniversalRenderPipelineAsset a)
        {
            if (rendererList == null) rendererList = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return a && rendererList != null ? rendererList.GetValue(a) as ScriptableRendererData[] : null;
        }

        /// <summary>OzQuality.Apply (every preset change, and its start) and the row. With the row ON and nothing switched off before, it
        /// returns at once.</summary>
        public static void ApplyAo()
        {
            if (aoAsset == null && AoOn) return;
            if (!Application.isPlaying) return;                                                            // never on the assets in edit mode
            var cur = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (aoAsset != null && (AoOn || cur != aoAsset)) RestoreAo();
            if (AoOn || GameBoot.TestMode || !cur || aoAsset != null) return;                              // the test mode drops SSAO by itself
            aoAsset = cur;
            var list = Renderers(cur);
            if (list != null)
                foreach (var rd in list)
                    if (rd) foreach (var f in rd.rendererFeatures)
                        if (f && f.isActive && f.GetType().Name == "ScreenSpaceAmbientOcclusion") { f.SetActive(false); aoOff.Add(f); }
        }

        static void RestoreAo()
        {
            foreach (var f in aoOff) if (f) f.SetActive(true);                                             // only the ones this row switched off
            aoOff.Clear(); aoAsset = null;
        }

        // ------------------------------------------------------------------ TREES AND PLANTS: OzL16DrawAhead's two numbers
        static bool treesTouched; static float margin0, farAbove0;

        public static void ApplyTrees()
        {
            int t = Trees;
            if (t == 0) { RestoreTrees(); return; }
            if (!treesTouched) { margin0 = OzL16DrawAhead.Margin; farAbove0 = OzL16DrawAhead.FarAbove; treesTouched = true; }
            OzL16DrawAhead.Margin = Mathf.Min(margin0, t == 1 ? TreesLightMargin : TreesLightestMargin);
            OzL16DrawAhead.FarAbove = Mathf.Min(farAbove0, t == 1 ? TreesLightFarAbove : TreesLightestFarAbove);
        }

        static void RestoreTrees()
        {
            if (!treesTouched) return;
            OzL16DrawAhead.Margin = margin0; OzL16DrawAhead.FarAbove = farAbove0; treesTouched = false;
        }

        // ------------------------------------------------------------------ WRECKS AND DEBRIS: the wreck caps
        static bool wrTouched; static int capW0, capR0;

        public static void ApplyWrecks()
        {
            if (!WrecksLight) { RestoreWrecks(); return; }
            if (!wrTouched) { capW0 = WreckSinkGate.CapWrecks; capR0 = WreckSinkGate.CapRenderers; wrTouched = true; }
            WreckSinkGate.CapWrecks = Mathf.Min(capW0, WrecksLightWrecks); WreckSinkGate.CapRenderers = Mathf.Min(capR0, WrecksLightParts);
            WreckPieces.TrimLive();                                                                        // pieces past the lighter cap leave the way the cap lets them go
        }

        static void RestoreWrecks()
        {
            if (!wrTouched) return;
            WreckSinkGate.CapWrecks = capW0; WreckSinkGate.CapRenderers = capR0; wrTouched = false;
        }
    }

    /// <summary>Lives only while SHADOWS is OFF: twice a second it makes sure the game camera of the scene in force has the switch.</summary>
    public sealed class OzLightRowsDriver : MonoBehaviour
    {
        float next;
        void LateUpdate()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            OzLightRows.Watch();
        }
    }
}
