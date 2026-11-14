// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: its row on the testing panel (Alt+P): on / off for this match, the three styles, the planet, cover again.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR on the testing panel (UI/OzCheatSpawner.cs, Alt+P; that file is not changed): a small plate under its EMPTY ·
    /// NEUTRAL row, shown and hidden with the panel. Buttons: FOG ON / FOG OFF (this match only; the OPTIONS row is not changed; refused in the
    /// Unit editor, in 'Zod exact' and in a computer match), the styles CLOUD SEA, CLOUD BANKS, MIST LAYERS (session only; the OPTIONS row
    /// FOG LOOK is not changed), PLANET (AUTO, DESERT, JUNGLE, CITY, VOLCANO: every palette on one map, session only) and COVER AGAIN (the
    /// map covered, the units' sight opening again: to show the reveal), and (1.4, 5 Nov) COLOUR (TODAY, LAND, AIR, DUSK) and COVER (100,
    /// 85, 70, 55 %: the deep fog's see-through), session only. The plate blocks the mouse like the panel's own columns.
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        RectTransform panel; OzText panelState, panelCost; float panelNextText;
        readonly List<RectTransform> panelBtns = new List<RectTransform>(6);
        readonly List<Image> panelBgs = new List<Image>(6);
        readonly List<OzText> panelLabels = new List<OzText>(6);
        const float BtnW = 96f, BtnH = 30f, BtnGap = 8f, PPad = 12f, PHead = 22f;
        static readonly string[] BtnNames = { "FOG ON", "CLOUD SEA", "CLOUD BANKS", "MIST LAYERS", "PLANET AUTO", "COVER AGAIN", "COLOUR TODAY", "COVER 100 %" };

        /// <summary>
        /// drawn (false: never built, so no gap and nothing blocks the mouse). The session settings, PanelAct and the fog's own code stay (checks
        /// use them); a check may set it true for its own run.</summary>
        public static bool PanelDrawn = false;

        void PanelStep()
        {
            bool want = PanelDrawn && OzCheatSpawner.Visible && !OzUeHooks.NoCheatPanel;
            if (!want) { if (panel && panel.gameObject.activeSelf) panel.gameObject.SetActive(false); return; }
            if (!panel && !PanelBuild()) return;
            if (!panel.gameObject.activeSelf) { panel.gameObject.SetActive(true); panelNextText = 0f; }
            if (Time.unscaledTime >= panelNextText) { panelNextText = Time.unscaledTime + 0.5f; PanelText(); }
            var hud = OpenZHud.I; var m = Mouse.current;
            if (hud == null || m == null || hud.Modal || !m.leftButton.wasPressedThisFrame) return;
            Vector2 mp = m.position.ReadValue();
            for (int i = 0; i < panelBtns.Count; i++)
                if (OpenZHud.Contains(panelBtns[i], mp)) { PanelAct(i); panelNextText = 0f; break; }
        }

        bool PanelBuild()
        {
            var hud = OpenZHud.I; if (hud == null || !hud.Root) return false;
            panelBtns.Clear(); panelBgs.Clear(); panelLabels.Clear();                                       // a rebuilt HUD: the old plate went with it
            float w = PPad * 2f + BtnNames.Length * BtnW + (BtnNames.Length - 1) * BtnGap, h = PPad + PHead + BtnH + 8f + 14f + PPad * 0.5f;
            panel = Hud.Node("CheatFogOfWar", hud.Root);
            panel.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(w, h));   // under the testing panel's EMPTY · NEUTRAL row
            Hud.Plate(panel, "md", PHead + 6f, OzUi.Amber);
            Hud.Label(panel, "Title", "FOG OF WAR · TESTING", 12, OzUi.Ink, 0.16f).At(PPad, 8);
            panelState = Hud.Label(panel, "State", "", 10, OzUi.AmberHi, 0.1f, OzStyle.Plain).At(w - PPad, 10, OzH.R, false, 260);
            for (int i = 0; i < BtnNames.Length; i++)
            {
                var cell = Hud.Node("Fow_" + i, panel).TL(PPad + i * (BtnW + BtnGap), PPad + PHead, BtnW, BtnH);
                var bg = Hud.Img(cell, "Bg", OzUi.A(OzUi.GM950, 0.75f)); bg.rectTransform.TL(0, 0, BtnW, BtnH);
                Hud.Border(bg.transform, OzUi.A(OzUi.Amber, 0.55f));
                var lb = Hud.Label(cell, "Name", BtnNames[i], 10, OzUi.Ink, 0.08f).At(BtnW * 0.5f, BtnH * 0.5f, OzH.C, true, BtnW - 6f);
                panelBtns.Add(cell); panelBgs.Add(bg); panelLabels.Add(lb);
            }
            panelCost = Hud.Label(panel, "Cost", "", 9, OzUi.Muted, 0.06f, OzStyle.Plain).At(PPad, PPad + PHead + BtnH + 6f, OzH.L, false, w - 2f * PPad);
            hud.AddBlocker(panel);
            return true;
        }

        /// <summary>1.4 MAYHEM: the end screen's line for a MAYHEM match in which the testing panel was used (it can switch the fog off
        /// or thin its cover): MAYHEM's own table takes only matches played under the fog. The testing overlay's own words (its spawns).</summary>
        public const string PanelUnranked = "TESTING OVERLAY USED · NOT IN THE TABLE";
        /// <summary>1.4 MAYHEM: the testing panel changed a session setting of the fog in this Play session (a style, PLANET, COLOUR or
        /// COVER: they stay for every later match, whatever their value): a MAYHEM match that starts after that is not in MAYHEM's table
        /// either (Decide). No fixed number: it does not depend on the shipped cover. Reset with the fog's other session settings.</summary>
        static bool panelSession;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void PanelSessionReset() => panelSession = false;

        void PanelAct(int i)
        {
            AudioDirector.UI("ui_click");
            if (game && game.IsMayhem && i != 5) OzHighScores.Unranked = PanelUnranked;                 // 1.4 MAYHEM: every button but COVER AGAIN (that one only covers the map again)
            if (i != 0 && i != 5) panelSession = true;                                                    // 1.4 MAYHEM: a session setting (style, PLANET, COLOUR, COVER): later MAYHEM matches are not in the table either (Decide)
            if (i == 0)
            {
                if (!SetForced(!wanted, out var refused) && OpenZHud.I) OpenZHud.I.Toast("FOG OF WAR IS NEVER ON HERE: " + refused.ToUpperInvariant(), HudStyle.Danger);
                return;
            }
            if (i >= 1 && i <= 3) { SessionLook = i - 1; return; }                                     // session only: the next level start keeps it, OPTIONS FOG LOOK leads again when changed
            if (i == 4) { PlanetOverride = planetOverride >= Palettes.Length - 1 ? -1 : planetOverride + 1; return; }      // AUTO, DESERT, JUNGLE, CITY, VOLCANO, ARCTIC (1.4 fog_biome_looks)
            if (i == 5) CoverAgain();
            if (i == 6) { panelColour = true; ColourIdea = colourIdea + 1; }                             // 1.4: TODAY, LAND, AIR, DUSK (session only; leads in MAYHEM too, fog_biome_looks)
            if (i == 7) { panelCover = true; Cover = CoverLevels[(CoverStep() + 1) % CoverLevels.Length]; }   // 1.4: 100, 85, 70, 55 % (session only; MAYHEM's easing stops for the session)
        }

        /// <summary>The see-through level nearest the cover on screen.</summary>
        static int CoverStep()
        {
            int b = 0;
            for (int i = 1; i < CoverLevels.Length; i++) if (Mathf.Abs(CoverLevels[i] - cover) < Mathf.Abs(CoverLevels[b] - cover)) b = i;
            return b;
        }

        void PanelText()
        {
            if (!panel) return;
            panelLabels[0].Set(wanted ? "FOG OFF" : "FOG ON");
            for (int i = 0; i < 3; i++) panelBgs[i + 1].color = OzUi.A(i == look ? OzUi.AmberLo : OzUi.GM950, 0.75f);
            panelLabels[4].Set(planetOverride < 0 ? "PLANET AUTO" : PlanetNames[planetOverride]);
            panelBgs[4].color = OzUi.A(planetOverride >= 0 ? OzUi.AmberLo : OzUi.GM950, 0.75f);
            panelLabels[6].Set("COLOUR " + ColourNames[ColourNow]);
            panelBgs[6].color = OzUi.A(colourIdea != ColourToday ? OzUi.AmberLo : OzUi.GM950, 0.75f);
            panelLabels[7].Set($"COVER {CoverNow * 100f:0} %");
            panelBgs[7].color = OzUi.A(cover < 0.999f ? OzUi.AmberLo : OzUi.GM950, 0.75f);
            string src = forceSession > 0 ? "testing panel" : forceSession < 0 ? "testing panel" : "MAYHEM";   // 1.4 MAYHEM: the fog of war is MAYHEM's
            panelState.Set(wanted ? $"ON ({src}) · {LookName} · {PlanetName.Replace(" (testing panel)", "")}" : "OFF · " + (why ?? ""));
            panelCost.Set(wanted ? $"cost {CostMs()[5]:0.000} ms a frame · explored {(mask != null ? mask.ExploredShare() * 100f : 0f):0} % · hidden units {HiddenViews} · colour {ColourNames[ColourNow]} · cover {CoverNow * 100f:0} %{(BiomeEases ? $" (now {CoverNow * 100f:0.0} %) easing {BiomeLook.CoverMin * 100f:0}-{BiomeLook.CoverMax * 100f:0} ({BiomeLook.Biome})" : "")}"
                                   + (GpuTiming ? $" · GPU {GpuMsField:0.00} + {GpuMsCloud:0.00} + {GpuMsComposite:0.00} ms" : "") : "the reveal map is not running in this match");
        }
    }
}
