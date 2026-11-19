using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        OzText requestTime;
        bool requestAiMaps;
        float requestMapScroll;
        string shownRequestMap;

        internal void ShowMatchRequest()
        {
            if (R.ConnectionInterrupted) return;
            FindFirstObjectByType<OpenZPauseMenu>()?.Close();
            if (OzConfirm.Blocks) OzConfirm.Dismiss();
            requestAiMaps = OzAiMapCatalog.Find(R.MatchMap ?? R.View.Map) != null;
            shownRequestMap = null;
            requestMapScroll = 1;
            page = Page.MatchRequest;
            dirty = true;
        }

        internal void ShowConnectionInterrupted()
        {
            FindFirstObjectByType<OpenZPauseMenu>()?.Close();
            if (OzConfirm.Blocks) OzConfirm.Dismiss();
            page = Page.Reconnect;
            dirty = true;
        }

        void BuildMatchRequestPage()
        {
            var shade = Hud.Img(root, "Match pause", new Color(0, 0, 0, .88f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            if (page == Page.Reconnect)
            {
                var popup = Panel("CONNECTION INTERRUPTED", 485, 255, 950, 520);
                requestTime = Label(popup, "", 370, 100, 280, 112, OzUi.Amber);
                Label(popup, "The duel is paused. Waiting for your opponent to reconnect.", 65, 285, 830, 22, OzUi.Ink);
                Button(popup, "LEAVE MATCH", 260, 383, 430, R.LeaveInterruptedMatch);
                return;
            }
            if (R.CurrentMatchRequest == DuelRuntime.MatchRequestKind.Lobby)
            {
                var popup = Panel("RETURN TO LOBBY", 455, 310, 1010, 410);
                requestTime = Label(popup, "", 815, 42, 155, 42, OzUi.Amber);
                Label(popup, R.MatchRequestOwner ? "Waiting for " + R.OpponentName + "." : R.OpponentName + " asks to return to the lobby.", 40, 115, 925, 24, OzUi.Ink);
                Label(popup, "Accept ends the duel without a winner. Decline resumes play.", 40, 168, 925, 20, OzUi.Muted);
                Button(popup, "ACCEPT", 40, 260, 420, () => R.AnswerMatchRequest(true), true, !R.MatchRequestOwner);
                Button(popup, "DECLINE", 540, 260, 420, () => R.AnswerMatchRequest(false), false, !R.MatchRequestOwner);
                return;
            }

            var panel = Panel("CHANGE MAP", 135, 80, 1650, 920);
            if (!R.MatchRequestOwner && shownRequestMap != R.MatchMap && R.MatchMap != null)
            {
                requestAiMaps = OzAiMapCatalog.Find(R.MatchMap) != null;
                requestMapScroll = 1;
            }
            shownRequestMap = R.MatchMap;
            requestTime = Label(panel, "", 1440, 24, 180, 42, OzUi.Amber);
            Label(panel, "Your lobby rules carry over. Return to the lobby to change them. You have 30 seconds to agree.", 26, 68, 1480, 19, OzUi.Ink);
            Button(panel, "OPENZ MAPS", 26, 108, 240, () => { requestAiMaps = false; requestMapScroll = 1; dirty = true; }, !requestAiMaps);
            Button(panel, "AI MAPS", 278, 108, 240, () => { requestAiMaps = true; requestMapScroll = 1; dirty = true; }, requestAiMaps);
            var maps = requestAiMaps ? OzAiMapCatalog.Playable.Where(m => m.Duel).Select(OzAiMapGallery.Definition).ToArray()
                : OzLevelDef.All.Where(m => DuelRules.IsMapAllowed(m.Id)).ToArray();
            var list = Scroll(panel, 26, 172, 510, 470, false, Math.Max(470, maps.Length * 56));
            var scroll = list.parent.GetComponent<ScrollRect>();
            scroll.verticalNormalizedPosition = requestMapScroll;
            scroll.onValueChanged.AddListener(p => requestMapScroll = p.y);
            bool mayPick = R.MatchRequestOwner && !R.MatchMapSuggested;
            for (int i = 0; i < maps.Length; i++)
            {
                var map = maps[i];
                Button(list, map.Number + " · " + map.Name, 0, i * 56, 485, () => R.SelectMatchMap(map.Id),
                    R.MatchMap == map.Id, mayPick && map.Id != R.View.Map);
            }
            string selectedId = R.MatchMap ?? R.View.Map;
            var selected = OzAiMapGallery.Definition(selectedId);
            var preview = Resources.Load<Texture2D>("OpenZ/AiMaps/Previews/" + selectedId)
                ?? Resources.Load<Texture2D>("OpenZ/Menu/" + selectedId + "_poster");
            if (preview)
            {
                var image = Hud.Node("Map preview", panel).gameObject.AddComponent<RawImage>();
                image.texture = preview;
                image.rectTransform.TL(572, 172, 820, 360);
                image.uvRect = OzLevelPreview.Cover((float)preview.width / preview.height, 820f / 360);
                image.raycastTarget = false;
            }
            Label(panel, selected?.Name ?? selectedId, 572, 545, 800, 24, OzUi.Ink);
            string request = R.MatchRequestOwner
                ? R.MatchMapSuggested ? "Waiting for " + R.OpponentName + "." : "Select a map to suggest to " + R.OpponentName + "."
                : R.MatchMapSuggested ? R.OpponentName + " suggests this map. Accept starts it. Decline resumes this duel."
                    : R.OpponentName + " is choosing a map.";
            Label(panel, request, 572, 584, 1000, 18, OzUi.Muted);
            if (R.MatchRequestOwner)
                Button(panel, "SUGGEST MAP", 1120, 625, 470, R.SuggestMatchMap, true, mayPick && R.MatchMap != null);
            else
            {
                Button(panel, "ACCEPT", 1120, 625, 225, () => R.AnswerMatchRequest(true), true, R.MatchMapSuggested);
                Button(panel, "DECLINE", 1365, 625, 225, () => R.AnswerMatchRequest(false), false, R.MatchMapSuggested);
            }
            BuildChatPanel(161, 786, 1598, 190);
        }
    }
}
