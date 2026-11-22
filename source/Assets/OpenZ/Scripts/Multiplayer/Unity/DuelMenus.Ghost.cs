using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        static Sprite ghostIcon;
        readonly List<RectTransform> ghostSeatButtons = new List<RectTransform>(8);
        readonly Vector3[] ghostCorners = new Vector3[4];
        RectTransform ghostLayer, ghostBand, ghostFreeButton, ghostFogControl;
        OzText ghostStatus, ghostFogStatus;
        RawImage ghostCursor;
        int ghostSeatMask;
        float ghostBandWidth, ghostBandHeight, ghostTargetsAt;

        void BuildGhostView()
        {
            ghostSeatButtons.Clear();
            ghostLayer = Hud.Node("Ghost overlay", root);
            ghostLayer.Fill();
            ghostBand = Hud.Node("Ghost controls", ghostLayer);
            Hud.Plate(ghostBand, "md", 0, OzForkOptions.Accent);
            DrawGhostIcon(ghostBand, 14, 9, 23);
            Label(ghostBand, "GHOST", 44, 13, 67, 17, OzForkOptions.Accent);
            ghostStatus = Label(ghostBand, "", 115, 16, 270, 13, OzUi.Muted);

            var targets = R.GhostTargets;
            ghostSeatMask = TargetMask(targets);
            foreach (var target in targets)
            {
                int seat = target.Position;
                var tint = OzUi.Mark((Team)(seat + 1));
                ghostSeatButtons.Add(GhostButton((seat + 1).ToString(), target.Name, tint,
                    R.GhostFollowing && R.GhostTarget == seat, () => R.FollowGhostTarget(seat), true));
            }
            ghostFreeButton = GhostButton("FREE CAM", "Free camera", OzForkOptions.Accent, !R.GhostFollowing,
                () => R.SetGhostCameraMode(false));
            ghostFogControl = null;
            ghostFogStatus = null;
            if (R.GhostFogAvailable)
            {
                if (R.GhostFollowing)
                {
                    ghostFogControl = Hud.Node("Player fog status", ghostBand);
                    ghostFogStatus = Hud.Label(ghostFogControl, "Fog status", "", 12, OzUi.Muted).FillParent(OzH.C);
                    ghostFogStatus.graphic.raycastTarget = false;
                }
                else ghostFogControl = GhostButton(R.GhostFog ? "FOG ON" : "FOG OFF", "Fog of war",
                    OzForkOptions.Accent, R.GhostFog, () => { R.GhostFog = !R.GhostFog; dirty = true; });
            }

            ghostCursor = Hud.Node("Tracked player cursor", ghostLayer).gameObject.AddComponent<RawImage>();
            ghostCursor.raycastTarget = false;
            ghostCursor.enabled = false;
            ghostBandWidth = 0;
            ghostTargetsAt = 0;
            TickGhostView();
        }

        static int TargetMask(RoomSnapshot.Human[] targets)
        {
            int mask = 0;
            foreach (var target in targets) mask |= 1 << target.Position;
            return mask;
        }

        RectTransform GhostButton(string caption, string name, Color tint, bool active, Action click, bool seat = false)
        {
            var rect = Hud.Node(name, ghostBand);
            var bg = Hud.Img(rect, "Button", Color.Lerp(OzUi.GM950, tint, active ? .45f : seat ? .24f : .07f));
            bg.rectTransform.Fill();
            bg.raycastTarget = true;
            Hud.Border(rect, active || seat ? tint : OzUi.Ink2, active ? 2f : 1f);
            var label = Hud.Label(rect, "Label", caption, seat ? 14 : 12, OzUi.Ink).FillParent(OzH.C);
            label.graphic.raycastTarget = false;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => click());
            return rect;
        }

        internal bool HideGhostLocalPointer(Vector2 pointer) => page == Page.Entry && ghostBand && ghostBand.gameObject.activeInHierarchy &&
            R.GhostFollowing && R.CurrentGhostView != null && Application.isFocused && !OpenZHud.Contains(ghostBand, pointer) &&
            pointer.x >= 0 && pointer.y >= 0 && pointer.x < Screen.width && pointer.y < Screen.height;

        internal void TickGhostView()
        {
            if (page != Page.Entry || !R.MatchGhost || !ghostLayer || !ghostLayer.gameObject.activeInHierarchy || !R.Game) return;
            var hud = OpenZHud.I;
            bool showControls = hud && !(hud.Pause && hud.Pause.IsOpen) && !OzConfirm.Blocks;
            ghostBand.gameObject.SetActiveFast(showControls);

            if (Time.unscaledTime >= ghostTargetsAt)
            {
                ghostTargetsAt = Time.unscaledTime + .25f;
                var targets = R.GhostTargets;
                if (TargetMask(targets) != ghostSeatMask || R.GhostFogAvailable != (ghostFogControl != null)) dirty = true;
                var tracked = Array.Find(targets, member => member.Position == R.GhostTarget);
                ghostStatus.Set(R.GhostFollowing && tracked != null ? "FOLLOWING " + tracked.Name : "FREE CAMERA");
            }
            if (ghostFogStatus != null)
                ghostFogStatus.Set(OzRadarLoss.I && OzRadarLoss.I.Fog ? "FOG ON" : "FOG OFF");
            if (showControls) LayoutGhostBand(hud);

            var view = R.CurrentGhostView;
            ghostCursor.enabled = ghostBand.gameObject.activeSelf && view != null && view.HasMouse;
            if (!ghostCursor.enabled) return;
            bool arrow = string.IsNullOrEmpty(view.Cursor) || view.Cursor == "arrow";
            int frame = (int)(Time.unscaledTime / .2f) & 3;
            ghostCursor.texture = OzSoftCursor.GhostTexture(view.Cursor, frame, view.Seat == (int)Team.Blue - 1);
            float size = 32 * Mathf.Clamp(OzText.UiScale, .5f, 2);
            ghostCursor.rectTransform.TL(ghostLayer.rect.width * view.MouseX - size * (arrow ? .125f : .5f),
                ghostLayer.rect.height * (1 - view.MouseY) - size * (arrow ? .09375f : .5f), size, size);
        }

        void LayoutGhostBand(OpenZHud hud)
        {
            const float left = 18, gap = 12, maxWidth = 470;
            var score = hud.GhostScorePanel;
            if (!score || !score.gameObject.activeInHierarchy) { ghostBand.gameObject.SetActiveFast(false); return; }
            score.GetWorldCorners(ghostCorners);
            float scale = Mathf.Max(.01f, canvas.scaleFactor);
            float room = ghostCorners[1].x / scale - gap - left;
            float y = (Screen.height - ghostCorners[1].y) / scale;
            float scoreHeight = (ghostCorners[1].y - ghostCorners[0].y) / scale;
            const float minWidth = 224;
            bool besideScore = room >= minWidth;
            float width = besideScore ? Mathf.Min(room, maxWidth) : Mathf.Min(maxWidth, ghostLayer.rect.width - left * 2);
            float height = Mathf.Clamp(scoreHeight, 92, 122);
            var pos = new Vector2(left, y + (besideScore ? 0 : scoreHeight + 8));
            hud.PlaceGhostWordmark(besideScore, ghostCorners[2].x, Screen.height - (pos.y + height) * scale);
            if (ghostBand.anchoredPosition != new Vector2(pos.x, -pos.y) || ghostBandWidth != width || ghostBandHeight != height)
                ghostBand.TL(pos.x, pos.y, width, height);
            if (ghostBandWidth == width && ghostBandHeight == height) return;
            ghostBandWidth = width;
            ghostBandHeight = height;

            const float inset = 14, spacing = 5, buttonHeight = 32;
            float freeWidth = width < 275 ? 61 : 88;
            float fogWidth = width < 275 ? 55 : 72;
            float rowY = height - 13 - buttonHeight;
            float extra = freeWidth + spacing + (ghostFogControl ? fogWidth + spacing : 0);
            bool sameRow = width >= inset * 2 + extra + ghostSeatButtons.Count * 32;
            float freeX = width - inset - extra + spacing;
            ghostFreeButton.TL(freeX, sameRow ? rowY : 8, freeWidth, buttonHeight);
            if (ghostFogControl) ghostFogControl.TL(freeX + freeWidth + spacing, sameRow ? rowY : 8, fogWidth, buttonHeight);
            ghostStatus.SetActive(sameRow);
            float space = width - inset * 2 - (sameRow ? extra : 0);
            float seatWidth = Mathf.Clamp((space - Mathf.Max(0, ghostSeatButtons.Count - 1) * spacing) / Mathf.Max(1, ghostSeatButtons.Count), 18, 40);
            for (int i = 0; i < ghostSeatButtons.Count; i++)
                ghostSeatButtons[i].TL(inset + i * (seatWidth + spacing), rowY, seatWidth, buttonHeight);
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

        void HandleGhostKeys()
        {
            var kb = Keyboard.current;
            if (page != Page.Entry || kb == null || !R.Playing || !R.MatchGhost || IsTyping || OzConfirm.Blocks ||
                OpenZHud.I && OpenZHud.I.Pause && OpenZHud.I.Pause.IsOpen) return;
            if (kb.qKey.wasPressedThisFrame) R.CycleGhostTarget(-1);
            if (kb.eKey.wasPressedThisFrame) R.CycleGhostTarget(1);
            if (kb.fKey.wasPressedThisFrame) R.SetGhostCameraMode(!R.GhostFollowing);
            if (kb.rKey.wasPressedThisFrame) R.SetGhostCameraMode(true);
        }
    }
}
