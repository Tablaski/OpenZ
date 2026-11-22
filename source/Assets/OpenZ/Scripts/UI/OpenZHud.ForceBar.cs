using System;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    public partial class OpenZHud
    {
        Image forceRedShare, forceBlueShare, forceCursor, forceRedGlow, forceBlueGlow;
        OzText forceRedBonus, forceBlueBonus;
        OzText matchPing, matchSync, matchRelay;
        RectTransform powerRow, powerRedShare, powerBlueShare;
        OzText powerRedText, powerBlueText;
        HudButton forceRedHot, forceBlueHot;
        double forceRedBoost, forceBlueBoost;
        bool forceValueMode;
        float forceShare = .5f;
        int skirmishSeatCount;
        readonly OzText[] skirmishSeats = new OzText[8], skirmishUnits = new OzText[8], skirmishPower = new OzText[8];
        readonly OzText[] skirmishPings = new OzText[8];
        readonly Image[,] skirmishSignals = new Image[8, 3];
        bool skirmishNetwork;
        readonly RectTransform[] skirmishSegments = new RectTransform[8];
        readonly double[] skirmishShares = new double[8], skirmishScores = new double[8];
        RectTransform skirmishUnitRow, skirmishPowerRow, skirmishRail;
        OzText skirmishClock, skirmishStatus, skirmishTitle;

        void BuildSkirmishTopBar()
        {
            top = Hud.Node("ArmyBalance", Root).Place(OzHudLayout.At(OzPanel.Status, new OzRect(OzAnchor.TC, 495, 8, 930, 122)));
            Hud.Plate(top, "md");
            AddBlocker(top);
            skirmishTitle = Hud.Label(top, "BalanceTitle", "", 16, OzUi.Amber).At(16, 17, OzH.L, true, 425);
            skirmishClock = Hud.Label(top, "Clock", "0:00:00", 24, OzUi.Amber).At(470, 17, OzH.C, true, 170);
            skirmishStatus = Hud.Label(top, "Status", "", 15, OzUi.Ink).At(914, 17, OzH.R, true, 225);
            skirmishUnitRow = Hud.Node("Units", top); skirmishUnitRow.TL(0, 59, 930, 20);
            skirmishPowerRow = Hud.Node("AttackingPower", top); skirmishPowerRow.TL(0, 83, 930, 20);
            Hud.Label(skirmishUnitRow, "Metric", "UNITS", 14, OzUi.Ink).At(16, 10, OzH.L, true, 166);
            Hud.Label(skirmishPowerRow, "Metric", "ATTACKING POWER", 14, OzUi.Ink).At(16, 10, OzH.L, true, 166);
            skirmishRail = Hud.Node("BalanceRail", top); skirmishRail.TL(194, 109, 720, 4);
            Hud.Img(skirmishRail, "Track", OzUi.GM950).rectTransform.Fill();
            skirmishSeatCount = OzAiMapCatalog.FindSkirmishMap(game.level)?.Starts ?? 3;
            skirmishNetwork = Multiplayer.DuelRuntime.I?.SkirmishRoom == true;
            float column = 720f / skirmishSeatCount;
            for (int i = 0; i < skirmishSeatCount; i++)
            {
                var team = (Team)(i + 1);
                float x = 194 + (i + .5f) * column;
                float networkWidth = skirmishNetwork ? 54 : 0;
                skirmishSeats[i] = Hud.Label(top, "Participant", "", skirmishSeatCount > 4 ? 12 : 18, OzUi.TeamText(team))
                    .At(x - networkWidth * .5f, 44, OzH.C, true, column - 3 - networkWidth);
                if (skirmishNetwork)
                {
                    float right = x + column * .5f;
                    skirmishPings[i] = Hud.Label(top, "Participant ping", "", 13, OzUi.Ink).At(right - 19, 44, OzH.R, true, 38);
                    for (int bar = 0; bar < 3; bar++)
                    {
                        int height = 3 + bar * 3;
                        var signal = Hud.Img(top, "Connection health", OzUi.Muted);
                        signal.rectTransform.TL(right - 15 + bar * 4, 49 - height, 3, height);
                        signal.gameObject.SetActive(false);
                        skirmishSignals[i, bar] = signal;
                    }
                }
                skirmishUnits[i] = Hud.Label(skirmishUnitRow, "Count", "", 18, OzUi.Ink).At(x, 10, OzH.C, true, column - 3);
                skirmishPower[i] = Hud.Label(skirmishPowerRow, "Score", "", skirmishSeatCount > 4 ? 14 : 18, OzUi.Ink).At(x, 10, OzH.C, true, column - 3);
                skirmishSegments[i] = Hud.Img(skirmishRail, "Share", OzUi.Mark(team)).rectTransform;
            }
        }

        void RefreshSkirmishTopBar()
        {
            float now = OzMotion.Now;
            if (now < nextTop) return;
            nextTop = now + .1f;
            var mode = OzForceBarOption.ForMatch(Multiplayer.DuelRuntime.Active);
            bool showUnits = mode != OzForceBarOption.Mode.AttackingPower;
            bool showPower = mode != OzForceBarOption.Mode.Units;
            bool both = showUnits && showPower;
            top.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, both ? 122 : 98);
            skirmishUnitRow.gameObject.SetActiveFast(showUnits);
            skirmishPowerRow.gameObject.SetActiveFast(showPower);
            skirmishPowerRow.TL(0, both ? 83 : 59, 930, 20);
            skirmishRail.TL(194, both ? 109 : 85, 720, 4);
            skirmishClock.Set(UnitInfo.Clock(W.Time));
            skirmishTitle.Set(W.SkirmishHasTeams ? "ARMY BALANCE · TEAMS" : "ARMY BALANCE · FREE-FOR-ALL");
            skirmishStatus.Set(W.SkirmishEliminated(Player) ? "OBSERVING" : "");
            double total = 0;
            for (int i = 0; i < skirmishSeatCount; i++)
            {
                var team = (Team)(i + 1);
                bool eliminated = W.SkirmishEliminated(team);
                if (skirmishNetwork && skirmishSeatCount > 4)
                    skirmishSeats[i].Set($"{i + 1}{(eliminated ? " OUT" : "")}");
                else
                    skirmishSeats[i].Set($"{i + 1} · {TeamColors.Name(team)}{(eliminated ? " · OUT" : team == Player ? " · YOU" : "")}");
                if (skirmishNetwork) RefreshSkirmishPing(i);
                int units = eliminated ? 0 : W.UnitCount(team);
                skirmishUnits[i].Set(units.ToString());
                skirmishScores[i] = showPower && !eliminated ? W.MilitaryValue(team) : 0;
                total += skirmishShares[i] = showPower ? skirmishScores[i] : units;
            }
            float x = 0;
            for (int i = 0; i < skirmishSeatCount; i++)
            {
                double score = skirmishScores[i];
                double opponent = 0;
                for (int j = 0; j < skirmishSeatCount; j++)
                    if (j != i) opponent = Math.Max(opponent, skirmishScores[j]);
                skirmishPower[i].Set(W.SkirmishEliminated((Team)(i + 1)) ? "—" : $"{Math.Round(score):0}  {ForcePercent(ZForceComparison.RelativePercent(score, opponent))}");
                float width = total > 0 ? (float)(720 * skirmishShares[i] / total) : 0;
                skirmishSegments[i].TL(x, 0, width, 4);
                x += width;
            }
        }

        void RefreshSkirmishPing(int seat)
        {
            var runtime = Multiplayer.DuelRuntime.I;
            var reading = runtime != null && runtime.HasFreshRoomLatency
                ? Array.Find(runtime.RoomPings, entry => entry.Seat == seat)
                : null;
            bool measured = reading != null && reading.Ping > 0 && !reading.Disconnected;
            skirmishPings[seat].Set(runtime != null && runtime.RoomHostPosition == seat ? "HOST" :
                reading?.Disconnected == true ? "— ×" : measured ? $"{reading.Ping} ms" : "");
            int health = !measured ? 0 : reading.Ping >= 160 || reading.Variation >= 35 ? 1 :
                reading.Ping >= 95 || reading.Variation >= 15 ? 2 : 3;
            Color color = health == 3 ? new Color32(166, 205, 122, 255) :
                health == 2 ? new Color32(234, 196, 123, 255) : new Color32(243, 146, 121, 255);
            for (int bar = 0; bar < 3; bar++)
            {
                var signal = skirmishSignals[seat, bar];
                signal.gameObject.SetActiveFast(measured);
                signal.color = bar < health ? color : OzUi.A(OzUi.Muted, .4f);
            }
        }

        void BuildForceComparison()
        {
            var rail = Hud.Node("ForceBalance", top); rail.TL(79, 57, 622, 4);
            Hud.Img(rail, "Track", OzUi.GM950).rectTransform.Fill();
            forceRedShare = Hud.Img(rail, "RedShare", OzUi.TeamPaint(Team.Red));
            forceBlueShare = Hud.Img(rail, "BlueShare", OzUi.TeamPaint(Team.Blue));
            forceCursor = Hud.Img(rail, "ForceBalanceMarker", OzUi.Ink);
            forceCursor.rectTransform.TL(310, -2, 2, 8);
            forceRedGlow = Hud.Img(top, "RedBonusGlow", Color.clear); forceRedGlow.rectTransform.Place(new OzRect(77, 40, 220, 14));
            forceBlueGlow = Hud.Img(top, "BlueBonusGlow", Color.clear); forceBlueGlow.rectTransform.Place(new OzRect(483, 40, 220, 14));
            forceRedBonus = Hud.Label(top, "RedFortBonus", "", 9, OzUi.Ink).At(187, 47, OzH.C, true, 216);
            forceBlueBonus = Hud.Label(top, "BlueFortBonus", "", 9, OzUi.Ink).At(593, 47, OzH.C, true, 216);
            forceRedBonus.rectTransform.gameObject.SetActive(false); forceBlueBonus.rectTransform.gameObject.SetActive(false);
            BuildAttackingPowerRow();
            if (Multiplayer.DuelRuntime.Active) BuildMatchNetworkReadout();
        }

        void BuildAttackingPowerRow()
        {
            powerRow = Hud.Node("AttackingPowerRow", top);
            powerRow.TL(130, 90, 520, 32);
            Hud.Plate(powerRow, "sm", shadow: false);
            AddBlocker(powerRow);
            powerRedText = Hud.Label(powerRow, "RedPower", "", 16, OzUi.TeamText(Team.Red)).At(14, 11, OzH.L, true, 148);
            powerBlueText = Hud.Label(powerRow, "BluePower", "", 16, OzUi.TeamText(Team.Blue)).At(506, 11, OzH.R, true, 148);
            Hud.Label(powerRow, "Metric", "ATTACKING POWER", 13, OzUi.Amber).At(260, 11, OzH.C, true, 176);
            var rail = Hud.Node("PowerShare", powerRow); rail.TL(14, 23, 492, 4);
            powerRedShare = Hud.Img(rail, "RedShare", OzUi.TeamPaint(Team.Red)).rectTransform;
            powerBlueShare = Hud.Img(rail, "BlueShare", OzUi.TeamPaint(Team.Blue)).rectTransform;
            powerRow.gameObject.SetActive(false);
        }

        void BuildMatchNetworkReadout()
        {
            var panel = Hud.Node("Match network status", top);
            panel.TL(OzLayout.TopStrip.w + 8, 2, 254, 47);
            Hud.Plate(panel, "sm", 15, OzUi.Amber, shadow: false);
            Hud.Img(panel, "Column divider", OzUi.A(OzUi.Ink2, .4f)).rectTransform.TL(110, 10, 1, 27);
            Hud.Label(panel, "Ping label", "PING", 11, OzUi.Muted).At(16, 7);
            Hud.Label(panel, "Sync label", "SYNC", 11, OzUi.Muted).At(125, 7);
            matchPing = Hud.Label(panel, "Ping in milliseconds", "—", 18, OzUi.Ink).At(16, 21);
            matchSync = Hud.Label(panel, "Sync in hertz", "—", 18, OzUi.Ink).At(125, 21);
            matchRelay = Hud.Label(panel, "Relay", "", 10, OzUi.Amber).At(200, 7);
        }

        void RefreshForceMetrics(int redUnits, int blueUnits)
        {
            if (OpenZ.Multiplayer.DuelRuntime.Active)
            {
                SetFortProductionBoost(Team.Red, W.DuelFortLoserReduction(Team.Red));
                SetFortProductionBoost(Team.Blue, W.DuelFortLoserReduction(Team.Blue));
            }
            var mode = OzForceBarOption.ForMatch(W.HasDuelRules);
            bool valueMode = mode == OzForceBarOption.Mode.AttackingPower;
            if (forceValueMode != valueMode)
            {
                forceValueMode = valueMode; shownRed = shownBlue = int.MinValue;
                lastRedW = lastBlueW = -1;
            }
            bool both = mode == OzForceBarOption.Mode.Both;
            double redPower = 0, bluePower = 0;
            string delta = "", otherDelta = "";
            if (valueMode || both)
            {
                redPower = W.MilitaryValue(Team.Red);
                bluePower = W.MilitaryValue(Team.Blue);
                delta = ForcePercent(ZForceComparison.RelativePercent(redPower, bluePower));
                otherDelta = ForcePercent(ZForceComparison.RelativePercent(bluePower, redPower));
            }
            powerRow.gameObject.SetActiveFast(both);
            if (both)
            {
                powerRedText.Set($"{Math.Round(redPower):0}  {delta}");
                powerBlueText.Set($"{otherDelta}  {Math.Round(bluePower):0}");
                float share = 492f * (float)ZForceComparison.Share(redPower, bluePower);
                powerRedShare.TL(0, 0, share, 4);
                powerBlueShare.TL(share, 0, 492 - share, 4);
            }
            double forceRed = valueMode ? redPower : redUnits;
            double forceBlue = valueMode ? bluePower : blueUnits;
            redTick.Set((int)Math.Round(forceRed)); blueTick.Set((int)Math.Round(forceBlue));
            forceShare = (float)ZForceComparison.Share(forceRed, forceBlue);
            redBar.Set(valueMode ? forceShare : redUnits / (float)ZWorld.MAX_UNITS_PER_TEAM);
            blueBar.Set(valueMode ? 1 - forceShare : blueUnits / (float)ZWorld.MAX_UNITS_PER_TEAM);
            redCount.rectTransform.sizeDelta = new Vector2(120, redCount.rectTransform.sizeDelta.y);
            blueCount.rectTransform.sizeDelta = new Vector2(120, blueCount.rectTransform.sizeDelta.y);
            redOf.Set(valueMode ? delta : "/ 70");
            blueOf.Set(valueMode ? otherDelta : "/ 70");
            redOf.rectTransform.sizeDelta = new Vector2(120, redOf.rectTransform.sizeDelta.y);
            // Keep the right edge fixed when widening this right-aligned label.
            blueOf.At(OzLayout.TopCountRX, OzLayout.TopOf70Y, OzH.R, false, 120);
            string description = valueMode || both ? "Attacking power: normal construction time of offensive mobile units. Robot squads count once; cranes, unarmed units, cannons and buildings do not count." : "Units on the field; robots count individually. At 70 the factories pause.";
            string reference = valueMode || both ? "The attacking-power gap is relative to the smaller army." : "The cursor marks each side's share of the total unit count.";
            forceRedHot.Tooltip = TeamColors.Name(Team.Red) + " FORCE|" + description + "\n" + reference + "\nUnits: " + redUnits + "/70." + BoostTip(forceRedBoost) + "|";
            forceBlueHot.Tooltip = TeamColors.Name(Team.Blue) + " FORCE|" + description + "\n" + reference + "\nUnits: " + blueUnits + "/70." + BoostTip(forceBlueBoost) + "|";
        }
        static string BoostTip(double reduction) => reduction > 0 ? "\nCatch-up boost applied: fort construction time −" + (reduction * 100).ToString("0.#") + "%." : "";

        static string ForcePercent(double percent)
        {
            if (double.IsPositiveInfinity(percent)) return "+∞";
            if (double.IsNegativeInfinity(percent)) return "−∞";
            if (Math.Abs(percent) < .05) return "0%";
            return (percent > 0 ? "+" : "−") + Math.Abs(percent).ToString("0.#") + "%";
        }

        void RefreshForceNumbers()
        {
            SetNum(redCount, ref shownRed, redTick.Value);
            SetNum(blueCount, ref shownBlue, blueTick.Value);
            var duel = Multiplayer.DuelRuntime.I;
            if (matchPing == null || !duel) return;
            matchPing.Set(duel.PingMs.ToString("0") + " ms");
            float? rate = duel.SyncRateHz;
            matchSync.Set(duel.Playing && rate.HasValue ? rate.Value.ToString("0.0") + " Hz" : "—");
            matchRelay.Set(duel.OnlinePeer?.UsesRelay == true ? "TURN" : "");
        }

        /// <summary>Display the actual fort-production reduction supplied by the match policy; never changes production.</summary>
        public void SetFortProductionBoost(Team team, double reduction)
        {
            if (double.IsNaN(reduction) || reduction < 0 || reduction > 1) throw new ArgumentOutOfRangeException(nameof(reduction));
            if (team == Team.Red) forceRedBoost = reduction;
            else if (team == Team.Blue) forceBlueBoost = reduction;
        }

        void RefreshForceAppearance()
        {
            float share = forceCursor ? Mathf.Lerp((forceCursor.rectTransform.anchoredPosition.x + 1) / 622f, forceShare, OzMotion.Reduced ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12)) : forceShare;
            forceRedShare.rectTransform.TL(0, 0, 622 * share, 4);
            forceBlueShare.rectTransform.TL(622 * share, 0, 622 * (1 - share), 4);
            forceCursor.rectTransform.TL(622 * share - 1, -2, 2, 8);
            ShowFortBoost(forceRedBoost, forceRedGlow, forceRedBonus);
            ShowFortBoost(forceBlueBoost, forceBlueGlow, forceBlueBonus);
        }
        static void ShowFortBoost(double reduction, Image glow, OzText label)
        {
            bool active = reduction > 0;
            label.rectTransform.gameObject.SetActiveFast(active);
            if (!active) { glow.color = Color.clear; return; }
            float pulse = OzMotion.Reduced ? .25f : .2f + .08f * Mathf.Sin(Time.unscaledTime * 3);
            glow.color = OzUi.A(OzUi.Amber, pulse);
            label.Set("FORT BOOST −" + (reduction * 100).ToString("0.#") + "%");
        }
    }
}
