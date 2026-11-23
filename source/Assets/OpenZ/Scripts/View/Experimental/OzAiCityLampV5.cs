// OpenZ-VK (GPL-3.0).
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Experimental
{
    // A view-only lamp. It never blocks movement or registers as a target.
    sealed class OzAiCityLampV5 : MonoBehaviour
    {
        public Vector3 Anchor { private get; set; }
        float nextCheck;

        void Update()
        {
            var game = ZGame.I;
            if (!game || game.paused || game.World == null || Time.time < nextCheck) return;
            nextCheck = Time.time + .10f;
            var px = ZGame.WorldToPx(Anchor);
            foreach (var unit in game.World.Objects)
            {
                if (unit.Destroyed || unit.Kind != ZKind.Vehicle ||
                    !(unit.Type is "light" or "medium" or "heavy" || ZBigTanks.Is(unit.Type))) continue;
                if (px.x < unit.LocX || px.x >= unit.LocX + unit.WidthPix ||
                    px.y < unit.LocY || px.y >= unit.LocY + unit.HeightPix) continue;
                transform.RotateAround(Anchor, Vector3.forward, 83);
                enabled = false;
                return;
            }
        }
    }
}
