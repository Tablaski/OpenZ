using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        static Sprite ghostIcon;
        bool ghostOptions, ghostShowCursor = true, ghostShowOrders = true, ghostShowProduction = true;
        int ghostShownTarget = -2, ghostShownClick, ghostShownOrder;
        int ghostProductionId = -1;
        string ghostProductionState, ghostProductionEvent;
        float ghostProductionUntil;
        float ghostClickUntil, ghostOrderUntil, ghostClickX, ghostClickY;
        OzText ghostStatus, ghostSelectionText, ghostActionText, ghostFactoryText;
        RectTransform ghostCursor, ghostClickMarker, ghostOrderMarker, ghostFactoryPanel, ghostLayer;
        readonly List<RectTransform> ghostUnitMarkers = new List<RectTransform>(32);

        void BuildGhostView()
        {
            ghostUnitMarkers.Clear();
            ghostLayer = Hud.Node("Ghost overlay", root);
            ghostLayer.Fill();
            var badge = Panel("", 25, 85, 520, 78);
            DrawGhostIcon(badge, 24, 19, 22);
            Label(badge, "GHOST", 54, 20, 430, 20, OzForkOptions.Accent);
            ghostStatus = Label(badge, "", 24, 46, 472, 17, OzUi.Muted);
            var targets = R.GhostTargets;
            var players = Panel("FOLLOW PLAYER", 25, 177, 380, 62 + 43 * targets.Length);
            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                Button(players, target.Name, 20, 52 + i * 43, 340, () => R.FollowGhostTarget(target.Position),
                    R.GhostFollowing && R.GhostTarget == target.Position, height: 38);
            }

            var controls = Panel("SPECTATOR CONTROLS", 25, 734, 1015, 128);
            Button(controls, "◀ PREVIOUS · Q", 20, 58, 150, () => R.CycleGhostTarget(-1), enabled: targets.Length > 0, height: 42);
            Button(controls, "NEXT · E ▶", 180, 58, 140, () => R.CycleGhostTarget(1), enabled: targets.Length > 0, height: 42);
            Button(controls, R.GhostFollowing ? "FREE CAMERA · F" : "FOLLOW · F", 330, 58, 207,
                () => R.SetGhostCameraMode(!R.GhostFollowing), enabled: targets.Length > 0, height: 42);
            Button(controls, "RECENTER · R", 547, 58, 180, () => R.SetGhostCameraMode(true), enabled: targets.Length > 0, height: 42);
            Button(controls, "VIEW OPTIONS", 737, 58, 253, () => { ghostOptions = !ghostOptions; dirty = true; }, ghostOptions, height: 42);

            var activity = Panel("LIVE ACTIONS", 1054, 720, 475, 142);
            ghostSelectionText = Label(activity, "", 22, 57, 430, 16, OzUi.Ink);
            ghostActionText = Label(activity, "", 22, 93, 430, 16, OzUi.Muted);
            ghostFactoryPanel = Panel("PRODUCTION OPEN", 416, 177, 502, 136);
            ghostFactoryText = Label(ghostFactoryPanel, "", 24, 60, 454, 17, OzUi.Ink).Body(24, 60, 454);
            if (ghostOptions) DrawGhostOptions();

            ghostCursor = Marker("➤", 31, OzUi.Ink);
            ghostClickMarker = Marker("◉", 36, OzUi.Amber);
            ghostOrderMarker = Marker("◎", 38, OzUi.Amber);
            ghostShownTarget = -2;
            TickGhostView();
        }

        static void DrawGhostIcon(Transform parent, float x, float y, float size)
        {
            if (!ghostIcon)
            {
                const int extent = 32;
                var texture = new Texture2D(extent, extent, TextureFormat.RGBA32, false) { name = "Ghost icon" };
                var pixels = new Color32[extent * extent];
                var body = new Color32(243, 241, 230, 255);
                var shade = new Color32(190, 198, 206, 255);
                var face = new Color32(39, 43, 50, 255);
                for (int row = 3; row < 30; row++)
                for (int column = 4; column < 28; column++)
                {
                    float horizontal = column - 15.5f;
                    float top = Mathf.Max(0, row - 17);
                    int hem = 3 + Mathf.RoundToInt(2 * (1 + Mathf.Cos((column - 4) * Mathf.PI / 4)));
                    if (horizontal * horizontal + top * top > 144 || row < hem) continue;
                    bool eye = row >= 17 && row <= 21 && (column is >= 10 and <= 12 or >= 19 and <= 21);
                    bool mouth = row >= 12 && row <= 15 && column is >= 14 and <= 17;
                    pixels[row * extent + column] = eye || mouth ? face : row < hem + 2 ? shade : body;
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                ghostIcon = Sprite.Create(texture, new Rect(0, 0, extent, extent), new Vector2(.5f, .5f));
            }
            var icon = Hud.Img(parent, "Ghost icon", Color.white);
            icon.sprite = ghostIcon;
            icon.rectTransform.TL(x, y, size, size);
            icon.raycastTarget = false;
        }

        RectTransform Marker(string name, int size, Color color)
        {
            var marker = Hud.Node(name, ghostLayer);
            var text = Hud.Label(marker, name, name, size, color).FillParent(OzH.C);
            text.graphic.raycastTarget = false;
            marker.gameObject.SetActive(false);
            return marker;
        }

        void DrawGhostOptions()
        {
            var panel = Panel("VIEW OPTIONS", 633, 352, 610, 358);
            Button(panel, "CLOSE", 440, 12, 145, () => { ghostOptions = false; dirty = true; }, height: 36);
            Label(panel, "VISION", 24, 69, 250, 18, OzUi.Ink);
            Label(panel, "Use the tracked player's radar and fog.", 24, 94, 350, 14, OzUi.Muted);
            Button(panel, "PLAYER FOG", 342, 67, 118, () => { R.GhostFog = true; dirty = true; }, R.GhostFog, height: 39);
            Button(panel, "FULL MAP", 469, 67, 117, () => { R.GhostFog = false; dirty = true; }, !R.GhostFog, height: 39);
            Toggle("PLAYER CURSOR", "Show their mouse and click markers.", 131, ghostShowCursor, value => ghostShowCursor = value);
            Toggle("SELECTIONS & ORDERS", "Highlight selected units and show order markers.", 196, ghostShowOrders, value => ghostShowOrders = value);
            Toggle("PRODUCTION", "Show the open factory and its unit queue.", 261, ghostShowProduction, value => ghostShowProduction = value);

            void Toggle(string name, string description, int y, bool value, Action<bool> change)
            {
                Label(panel, name, 24, y + 2, 430, 18, OzUi.Ink);
                Label(panel, description, 24, y + 29, 430, 14, OzUi.Muted);
                Button(panel, "OFF", 446, y, 64, () => { change(false); dirty = true; }, !value, height: 39);
                Button(panel, "ON", 521, y, 64, () => { change(true); dirty = true; }, value, height: 39);
            }
        }

        internal void TickGhostView()
        {
            if (page != Page.Entry || !R.RoomObserver || !ghostLayer || !ghostLayer.gameObject.activeInHierarchy || !R.Game) return;
            var view = R.GhostFollowing ? R.GhostView : null;
            var target = R.GhostTargets.FirstOrDefault(person => person.Position == R.GhostTarget);
            ghostStatus?.Set(R.GhostFollowing && target != null ?
                $"Following {target.Name} · {(R.GhostFog ? "Player fog" : "Full map")}" : "Free camera · Full map");
            if (ghostShownTarget != R.GhostTarget)
            {
                ghostShownTarget = R.GhostTarget;
                ghostShownClick = view?.Click ?? 0;
                ghostShownOrder = view?.Order ?? 0;
                ghostClickUntil = ghostOrderUntil = 0;
                ghostProductionId = -1;
                ghostProductionUntil = 0;
            }
            var camera = Camera.main;
            bool hasView = view != null && camera;
            float now = Time.unscaledTime;
            if (hasView && ghostShowCursor && view.HasMouse)
            {
                CursorAt(ghostCursor, view.MouseX, view.MouseY, 35);
                if (view.Click != ghostShownClick)
                {
                    ghostShownClick = view.Click;
                    ghostClickUntil = now + (view.MouseButton == 2 ? .65f : .35f);
                    ghostClickX = view.MouseX;
                    ghostClickY = view.MouseY;
                }
            }
            else ghostCursor.gameObject.SetActive(false);
            if (hasView && ghostShowCursor && now < ghostClickUntil) CursorAt(ghostClickMarker, ghostClickX, ghostClickY, 38);
            else ghostClickMarker.gameObject.SetActive(false);
            if (hasView && view.Order != ghostShownOrder)
            {
                ghostShownOrder = view.Order;
                ghostOrderUntil = now + 2.5f;
            }
            if (hasView && ghostShowOrders && now < ghostOrderUntil)
                WorldMarker(ghostOrderMarker, camera, new Vector3(view.OrderX, 0, view.OrderZ), 38);
            else ghostOrderMarker.gameObject.SetActive(false);

            int used = 0;
            var world = R.Game.World;
            if (hasView && ghostShowOrders)
            {
                foreach (int id in view.Selected)
                {
                    var unit = world.Get(id);
                    if (unit == null || unit.Destroyed || unit.Owner != (Team)(view.Seat + 1)) continue;
                    if (used == ghostUnitMarkers.Count) ghostUnitMarkers.Add(Marker("◇", 28, OzUi.Amber));
                    WorldMarker(ghostUnitMarkers[used++], camera, ZGame.PxToWorld(unit.CenterX, unit.CenterY), 32);
                }
            }
            for (int i = used; i < ghostUnitMarkers.Count; i++) ghostUnitMarkers[i].gameObject.SetActive(false);
            ghostSelectionText?.Set(hasView && ghostShowOrders ? $"{used} selected" : "");
            ghostActionText?.Set(hasView && ghostShowOrders && now < ghostOrderUntil ? "Order issued" : "");

            var factory = hasView && ghostShowProduction ? world.Get(view.Factory) : null;
            if (factory != null && (factory.Owner != (Team)(view.Seat + 1) || !factory.IsProducer || factory.Destroyed)) factory = null;
            ghostFactoryPanel.gameObject.SetActive(factory != null);
            if (factory != null)
            {
                string queue = factory.Queue.Count == 0 ? "Empty" : string.Join(", ", factory.Queue.Select(UnitName));
                string state = (factory.Building ? UnitName(factory.CurUnit) : "Idle") + " · Queue " + queue;
                if (factory.Id == ghostProductionId && ghostProductionState != state)
                {
                    ghostProductionEvent = state;
                    ghostProductionUntil = now + 3.5f;
                }
                ghostProductionId = factory.Id;
                ghostProductionState = state;
                ghostFactoryText.Set($"{UnitName(factory.Type)} · {(factory.Building ? UnitName(factory.CurUnit) : "Idle")}\nQueue · {queue}");
            }
            else ghostProductionId = -1;
            if (hasView && ghostShowProduction && now < ghostProductionUntil)
                ghostActionText?.Set(ghostProductionEvent);

            void CursorAt(RectTransform marker, float px, float py, float size)
            {
                marker.TL(ghostLayer.rect.width * px - size / 2, ghostLayer.rect.height * (1 - py) - size / 2, size, size);
                marker.gameObject.SetActive(true);
            }

            void WorldMarker(RectTransform marker, Camera cam, Vector3 at, float size)
            {
                var uv = cam.WorldToViewportPoint(at);
                if (uv.z <= 0 || uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) { marker.gameObject.SetActive(false); return; }
                CursorAt(marker, uv.x, uv.y, size);
            }
        }

        void HandleGhostKeys()
        {
            var kb = Keyboard.current;
            if (page != Page.Entry || kb == null || !R.Playing || !R.RoomObserver || IsTyping || OzConfirm.Blocks ||
                OpenZHud.I && OpenZHud.I.Pause && OpenZHud.I.Pause.IsOpen) return;
            if (kb.qKey.wasPressedThisFrame) R.CycleGhostTarget(-1);
            if (kb.eKey.wasPressedThisFrame) R.CycleGhostTarget(1);
            if (kb.fKey.wasPressedThisFrame) R.SetGhostCameraMode(!R.GhostFollowing);
            if (kb.rKey.wasPressedThisFrame) R.SetGhostCameraMode(true);
        }
    }
}
