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
            public int Seat, Revision, Click, MouseButton, Order, Factory;
            public float X, Y, Z, FocusX, FocusZ, Height, Pitch, Yaw, Fov, MouseX, MouseY, OrderX, OrderZ;
            public bool HasMouse;
            public int[] Selected;
        }

        readonly Dictionary<int, int> ghostFollowers = new Dictionary<int, int>();
        readonly Dictionary<int, double> ghostNextFrameFrom = new Dictionary<int, double>();
        readonly List<int> ghostSelection = new List<int>(32);
        RtsCamera ghostCamera;
        Camera ghostLens;
        PlayerInput ghostPlayerInput;
        double ghostNextFrame, ghostNextSubscribe, ghostFrameAt;
        int ghostClick, ghostMouseButton, ghostOrder;
        float ghostLastMarker = -10;
        float ghostSavedFov;
        bool ghostWasFollowing;
        internal int GhostTarget { get; private set; } = -1;
        internal bool GhostFollowing { get; private set; }
        internal bool GhostFog { get; set; } = true;
        internal GhostFrame GhostView { get; private set; }
        internal static Team GhostVisionTeam => I && I.RoomObserver && I.Playing && I.GhostFollowing && I.GhostFog && I.GhostTarget >= 0
            ? (Team)(I.GhostTarget + 1) : Team.Neutral;

        internal RoomSnapshot.Human[] GhostTargets => JoinedRoom?.Humans.Where(member => member.Position >= 0 && !member.Observer &&
            (!Game || !Game.World.SkirmishEliminated((Team)(member.Position + 1)))).OrderBy(member => member.Position).ToArray()
            ?? Array.Empty<RoomSnapshot.Human>();

        internal void FollowGhostTarget(int seat)
        {
            if (!RoomObserver || !GhostTargets.Any(member => member.Position == seat)) return;
            if (GhostTarget != seat) GhostView = null;
            GhostTarget = seat;
            GhostFollowing = true;
            ghostNextSubscribe = 0;
            Menus.Refresh();
        }

        internal void SetGhostCameraMode(bool follow)
        {
            if (!RoomObserver) return;
            if (follow)
            {
                var target = GhostTargets.FirstOrDefault(member => member.Position == GhostTarget) ?? GhostTargets.FirstOrDefault();
                if (target != null) FollowGhostTarget(target.Position);
                return;
            }
            GhostFollowing = false;
            ghostNextSubscribe = 0;
            Menus.Refresh();
        }

        internal void CycleGhostTarget(int direction)
        {
            var targets = GhostTargets;
            if (targets.Length == 0) return;
            int index = Array.FindIndex(targets, human => human.Position == GhostTarget);
            FollowGhostTarget(targets[index < 0 ? 0 : (index + direction + targets.Length) % targets.Length].Position);
        }

        void LateUpdate()
        {
            if (!Playing || !Game || RoomTransport == null) return;
            if (RoomObserver)
            {
                UpdateGhostFollowing();
                return;
            }
            if (RoomPosition < 0 || Game.World.SkirmishEliminated(LocalTeam)) return;
            bool observers = IsHost ? room.Members.Any(member => member.Seat.Role == MatchSeatRole.Observer) :
                JoinedRoom.Humans.Any(member => member.Observer);
            if (!observers) return;
            if (!ReadyGhostCamera()) return;
            var pointer = Mouse.current;
            if (pointer != null && (pointer.leftButton.wasPressedThisFrame || pointer.rightButton.wasPressedThisFrame))
            {
                ghostClick++;
                ghostMouseButton = pointer.rightButton.wasPressedThisFrame ? 2 : 1;
            }
            if (ghostPlayerInput && ghostPlayerInput.MarkerTime != ghostLastMarker)
            {
                ghostOrder++;
                ghostLastMarker = ghostPlayerInput.MarkerTime;
            }
            if (Now < ghostNextFrame || QueuedSendBytes > 32768) return;
            ghostNextFrame = Now + .10;
            int seat = RoomPosition;
            var pose = ghostLens.transform;
            var look = pose.rotation.eulerAngles;
            var focus = ghostCamera.focus;
            var mouse = pointer?.position.ReadValue() ?? Vector2.zero;
            var order = ghostPlayerInput ? ghostPlayerInput.MarkerPos : Vector3.zero;
            ghostSelection.Clear();
            if (ghostPlayerInput)
                foreach (var selected in ghostPlayerInput.Selection)
                {
                    if (ghostSelection.Count == 32) break;
                    if (selected != null && !selected.Destroyed && selected.Owner == LocalTeam) ghostSelection.Add(selected.Id);
                }
            var factory = OpenZHud.I && OpenZHud.I.Production ? OpenZHud.I.Production.Building : null;
            var frame = new GhostFrame
            {
                Seat = seat, Revision = loadedRoomRevision, Click = ghostClick, MouseButton = ghostMouseButton,
                Order = ghostOrder, OrderX = order.x, OrderZ = order.z, Factory = factory?.Id ?? -1,
                X = pose.position.x, Y = pose.position.y, Z = pose.position.z, Pitch = look.x, Yaw = look.y,
                FocusX = focus.x, FocusZ = focus.z, Height = ghostCamera.height, Fov = ghostLens.fieldOfView,
                HasMouse = pointer != null && Screen.width > 0 && Screen.height > 0,
                MouseX = Mathf.Clamp01(mouse.x / Math.Max(1, Screen.width)), MouseY = Mathf.Clamp01(mouse.y / Math.Max(1, Screen.height)),
                Selected = ghostSelection.ToArray()
            };
            if (IsHost) ForwardGhostFrame(frame);
            else SendGhostPacketToHost(DuelMessage.RoomGhostView, frame);
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
            if (GhostFollowing && !GhostTargets.Any(member => member.Position == GhostTarget))
            {
                var next = GhostTargets.FirstOrDefault();
                if (next == null) SetGhostCameraMode(false);
                else FollowGhostTarget(next.Position);
            }
            if (Now >= ghostNextSubscribe)
            {
                ghostNextSubscribe = Now + 1;
                SendGhostPacketToHost(DuelMessage.RoomGhostFollow, new GhostRequest { Seat = GhostFollowing ? GhostTarget : -1,
                    Revision = loadedRoomRevision });
            }
            bool tracking = GhostFollowing && GhostView != null && Now - ghostFrameAt < 1.5;
            if (tracking)
            {
                if (!ghostWasFollowing) ghostSavedFov = ghostLens.fieldOfView;
                var view = GhostView;
                ghostCamera.FocusGhostView(new Vector3(view.FocusX, 0, view.FocusZ), view.Height);
                ghostLens.fieldOfView = view.Fov;
                ghostCamera.transform.SetPositionAndRotation(new Vector3(view.X, view.Y, view.Z), Quaternion.Euler(view.Pitch, view.Yaw, 0));
            }
            else if (ghostWasFollowing)
            {
                ghostLens.fieldOfView = ghostSavedFov;
                ghostCamera.Place();
            }
            ghostWasFollowing = tracking;
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
            if (!Playing || !Game || packet.Payload.Length == 0 || packet.Payload.Length > 4096) return;
            if (packet.Kind == DuelMessage.RoomGhostFollow)
            {
                if (!IsHost || !IsRoomGhost(sender.Id)) return;
                var request = ReadJson<GhostRequest>(packet);
                if (request == null || request.Revision != loadedRoomRevision) return;
                if (request.Seat < 0) ghostFollowers.Remove(sender.Id);
                else if (room.Members.Any(member => member.Seat.MayUsePlayerCommands && member.Seat.Position == request.Seat))
                    ghostFollowers[sender.Id] = request.Seat;
                return;
            }
            if (IsHost)
            {
                if (IsRoomGhost(sender.Id) || !ghostFollowers.Any() ||
                    ghostNextFrameFrom.TryGetValue(sender.Id, out double next) && Now < next) return;
                ghostNextFrameFrom[sender.Id] = Now + .075;
            }
            else if (sender.Id != 1 || !RoomObserver || !GhostFollowing) return;
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
            frame.Seat >= 0 && frame.Seat < 8 && frame.Selected?.Length <= 32 &&
            frame.MouseX >= 0 && frame.MouseX <= 1 && frame.MouseY >= 0 && frame.MouseY <= 1 &&
            frame.Fov >= 10 && frame.Fov <= 120 && frame.Height > 0 && frame.Height < 1000 &&
            FiniteCoordinate(frame.X) && FiniteCoordinate(frame.Y) && FiniteCoordinate(frame.Z) &&
            FiniteCoordinate(frame.FocusX) && FiniteCoordinate(frame.FocusZ) && FiniteCoordinate(frame.Pitch) &&
            FiniteCoordinate(frame.Yaw) && FiniteCoordinate(frame.OrderX) && FiniteCoordinate(frame.OrderZ);

        static bool FiniteCoordinate(float value) => value > -10000 && value < 10000;

        void ForwardGhostFrame(GhostFrame frame)
        {
            if (Game.World.SkirmishEliminated((Team)(frame.Seat + 1))) return;
            var audience = RoomTransport.Remotes.Where(link => link.Ready && ghostFollowers.TryGetValue(link.Id, out int target) &&
                target == frame.Seat && IsRoomGhost(link.Id)).ToArray();
            if (audience.Length == 0) return;
            var packet = new DuelPacket(DuelMessage.RoomGhostView, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame)));
            foreach (var link in audience) link.Send(packet);
        }

        void ResetGhostView()
        {
            GhostTarget = -1;
            GhostFollowing = false;
            GhostView = null;
            ghostFollowers.Clear();
            ghostNextFrameFrom.Clear();
            ghostCamera = null;
            ghostLens = null;
            ghostPlayerInput = null;
            ghostClick = ghostMouseButton = ghostOrder = 0;
            ghostNextFrame = ghostNextSubscribe = ghostFrameAt = 0;
            ghostLastMarker = -10;
            ghostWasFollowing = false;
        }
    }
}
