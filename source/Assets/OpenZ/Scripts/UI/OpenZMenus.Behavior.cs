using OpenZ.Sim;
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
            AddToggle(p, toggles.Count, OzMapReadabilityOptions.FlagLabel, null, null, true);
            var flagSize = toggles[toggles.Count - 1];
            flagSize.row.OnClick = c => OzMapReadabilityOptions.StepFlag(c.right || c.shift ? -1 : Arrow(flagSize));
            flagSize.row.OnRightClick = c => OzMapReadabilityOptions.StepFlag(-1);
            AddToggle(p, toggles.Count, OzMapReadabilityOptions.BorderLabel, null, null, true);
            var borderSize = toggles[toggles.Count - 1];
            borderSize.row.OnClick = c => OzMapReadabilityOptions.StepBorder(c.right || c.shift ? -1 : Arrow(borderSize));
            borderSize.row.OnRightClick = c => OzMapReadabilityOptions.StepBorder(-1);
        }
    }
}
