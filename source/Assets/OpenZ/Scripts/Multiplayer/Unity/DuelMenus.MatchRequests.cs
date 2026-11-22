using System;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        OzText requestTime;
        bool choosingRequestMap;
        string requestDraftMap;
        SkirmishRules requestDraftTech;

        void ResetMapChangeDraft()
        {
            choosingRequestMap = false;
            requestDraftMap = null;
            requestDraftTech = new SkirmishRules();
        }

        internal void ShowMatchRequest()
        {
            if (R.ConnectionInterrupted) return;
            FindFirstObjectByType<OpenZPauseMenu>()?.Close();
            if (OzConfirm.Blocks) OzConfirm.Dismiss();
            ResetMapChangeDraft();
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
            if (page == Page.MatchRequest && R.MapChangeActive) { BuildMapChange(); return; }
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

        }

        void BuildMapChange()
        {
            var shade = Hud.Img(root, "Map change pause", new Color(0, 0, 0, .57f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            bool voting = R.SkirmishRoom ? R.ProposedMap != null : R.MatchMapSuggested;
            bool editing = R.MapChangeOwner && !voting;
            string requester = R.MapChangeRequester ?? "A player";
            if (editing && choosingRequestMap) DrawOnlineGallery(inMatch: true);
            else if (!voting && !editing)
            {
                var waiting = Panel("ASK TO CHANGE MAP", 605, 416, 710, 214);
                Label(waiting, $"{requester} asked to change the map and is choosing a battlefield.", 28, 93, 654, 22, OzUi.Ink);
            }
            else
            {
                string mapId = voting ? R.ProposedMap : requestDraftMap;
                var panel = Panel(voting ? "MAP CHANGE VOTE" : "ASK TO CHANGE MAP", 215, 176, 1490, 728);
                string status = editing ? "Choose a map and review its factory tech before sending your proposal." :
                    $"{requester} proposes this map and factory tech.";
                Label(panel, status, 31, 78, 1426, 22, OzUi.Ink);
                DrawMapChangeDetails(panel, mapId, editing);
                if (editing)
                {
                    Button(panel, "BACK TO MATCH", 31, 640, 260, R.CancelMapChange);
                    Button(panel, "PROPOSE MAP", 1117, 640, 340,
                        () => R.SubmitMapChange(requestDraftMap, requestDraftTech.VehicleTech, requestDraftTech.RobotTech),
                        true, mapId != null, 48);
                }
                else if (R.SkirmishRoom)
                {
                    Label(panel, $"{R.RoomVotesNeeded} matching votes decide. If time runs out, the map changes.", 31, 638, 855, 19, OzUi.Muted);
                    Button(panel, "VETO", 951, 628, 239, () => R.CastRoomVote(MapChangeBallot.Veto), enabled: R.CanCastRoomMapVote);
                    Button(panel, "GO", 1205, 628, 252, () => R.CastRoomVote(MapChangeBallot.Go), true, R.CanCastRoomMapVote);
                }
                else if (!R.MapChangeOwner)
                {
                    Button(panel, "DECLINE", 951, 628, 239, () => R.AnswerMatchRequest(false));
                    Button(panel, "ACCEPT", 1205, 628, 252, () => R.AnswerMatchRequest(true), true);
                }
                else Label(panel, $"Waiting for {R.OpponentName} to respond.", 31, 638, 845, 19, OzUi.Muted);
            }

            var notice = Panel(voting ? "PAUSED · MAP CHANGE VOTE" : "PAUSED · MAP SELECTION", 110, 14, 380, 94);
            Label(notice, $"{requester} requested a map change", 23, 58, 285, 15, OzUi.Ink);
            requestTime = Label(notice, "", 309, 50, 66, 25, OzUi.Amber);
        }

        void DrawMapChangeDetails(RectTransform panel, string mapId, bool editing)
        {
            var mapPanel = Hud.Img(panel, "Selected battlefield", OzUi.GM950).rectTransform;
            mapPanel.TL(31, 120, 785, 479);
            var map = OzAiMapCatalog.FindSkirmishMap(mapId);
            var definition = OzAiMapGallery.Definition(mapId);
            Label(mapPanel, map?.Name ?? definition?.Name ?? "SELECT MAP", 18, 25, 506, 23, OzUi.Amber);
            if (editing) Button(mapPanel, "SELECT MAP", 520, 13, 247, OpenMapChangeGallery);
            if (mapId == null) return;
            var mini = map != null ? SkirmishMinimap(map) : GeneratedSkirmishMinimap(mapId);
            if (mini)
            {
                var image = Hud.Node("Tactical minimap", mapPanel).gameObject.AddComponent<RawImage>();
                image.texture = mini;
                float scale = Mathf.Min(700f / mini.width, 385f / mini.height);
                float w = mini.width * scale, h = mini.height * scale;
                image.rectTransform.TL((785 - w) / 2, 72 + (385 - h) / 2, w, h);
                image.raycastTarget = false;
                if (map != null && !map.IsOfficial) OzAiMapBadge.Stamp(image.rectTransform, w);
            }

            var mapTech = ReadMapTech(mapId);
            var rules = editing ? requestDraftTech : new SkirmishRules
                { VehicleTech = R.ProposedVehicleTech, RobotTech = R.ProposedRobotTech };
            BuildRoomMapTech(panel, 834, 120, mapTech, rules, editing, updated =>
            {
                requestDraftTech = updated;
                dirty = true;
            }, proposal: true);
        }

        void OpenMapChangeGallery()
        {
            choosingRequestMap = true;
            onlineProposedMap = requestDraftMap;
            onlineOfficial = requestDraftMap == null ? !R.SkirmishRoom : OzAiMapCatalog.Find(requestDraftMap) == null;
            onlineMapSize = R.SkirmishRoom ? OzAiMapCatalog.FindSkirmishMap(requestDraftMap ?? R.Game.level)?.Starts ?? 3 : 2;
            onlineMapSearch = onlineWorld = "";
            onlineMapPage = 0;
            dirty = true;
        }

        void SelectMapChangeDraft(string map)
        {
            if (map != requestDraftMap)
            {
                requestDraftMap = map;
                requestDraftTech.VehicleTech = requestDraftTech.RobotTech = 0;
            }
            choosingRequestMap = false;
            dirty = true;
        }
    }
}
