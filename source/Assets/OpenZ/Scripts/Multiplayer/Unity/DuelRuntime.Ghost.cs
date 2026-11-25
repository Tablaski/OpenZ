using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelRuntime
    {
        [Serializable] internal sealed class GhostFrame
        {
            public int Seat, Revision, Factory, Portrait;
            public float X, Y, Z, FocusX, FocusZ, Height, Pitch, Yaw, Roll, Fov, MouseX, MouseY, OrderX, OrderY, OrderZ;
            public float BoxX, BoxY, OrderAge, ProductionX, ProductionY;
            public bool HasMouse, Box, PointerOverUi, ShowRoute, Queueing, AnchorProduction, FactoryList, ProductionRight, TilePressed;
            public string Cursor, OrderMark, ProductionTile;
            public int[] Selected;
            public GhostRoute[] Routes;
            public GhostGroup[] Groups;
        }

        [Serializable] internal sealed class GhostRoute
        {
            public int Unit;
            public Vector2Int[] Path, Waypoints, Draft;
            public bool HasDraftFrom, Patrolling;
            public Vector2Int DraftFrom, PatrolFrom, PatrolTo;
        }

        [Serializable] internal sealed class GhostGroup
        {
            public int Key;
            public int[] Units;
        }

        const int GhostMaxSelection = 384, GhostMaxRoutes = 96, GhostMaxPoints = 32, GhostMaxGroupMembers = 384;
        const int GhostMaxPayload = 40 * 1024;
        readonly Dictionary<int, int> ghostFollowers = new Dictionary<int, int>();
        readonly Dictionary<int, double> ghostNextFrameFrom = new Dictionary<int, double>();
        readonly List<int> ghostSelection = new List<int>(GhostMaxSelection);
        readonly List<GhostRoute> ghostRoutes = new List<GhostRoute>(GhostMaxRoutes);
        readonly List<GhostGroup> ghostGroups = new List<GhostGroup>(10);
        RtsCamera ghostCamera;
        Camera ghostLens;
        PlayerInput ghostPlayerInput;
        double ghostNextFrame, ghostNextSubscribe, ghostFrameAt;
        float ghostSavedFov;
        bool ghostWasFollowing;
        internal int GhostTarget { get; private set; } = -1;
        internal bool GhostFollowing { get; private set; }
        internal bool GhostFog { get; set; }
        internal GhostFrame GhostView { get; private set; }
        internal GhostFrame CurrentGhostView => MatchGhost && GhostFollowing && Now - ghostFrameAt < 1.5 ? GhostView : null;
        internal float GhostFrameAge => (float)Math.Max(0, Now - ghostFrameAt);
        internal bool GhostFogAvailable => Game && Game.World != null &&
            (OzRadarLoss.RuleFor(Game.World) is RadarLossRule.Fog or RadarLossRule.FogAndZoom);
        bool showingMatchGhost;
        bool LocalMatchGhost => RoomObserver || EliminatedMatchSeat(RoomPosition);
        internal bool MatchGhost => LocalMatchGhost && (!OpenZEnding.Running || RoomObserver && (!Game || !Game.World.GameOver));

        internal void ReleaseGhostCameraForEnding()
        {
            GhostFollowing = false;
            GhostView = null;
            if (ghostWasFollowing && ghostLens) ghostLens.fieldOfView = ghostSavedFov;
            ghostWasFollowing = false;
            ghostPlayerInput?.ApplyGhostSelection(null);
        }

        bool EliminatedMatchSeat(int seat) => seat >= 0 && Playing && Game &&
            Game.World.HasIndividualDefeatSequence && Game.World.SkirmishEliminated((Team)(seat + 1));

        bool IsMatchGhost(int id) => IsRoomGhost(id) || room?.Members.Any(member =>
            member.Id == RoomGuest(id) && EliminatedMatchSeat(member.Seat.Position)) == true;

        internal static Team GhostVisionTeam
        {
            get
            {
                if (!I || !I.MatchGhost || !I.Playing || (!I.GhostFollowing && !I.GhostFog)) return Team.Neutral;
                int seat = I.GhostTarget;
                if (seat < 0 || !I.GhostFollowing && I.EliminatedMatchSeat(seat)) seat = I.GhostTargets.FirstOrDefault()?.Position ?? -1;
                return seat < 0 ? Team.Neutral : (Team)(seat + 1);
            }
        }

        bool ActiveGhostSeat(int seat) => seat >= 0 && seat < 8 &&
            (!Game || !Game.World.SkirmishEliminated((Team)(seat + 1)));

        internal RoomSnapshot.Human[] GhostTargets
        {
            get
            {
                var humans = IsHost
                    ? room?.Members.Select(member => RoomSnapshot.Human.FromMember(member, RoomHostId, RoomHostId, room.RosterId(member.Id)))
                    : JoinedRoom?.Humans;
                return humans?.Where(member => !member.Observer && ActiveGhostSeat(member.Position))
                    .OrderBy(member => member.Position).ToArray() ?? Array.Empty<RoomSnapshot.Human>();
            }
        }

        internal void FollowGhostTarget(int seat)
        {
            if (!MatchGhost || !GhostTargets.Any(member => member.Position == seat)) return;
            if (GhostTarget != seat || !GhostFollowing) GhostView = null;
            GhostTarget = seat;
            GhostFollowing = true;
            ghostNextSubscribe = 0;
            Menus.Refresh();
        }

        internal void SetGhostCameraMode(bool follow)
        {
            if (!MatchGhost) return;
            if (follow)
            {
                var targets = GhostTargets;
                var target = targets.FirstOrDefault(member => member.Position == GhostTarget) ?? targets.FirstOrDefault();
                if (target != null) FollowGhostTarget(target.Position);
                return;
            }
            GhostFollowing = false;
            GhostView = null;
            ghostNextSubscribe = 0;
            Menus.Refresh();
        }

        internal void CycleGhostTarget(int direction)
        {
            var targets = GhostTargets;
            if (targets.Length == 0) return;
            int index = GhostFollowing ? Array.FindIndex(targets, human => human.Position == GhostTarget) : -1;
            int next = index < 0 ? (direction < 0 ? targets.Length - 1 : 0) : (index + direction + targets.Length) % targets.Length;
            FollowGhostTarget(targets[next].Position);
        }

        void LateUpdate()
        {
            if (!Playing || !Game || RoomTransport == null) return;
            bool ghost = MatchGhost;
            if (ghost != showingMatchGhost)
            {
                showingMatchGhost = ghost;
                Menus.Refresh();
            }
            if (ghost)
            {
                UpdateGhostFollowing();
                return;
            }
            if (RoomPosition < 0 || Game.World.SkirmishEliminated(LocalTeam)) return;
            bool observers = IsHost
                ? room.Members.Any(member => member.Seat.Role == MatchSeatRole.Observer || EliminatedMatchSeat(member.Seat.Position))
                : JoinedRoom?.Humans.Any(member => member.Observer || EliminatedMatchSeat(member.Position)) == true;
            if (!observers) return;
            if (!ReadyGhostCamera()) return;
            if (Now < ghostNextFrame || QueuedSendBytes > 32768) return;
            ghostNextFrame = Now + .065;
            int seat = RoomPosition;
            var pose = ghostLens.transform;
            var look = pose.rotation.eulerAngles;
            var focus = ghostCamera.focus;
            var pointer = Mouse.current;
            var mouse = pointer?.position.ReadValue() ?? Vector2.zero;
            var hud = OpenZHud.I;
            var production = hud ? hud.Production : null;
            var productionPosition = production ? production.GhostWindowTopLeft : Vector2.zero;
            var order = ghostPlayerInput ? ghostPlayerInput.MarkerPos : Vector3.zero;
            float now = Time.unscaledTime;
            float orderAge = ghostPlayerInput ? Mathf.Clamp(now - ghostPlayerInput.MarkerTime, 0, 4) : 4;
            bool queuing = ghostPlayerInput && ((Keyboard.current?.shiftKey.isPressed ?? false) || ghostPlayerInput.Pending.Count > 0);
            bool showRoute = ghostPlayerInput && (queuing || now - Mathf.Max(ghostPlayerInput.SelectTime, ghostPlayerInput.MarkerTime) < 3);
            Vector2 boxStart = default;
            bool box = ghostPlayerInput && ghostPlayerInput.TryGetSelectionBox(out boxStart);
            ghostSelection.Clear();
            ghostRoutes.Clear();
            ghostGroups.Clear();
            int pointBudget = 650;
            if (ghostPlayerInput)
            {
                foreach (var selected in ghostPlayerInput.Selection)
                {
                    if (ghostSelection.Count == GhostMaxSelection) break;
                    if (selected == null || selected.Destroyed || selected.Owner != LocalTeam) continue;
                    ghostSelection.Add(selected.Id);
                    if ((!showRoute && !selected.Patrolling) || ghostRoutes.Count == GhostMaxRoutes || pointBudget == 0) continue;
                    ghostPlayerInput.Pending.TryGetValue(selected, out var draft);
                    if (!selected.Patrolling && !selected.IsMoving && selected.Waypoints.Count == 0 && (draft == null || draft.Count == 0)) continue;
                    // Keep the draft visible when the frame reaches its route budget.
                    var draftPoints = CaptureGhostWaypoints(draft, ref pointBudget);
                    var waypointPoints = CaptureGhostWaypoints(selected.Waypoints, ref pointBudget);
                    var path = new List<Vector2Int>();
                    if (selected.IsMoving && pointBudget > 0)
                    {
                        path.Add(new Vector2Int(selected.CurrentTarget.x, selected.CurrentTarget.y));
                        pointBudget--;
                    }
                    if (selected.PathPoints != null)
                        foreach (var p in selected.PathPoints)
                        {
                            if (pointBudget == 0 || path.Count == GhostMaxPoints) break;
                            path.Add(new Vector2Int(p.x, p.y));
                            pointBudget--;
                        }
                    bool hasDraftFrom = OzShiftAppend.PreviewFrom(selected, out int qx, out int qy);
                    ghostRoutes.Add(new GhostRoute { Unit = selected.Id, Path = path.ToArray(), Waypoints = waypointPoints,
                        Draft = draftPoints, HasDraftFrom = hasDraftFrom, DraftFrom = new Vector2Int(qx, qy),
                        Patrolling = selected.Patrolling, PatrolFrom = new Vector2Int(selected.PatrolAx, selected.PatrolAy),
                        PatrolTo = new Vector2Int(selected.PatrolBx, selected.PatrolBy) });
                }
                if (hud?.Bridge?.Groups is { } groups)
                {
                    int members = GhostMaxGroupMembers;
                    foreach (var group in groups)
                    {
                        if (group.Key is < 0 or > 9 || group.Value == null || members == 0) continue;
                        var units = group.Value.Where(unit => unit != null && !unit.Destroyed && unit.Owner == LocalTeam)
                            .Take(members).Select(unit => unit.Id).ToArray();
                        if (units.Length == 0) continue;
                        ghostGroups.Add(new GhostGroup { Key = group.Key, Units = units });
                        members -= units.Length;
                    }
                }
            }
            var frame = new GhostFrame
            {
                Seat = seat, Revision = loadedRoomRevision, Factory = production?.Building?.Id ?? -1, Portrait = hud?.HudUnit?.Id ?? -1,
                OrderX = order.x, OrderY = order.y, OrderZ = order.z, OrderAge = orderAge, ShowRoute = showRoute, Queueing = queuing,
                X = pose.position.x, Y = pose.position.y, Z = pose.position.z, Pitch = look.x, Yaw = look.y, Roll = look.z,
                FocusX = focus.x, FocusZ = focus.z, Height = ghostCamera.height, Fov = ghostLens.fieldOfView,
                HasMouse = pointer != null && Cursor.visible && mouse.x >= 0 && mouse.y >= 0 && mouse.x < Screen.width && mouse.y < Screen.height,
                MouseX = GhostX(mouse.x), MouseY = GhostY(mouse.y), Box = box, BoxX = GhostX(boxStart.x), BoxY = GhostY(boxStart.y),
                PointerOverUi = hud && hud.PointerOverUi, Cursor = hud?.Cursors?.Current, OrderMark = hud?.Cursors?.OrderMark,
                AnchorProduction = production && production.anchorToBuilding, ProductionTile = production?.GhostHoveredTile,
                ProductionX = productionPosition.x, ProductionY = productionPosition.y,
                ProductionRight = production && production.GhostWindowRight, TilePressed = production && production.GhostTilePressed,
                FactoryList = hud && hud.GhostFactoryListOpen,
                Selected = ghostSelection.ToArray(), Routes = ghostRoutes.ToArray(), Groups = ghostGroups.ToArray()
            };
            if (IsHost) ForwardGhostFrame(frame);
            else SendGhostPacketToHost(DuelMessage.RoomGhostView, frame);
        }

        static float GhostX(float value) => Mathf.Clamp01(value / Math.Max(1, Screen.width));
        static float GhostY(float value) => Mathf.Clamp01(value / Math.Max(1, Screen.height));

        static Vector2Int[] CaptureGhostWaypoints(IReadOnlyList<ZWaypoint> source, ref int budget)
        {
            int count = Math.Min(budget, Math.Min(source?.Count ?? 0, GhostMaxPoints));
            var result = new Vector2Int[count];
            for (int i = 0; i < count; i++) result[i] = new Vector2Int(source[i].x, source[i].y);
            budget -= count;
            return result;
        }

        bool ReadyGhostCamera()
        {
            if (!ghostCamera)
            {
                ghostCamera = FindFirstObjectByType<RtsCamera>();
                ghostLens = ghostCamera ? ghostCamera.GetComponent<Camera>() : null;
                ghostPlayerInput = FindFirstObjectByType<PlayerInput>();
            }
            return ghostCamera && ghostLens;
        }

        void UpdateGhostFollowing()
        {
            if (!ReadyGhostCamera()) return;
            if (GhostFollowing)
            {
                var targets = GhostTargets;
                if (!targets.Any(member => member.Position == GhostTarget))
                {
                    if (targets.Length == 0) SetGhostCameraMode(false);
                    else FollowGhostTarget(targets[0].Position);
                }
            }
            if (Now >= ghostNextSubscribe)
            {
                ghostNextSubscribe = Now + 1;
                if (!IsHost) SendGhostPacketToHost(DuelMessage.RoomGhostFollow, new GhostRequest { Seat = GhostFollowing ? GhostTarget : -1,
                    Revision = loadedRoomRevision });
            }
            var view = CurrentGhostView;
            bool tracking = view != null;
            if (tracking)
            {
                if (!ghostWasFollowing) ghostSavedFov = ghostLens.fieldOfView;
                ghostCamera.FocusGhostView(new Vector3(view.FocusX, 0, view.FocusZ), view.Height);
                ghostLens.fieldOfView = view.Fov;
                ghostCamera.transform.SetPositionAndRotation(new Vector3(view.X, view.Y, view.Z), Quaternion.Euler(view.Pitch, view.Yaw, view.Roll));
            }
            else if (ghostWasFollowing)
            {
                ghostLens.fieldOfView = ghostSavedFov;
                ghostCamera.Place();
            }
            ghostWasFollowing = tracking;
            ghostPlayerInput?.ApplyGhostSelection(view);
            Menus.TickGhostView();
        }

        [Serializable] sealed class GhostRequest { public int Seat, Revision; }

        void SendGhostPacketToHost(DuelMessage kind, object value)
        {
            RoomTransport.Remotes.FirstOrDefault(link => link.Id == 1)?.Send(
                new DuelPacket(kind, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(value))));
        }

        void ReadGhostPacket(SkirmishOnline.Link sender, DuelPacket packet)
        {
            if (!Playing || !Game || packet.Payload.Length == 0 || packet.Payload.Length > GhostMaxPayload) return;
            if (packet.Kind == DuelMessage.RoomGhostFollow)
            {
                if (!IsHost || !IsMatchGhost(sender.Id)) return;
                var request = ReadJson<GhostRequest>(packet);
                if (request == null || request.Revision != loadedRoomRevision) return;
                if (request.Seat < 0) ghostFollowers.Remove(sender.Id);
                else if (ActiveGhostSeat(request.Seat) &&
                    room.Members.Any(member => member.Seat.MayUsePlayerCommands && member.Seat.Position == request.Seat))
                    ghostFollowers[sender.Id] = request.Seat;
                return;
            }
            if (IsHost)
            {
                if (IsMatchGhost(sender.Id) || (ghostFollowers.Count == 0 && !(MatchGhost && GhostFollowing)) ||
                    ghostNextFrameFrom.TryGetValue(sender.Id, out double next) && Now < next) return;
                ghostNextFrameFrom[sender.Id] = Now + .045;
            }
            else if (sender.Id != 1 || !MatchGhost || !GhostFollowing) return;
            var frame = ReadJson<GhostFrame>(packet);
            if (!ValidGhostFrame(frame)) return;
            if (IsHost)
            {
                var participant = room.Members.FirstOrDefault(member => member.Id == RoomGuest(sender.Id));
                if (participant.Seat.MayUsePlayerCommands && frame.Seat == participant.Seat.Position)
                    ForwardGhostFrame(frame);
            }
            else if (GhostTarget == frame.Seat)
            {
                GhostView = frame;
                ghostFrameAt = Now;
            }
        }

        bool ValidGhostFrame(GhostFrame frame) => frame != null && frame.Revision == loadedRoomRevision &&
            frame.Seat >= 0 && frame.Seat < 8 && frame.Selected?.Length <= GhostMaxSelection &&
            frame.Routes?.Length <= GhostMaxRoutes && frame.Routes.All(r => r != null && ValidGhostPoints(r.Path) &&
                ValidGhostPoints(r.Waypoints) && ValidGhostPoints(r.Draft) && (!r.HasDraftFrom || ValidGhostPoint(r.DraftFrom)) &&
                (!r.Patrolling || ValidGhostPoint(r.PatrolFrom) && ValidGhostPoint(r.PatrolTo))) &&
            frame.Groups?.Length <= 10 && frame.Groups.All(g => g != null && g.Key is >= 0 and < 10 && g.Units?.Length <= GhostMaxGroupMembers) &&
            frame.Groups.Sum(g => g.Units.Length) <= GhostMaxGroupMembers &&
            UnitInterval(frame.MouseX) && UnitInterval(frame.MouseY) && UnitInterval(frame.BoxX) && UnitInterval(frame.BoxY) &&
            UnitInterval(frame.ProductionX) && UnitInterval(frame.ProductionY) &&
            frame.OrderAge >= 0 && frame.OrderAge <= 4 && (frame.Cursor?.Length ?? 0) <= 16 && (frame.OrderMark?.Length ?? 0) <= 16 &&
            (frame.ProductionTile?.Length ?? 0) <= 40 &&
            frame.Fov >= 10 && frame.Fov <= 120 && frame.Height > 0 && frame.Height < 1000 &&
            FiniteCoordinate(frame.X) && FiniteCoordinate(frame.Y) && FiniteCoordinate(frame.Z) &&
            FiniteCoordinate(frame.FocusX) && FiniteCoordinate(frame.FocusZ) && FiniteCoordinate(frame.Pitch) &&
            FiniteCoordinate(frame.Yaw) && FiniteCoordinate(frame.Roll) && FiniteCoordinate(frame.OrderX) &&
            FiniteCoordinate(frame.OrderY) && FiniteCoordinate(frame.OrderZ);

        static bool FiniteCoordinate(float value) => value > -10000 && value < 10000;
        static bool UnitInterval(float value) => value >= 0 && value <= 1;
        bool ValidGhostPoints(Vector2Int[] points) => points?.Length <= GhostMaxPoints && points.All(ValidGhostPoint);
        bool ValidGhostPoint(Vector2Int point) => point.x >= -256 && point.y >= -256 &&
            point.x <= Game.World.Map.WidthPix + 256 && point.y <= Game.World.Map.HeightPix + 256;

        void ForwardGhostFrame(GhostFrame frame)
        {
            if (Game.World.SkirmishEliminated((Team)(frame.Seat + 1))) return;
            if (MatchGhost && GhostFollowing && GhostTarget == frame.Seat)
            {
                GhostView = frame;
                ghostFrameAt = Now;
            }
            var audience = RoomTransport.Remotes.Where(link => link.Ready && ghostFollowers.TryGetValue(link.Id, out int target) &&
                target == frame.Seat && IsMatchGhost(link.Id)).ToArray();
            if (audience.Length == 0) return;
            var packet = new DuelPacket(DuelMessage.RoomGhostView, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame)));
            foreach (var link in audience) link.Send(packet);
        }

        void ResetGhostView()
        {
            showingMatchGhost = false;
            GhostTarget = -1;
            GhostFollowing = false;
            GhostFog = false;
            GhostView = null;
            ghostFollowers.Clear();
            ghostNextFrameFrom.Clear();
            ghostCamera = null;
            ghostLens = null;
            ghostPlayerInput = null;
            ghostNextFrame = ghostNextSubscribe = ghostFrameAt = 0;
            ghostWasFollowing = false;
        }
    }
}
