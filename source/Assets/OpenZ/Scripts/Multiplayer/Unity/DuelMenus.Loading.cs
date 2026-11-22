using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        string battlefieldLoadingMapId;
        AsyncOperation battlefieldLoad;
        bool waitingForHostLoad, battlefieldReady;
        OzText battlefieldStage, battlefieldProgress, battlefieldDetail;
        Image battlefieldFill;
        RawImage battlefieldPlanet;

        bool BattlefieldLoading => page == Page.Loading || page == Page.SkirmishLoading;
        internal bool MultiplayerLoading => page == Page.Loading;

        internal void ShowMultiplayerLoading(string mapId, bool waitingForHost = false) =>
            ShowBattlefieldLoading(mapId, Page.Loading, waitingForHost);

        void ShowBattlefieldLoading(string mapId, Page loadingPage, bool waitingForHost = false)
        {
            battlefieldLoadingMapId = mapId;
            battlefieldLoad = null;
            battlefieldReady = false;
            waitingForHostLoad = waitingForHost;
            page = loadingPage;
            dirty = true;
        }

        internal void TrackBattlefieldLoad(string scene)
        {
            battlefieldLoad = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single)
                ?? throw new InvalidOperationException("Unable to load the battlefield.");
        }

        internal void MarkBattlefieldReady()
        {
            if (MultiplayerLoading) battlefieldReady = true;
        }

        void BuildBattlefieldLoading()
        {
            var bed = Hud.Img(overlayRoot, "Loading backdrop", OzUi.GM950);
            bed.rectTransform.Fill();
            bed.raycastTarget = true;

            string id = battlefieldLoadingMapId;
            var map = OzAiMapCatalog.FindSkirmishMap(id);
            string world = map?.World ?? (id == "L19" ? "city" : null);
            var art = world == null ? null : Resources.Load<Texture2D>("OpenZ/AiMaps/Loading/" + world);
            battlefieldPlanet = null;
            if (art)
            {
                battlefieldPlanet = Hud.Node("Planet", overlayRoot).gameObject.AddComponent<RawImage>();
                battlefieldPlanet.texture = art;
                battlefieldPlanet.raycastTarget = false;
                battlefieldPlanet.rectTransform.Fill();
            }

            root = CreateSkirmishFrame(overlayRoot);
            root.name = "Battlefield loading frame";
            float x = OzMenuLayout.LoadX, w = OzMenuLayout.LoadW;
            OzAnniversaryBadge.Build(root, x, OzMenuLayout.LoadTop - 46, 11);
            Hud.Label(root, "Mode", MultiplayerLoading ? "MULTIPLAYER" : "SKIRMISH", 20, OzUi.Amber, .17f)
                .At(x, OzMenuLayout.LoadTop);
            string title = map?.Name ?? (id == "L19" ? "Mayhem" : "Battlefield");
            Hud.Label(root, "Battlefield name", title.ToUpperInvariant(), 105, OzUi.Ink, -.012f)
                .At(x, OzMenuLayout.LoadWordY + 13, width: 1410);
            if (world != null)
                Hud.Label(root, "Planet name", world.ToUpperInvariant() + " PLANET", 17, OzUi.Ink2, .13f)
                    .At(x, OzMenuLayout.LoadGoalY);

            battlefieldStage = Hud.Label(root, "Loading stage", "", 18, OzUi.Ink2, .09f)
                .At(x, OzMenuLayout.LoadMeterY, width: 310);
            battlefieldProgress = Hud.Label(root, "Loading progress", "", 18, OzUi.Amber, .07f)
                .At(x + w, OzMenuLayout.LoadMeterY, OzH.R, width: 145);
            var track = new OzRect(x, OzMenuLayout.LoadBarY + 4, w, OzMenuLayout.LoadBarH);
            var bar = Hud.Kit(root, "Loading track", "Bars/bar_track", track, null);
            if (!bar.sprite) bar.color = OzUi.GM700;
            battlefieldFill = OzKit.FillBar(root, "Loading fill", "amber");
            battlefieldFill.rectTransform.Place(track);
            battlefieldFill.raycastTarget = false;
            if (!battlefieldFill.sprite) battlefieldFill.color = OzUi.Amber;
            battlefieldDetail = Hud.Label(root, "Loading detail", "", 20, OzUi.Ink2)
                .At(x, OzMenuLayout.LoadTipY + 4, width: 1300);

            var mini = map != null ? SkirmishMinimap(map) : GeneratedSkirmishMinimap(id);
            if (mini) BuildLoadingMinimap(mini, map != null && !map.IsOfficial);
            UpdateBattlefieldLoading();
        }

        void BuildLoadingMinimap(Texture2D texture, bool aiMap)
        {
            const float panelWidth = 274, pictureSize = 242;
            var panel = Hud.Img(root, "Minimap frame", OzUi.GM500);
            panel.rectTransform.Place(new OzRect(1550, 675, panelWidth, 291));
            panel.raycastTarget = false;
            var backing = Hud.Img(panel.transform, "Minimap backing", OzUi.GM900);
            backing.rectTransform.TL(1, 1, panelWidth - 2, 289);
            backing.raycastTarget = false;
            var accent = Hud.Img(panel.transform, "Minimap accent", OzUi.AmberLo);
            accent.rectTransform.TL(0, 0, 52, 1);
            accent.raycastTarget = false;
            Hud.Label(panel.transform, "Minimap label", "MINIMAP", 16, OzUi.Ink2, .13f).At(16, 14);
            if (aiMap) OzAiMapBadge.Stamp(panel.rectTransform, panelWidth - 4);

            var picture = Hud.Node("Minimap", panel.transform).gameObject.AddComponent<RawImage>();
            picture.texture = texture;
            picture.raycastTarget = false;
            float scale = pictureSize / Mathf.Max(texture.width, texture.height);
            float width = texture.width * scale, height = texture.height * scale;
            picture.rectTransform.TL((panelWidth - width) / 2, 35 + (pictureSize - height) / 2, width, height);
        }

        void UpdateBattlefieldLoading()
        {
            FitSkirmishFrame();
            var size = overlayRoot.rect.size;
            if (battlefieldPlanet && size.x > 0 && size.y > 0)
                battlefieldPlanet.uvRect = OzLevelPreview.Cover(1920f / 1080f, size.x / size.y);

            bool sceneLoaded = battlefieldLoad?.isDone == true;
            float progress = sceneLoaded || battlefieldReady ? 1 : Mathf.Clamp01((battlefieldLoad?.progress ?? 0) / .9f);
            string stage = "LOADING BATTLEFIELD";
            string meter = Mathf.Min(99, Mathf.FloorToInt(100 * progress)) + "%";
            string detail = MultiplayerLoading ? "Loading the map on your computer." : "Loading terrain, buildings and starting units.";

            if (waitingForHostLoad)
            {
                stage = "WAITING FOR HOST";
                meter = "";
                detail = "Waiting for the host to send the match settings.";
            }
            else if (battlefieldReady)
            {
                meter = "MAP LOADED";
                int planning = R.View?.PlanningSeconds ?? 0;
                if (R.Countdown >= 0)
                {
                    stage = planning > 0 ? "OPENING ORDERS" : "STARTING MATCH";
                    double seconds = Math.Max(1, Math.Ceiling(R.Countdown - planning));
                    detail = (planning > 0 ? "Opening orders begin in " : "The match starts in ") + seconds + " s.";
                }
                else
                {
                    stage = "WAITING FOR PLAYERS";
                    detail = "Your map is loaded. Waiting for all active players to load.";
                }
            }
            else if (progress >= 1)
            {
                stage = "PREPARING BATTLEFIELD";
                meter = sceneLoaded ? "MAP LOADED" : "";
                detail = MultiplayerLoading ? "Setting up the battlefield for the match." : "Setting up teams and AI before the match starts.";
            }

            battlefieldStage.Set(stage);
            battlefieldProgress.Set(meter);
            battlefieldDetail.Set(detail);
            if (battlefieldFill.sprite) battlefieldFill.fillAmount = progress;
            else battlefieldFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, OzMenuLayout.LoadW * progress);
        }
    }
}
