using UnityEngine;

namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        void AddBehaviorOptions(RectTransform p)
        {
            AddCycle(p, toggles.Count, OzForkOptions.ChatLabel, () => OzForkOptions.ChatName, OzForkOptions.BindChat);
            AddCycle(p, toggles.Count, OzMapReadabilityOptions.FlagLabel, () => OzMapReadabilityOptions.FlagName, OzMapReadabilityOptions.StepFlag);
            AddCycle(p, toggles.Count, OzMapReadabilityOptions.BorderLabel, () => OzMapReadabilityOptions.BorderName, OzMapReadabilityOptions.StepBorder);
            AddCycle(p, toggles.Count, OzMapReadabilityOptions.RadarSweepLabel, () => OzMapReadabilityOptions.RadarSweepName, OzMapReadabilityOptions.StepRadarSweep);
        }
    }
    public partial class OpenZPauseMenu
    {
        void AddBehaviorOptions(RectTransform p)
        {
            if (Multiplayer.DuelRuntime.Active)
            {
                AddToggle(p, toggles.Count, OzForkOptions.ChatLabel, null, null, true);
                var chatKey = toggles[toggles.Count - 1];
                chatKey.row.OnClick = c => OzForkOptions.BindChat();
                chatKey.row.OnRightClick = c => OzForkOptions.BindChat();
            }
            AddGraphicsCycle(p, OzMapReadabilityOptions.FlagLabel, () => OzMapReadabilityOptions.FlagName, OzMapReadabilityOptions.StepFlag);
            AddToggle(p, toggles.Count, OzMapReadabilityOptions.BorderLabel, null, null, true);
            var borderSize = toggles[toggles.Count - 1];
            borderSize.row.OnClick = c => OzMapReadabilityOptions.StepBorder(c.right || c.shift ? -1 : Arrow(borderSize));
            borderSize.row.OnRightClick = c => OzMapReadabilityOptions.StepBorder(-1);
            AddGraphicsCycle(p, OzMapReadabilityOptions.RadarSweepLabel, () => OzMapReadabilityOptions.RadarSweepName, OzMapReadabilityOptions.StepRadarSweep);
        }
    }
}
