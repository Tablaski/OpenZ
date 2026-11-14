using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 10: the wrench (repair) key of the command bar.
    ///
    /// What: a key in the bar's own style (HudButton Key, 48 x 44 u, in a tray on a small plate like the bar's, the kit's wrench glyph)
    /// that pops up just above the bar's first key (R) while the selection holds at least one own damaged vehicle that is not already on
    /// its way to a repair station (PlayerInput.RepairCandidates: the filter of the R order, its repair order anywhere in its list counting
    /// as on its way) AND the player owns a repair station (PlayerInput.OwnsRepairStation). A click is the R order itself
    /// (PlayerInput.SendToRepairFromHud = SendToRepair: the damaged
    /// vehicles drive to the nearest own repair station and come back, with that order's toast, order sound and voice); a refused click
    /// gets the order-invalid blip. The tooltip is the touch layout's REPAIR key text. Nothing in the bar moves; the key comes and goes
    /// with a short pop (a fade only with REDUCED MOTION).
    ///
    /// Place: a child of the command bar, so it rides with it (ARRANGE INTERFACE, the entrance and exit slides) and goes with it (MINIMAL
    /// UI, the build menu that switches the lower panels off: no picture, no pointer). It stands on the side of the bar the factory list
    /// opens to (above the bar; under it when the bar was moved near the top of the screen). While the factory list shows on that side the
    /// key steps beside the list (at the same height), or, when a panel stands there, past the list's far edge, so the two never meet; it
    /// keeps clear of the other HUD groups where they stand now (OzHudLayout.BoxNow), the plates that come and go only when nothing else
    /// is free (as the touch keys do).
    /// Hidden: in the touch and gamepad layout (its on-screen REPAIR key does the same), while the HUD is hidden, after the match end.
    /// No sound of its own when it comes or goes: that always follows something with its own sound (a selection, a click, a hit, a flag).
    /// </summary>
    public partial class OpenZHud
    {
        /// <summary>The plate (canvas units), its gap to the bar (the factory list's 12 u), where the key stands on the plate (the plate
        /// stands KeyRX0 - RepairKeyX right of the bar's edge, so the key is right over the bar's R key), the tray (the bar's key trays: 3 u
        /// round the key), the gap beside an open factory list, the glyph.</summary>
        public const float RepairPodW = 64f, RepairPodH = 60f, RepairPodGap = 12f, RepairKeyX = 8f, RepairTrayInset = 5f, RepairBesideGap = 8f, RepairGlyph = 28f;
        /// <summary>Pop in / out (s), the start scale of the pop, how often the repair stations are looked for (s).</summary>
        public const float RepairPopIn = 0.16f, RepairPopOut = 0.12f, RepairPopFrom = 0.7f, RepairStationEvery = 0.2f;

        /// <summary>The tooltip: the touch layout's REPAIR key text (OpenZHud.TouchKeys.cs), with the key it is bound to now.</summary>
        public static string RepairKeyTip() => "REPAIR|Click this key (no need to drag it onto a vehicle): the selected damaged vehicles drive to your repair station, get repaired and come back. The same as pressing " + OzBindings.Cap(OzAction.RepairVehicles) + ".|" + OzBindings.Cap(OzAction.RepairVehicles);

        RectTransform repairPod, repairBody; CanvasGroup repairCg; HudButton repairKey; Image repairGlyph;
        bool repairWant, repairStation; float repairK, repairStationAt = -10f;
        int repairVer = int.MinValue; float repairCw = -1f, repairCh = -1f, repairListH = -1f; bool repairList; Rect repairCanvasRect;
        static readonly Rect[] repairBoxes = new Rect[OzHudLayout.Count];
        static readonly Rect[] repairCands = new Rect[4];
        static readonly int[] repairCandSpot = new int[4];

        /// <summary>Checks: times the key popped in, times it was clicked, since the game started.</summary>
        public static int RepairKeyPops, RepairKeyClicks;
        /// <summary>The key's plate (it holds the key; null before the HUD is built).</summary>
        public RectTransform RepairKeyRect => repairPod;
        public HudButton RepairKeyButton => repairKey;
        public Image RepairKeyGlyph => repairGlyph;
        /// <summary>The rules of the class text want the key now (it may still be popping in or out).</summary>
        public bool RepairKeyWanted => repairWant;
        /// <summary>The key is on screen now: wanted, fully in, and its bar is drawn (not hidden by MINIMAL UI, the build menu or the HUD key).</summary>
        public bool RepairKeyVisible => repairPod && repairWant && repairK >= 0.999f && hudVisible && Canvas && Canvas.enabled
                                        && repairPod.gameObject.activeInHierarchy && !LowerHides(repairPod);
        /// <summary>Where it stands: 0 over the R key, 1 beside the factory list, 2 past the list's far edge, 3 on the bar's other side.</summary>
        public int RepairKeySpot { get; private set; } = -1;
        /// <summary>Its plate on the canvas (canvas units from the top-left corner, y down), as last placed.</summary>
        public Rect RepairKeyCanvasRect => repairCanvasRect;
        /// <summary>The player owned a repair station at the last look (every RepairStationEvery s).</summary>
        public bool RepairStationOwned => repairStation;

        // ================================================================== build (BuildAll, right after the command bar)
        void BuildRepairKey()
        {
            if (!cmdBar) return;
            repairPod = Hud.Node("RepairKey", cmdBar);
            repairPod.Place(new OzRect(OzLayout.KeyRX0 - RepairKeyX, -(RepairPodGap + RepairPodH), RepairPodW, RepairPodH));
            repairCg = repairPod.gameObject.AddComponent<CanvasGroup>(); repairCg.blocksRaycasts = false; repairCg.interactable = false; repairCg.alpha = 0f;
            AddBlocker(repairPod);
            repairBody = Hud.Node("Body", repairPod); repairBody.Fill(); repairBody.pivot = new Vector2(0.5f, 0f);   // stretched with no offsets: the pivot only says where the pop grows from
            Hud.Plate(repairBody, "sm");
            Hud.Kit(repairBody, "Tray", "Panels/well_4", new OzRect(RepairTrayInset, RepairTrayInset, RepairPodW - 2f * RepairTrayInset, RepairPodH - 2f * RepairTrayInset));
            repairKey = HudButton.Make(repairBody, "KeyRepair", null, new Vector2(OzLayout.KeyRW, OzLayout.KeyH), HudButtonKind.Key, OzLayout.KeyLetterSize, 0f);
            repairKey.Rt.Place(new OzRect(RepairKeyX, OzLayout.KeyY, OzLayout.KeyRW, OzLayout.KeyH));
            // the glyph on the key face: centred like the bar's letters (face middle = (h - 4) / 2, kit README: 5 u front skirt)
            repairGlyph = Hud.Icon(repairKey.Content, "Wrench", RepairKeyIcon, (OzLayout.KeyRW - RepairGlyph) * 0.5f, (OzLayout.KeyH - 4f - RepairGlyph) * 0.5f, RepairGlyph, OzUi.Ink);   // 1.2 item 21: the key's own spanner (OpenZHud.RepairKeyIcon.cs)
            repairKey.Tooltip = RepairKeyTip();
            repairKey.OnClick = c => RepairKeyClick();
            repairPod.gameObject.SetActive(false);
        }

        /// <summary>The click: the R order (its own toast and sounds); refused (nothing left to send), the order-invalid blip.</summary>
        void RepairKeyClick()
        {
            RepairKeyClicks++;
            if (!input.SendToRepairFromHud()) AudioDirector.UI("ui_order_invalid");
        }

        // ================================================================== frame (Update, right after the factory list)
        void TickRepairKey()
        {
            if (!repairPod || !input || W == null) return;
            float now = OzMotion.Now;
            if (now >= repairStationAt) { repairStationAt = now + RepairStationEvery; repairStation = input.OwnsRepairStation(); }
            bool want = repairStation && hudVisible && !exited && !W.GameOver && !OzTouch.Driving && input.RepairCandidates() > 0;
            var go = repairPod.gameObject;
            if (want != repairWant)
            {
                repairWant = want;
                if (want) { RepairKeyPops++; repairKey.Tooltip = RepairKeyTip(); repairVer = int.MinValue; go.SetActive(true); }
            }
            if (!go.activeSelf) return;
            PlaceRepairKey();
            float dur = OzMotion.Dur(want ? RepairPopIn : RepairPopOut, true);
            repairK = dur <= 0f ? (want ? 1f : 0f) : Mathf.MoveTowards(repairK, want ? 1f : 0f, Time.unscaledDeltaTime / dur);
            float s = OzMotion.Reduced ? 1f : want ? Mathf.LerpUnclamped(RepairPopFrom, 1f, OzEase.OutBack(repairK)) : Mathf.Lerp(RepairPopFrom + 0.15f, 1f, repairK);
            repairBody.localScale = new Vector3(s, s, 1f);
            repairCg.alpha = want ? OzEase.OutCubic(repairK) : repairK;
            if (!want && repairK <= 0f) go.SetActive(false);
        }

        /// <summary>The key's place (see the class text), worked out again only when the layout, the canvas or the factory list changed.</summary>
        void PlaceRepairKey()
        {
            OzHudLayout.CanvasSize(out float cw, out float ch);
            bool list = factoryShown && factoryPanel;
            float listH = list ? factoryPanel.sizeDelta.y : 0f;
            if (repairVer == OzHudLayout.Version && cw == repairCw && ch == repairCh && list == repairList && listH == repairListH) return;
            repairVer = OzHudLayout.Version; repairCw = cw; repairCh = ch; repairList = list; repairListH = listH;

            var bar = OzHudLayout.BoxNow(OzPanel.Command);                                                // canvas units from the top-left corner, y down
            bool up = !OzHudLayout.Moved(OzPanel.Command) || !FactoryBelow(bar.y, ch);                     // the factory list's side of the bar
            float x0 = bar.x + OzLayout.KeyRX0 - RepairKeyX;
            float yAbove = bar.y - RepairPodGap - RepairPodH, yUnder = bar.yMax + RepairPodGap;
            Rect lr = default;
            if (list) { var fr = FactoryRect(listH); var tl = OzHudLayout.TopLeft(fr, cw, ch); lr = new Rect(tl.x, tl.y, fr.w, fr.h); }
            int n = 0;
            repairCands[n] = new Rect(x0, up ? yAbove : yUnder, RepairPodW, RepairPodH); repairCandSpot[n++] = 0;
            if (list)
            {
                repairCands[n] = new Rect(lr.xMax + RepairBesideGap, up ? yAbove : yUnder, RepairPodW, RepairPodH); repairCandSpot[n++] = 1;
                repairCands[n] = new Rect(x0, up ? lr.yMin - RepairPodGap - RepairPodH : lr.yMax + RepairPodGap, RepairPodW, RepairPodH); repairCandSpot[n++] = 2;
            }
            repairCands[n] = new Rect(x0, up ? yUnder : yAbove, RepairPodW, RepairPodH); repairCandSpot[n++] = 3;
            for (int i = 0; i < OzHudLayout.Count; i++) repairBoxes[i] = OzHudLayout.BoxNow((OzPanel)i);
            int pick = -1;
            for (int pass = 0; pass < 2 && pick < 0; pass++)
                for (int i = 0; i < n && pick < 0; i++)
                    if (RepairSpotFree(repairCands[i], cw, ch, list, lr, pass == 0)) pick = i;
            if (pick < 0) pick = list ? 2 : 0;                                                            // nothing free: past the list (never on it), else over R
            var r = repairCands[pick];
            RepairKeySpot = repairCandSpot[pick]; repairCanvasRect = r;
            repairPod.Place(new OzRect(r.x - bar.x, r.y - bar.y, RepairPodW, RepairPodH));
            repairBody.pivot = new Vector2(0.5f, r.yMax <= bar.y + 0.5f ? 0f : 1f);                    // the pop grows from the side that faces the bar
        }

        /// <summary>Inside the canvas, off the open factory list and off the other HUD groups (all = also off the plates that come and go:
        /// messages, CANNON READY, the transmission window).</summary>
        static bool RepairSpotFree(Rect r, float cw, float ch, bool list, Rect lr, bool all)
        {
            if (r.xMin < 0f || r.yMin < 0f || r.xMax > cw || r.yMax > ch) return false;
            if (list && r.Overlaps(lr)) return false;
            for (int i = 0; i < OzHudLayout.Count; i++)
            {
                var p = (OzPanel)i;
                if (p == OzPanel.Command) continue;
                if (!all && (p == OzPanel.Messages || p == OzPanel.Cannons || p == OzPanel.Comms)) continue;
                if (repairBoxes[i].Overlaps(r)) return false;
            }
            return true;
        }
    }
}
