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
            for (int i = 0; i < 2; i++)
            {
                var family = (ZBehaviorFamily)i;
                AddCycle(p, toggles.Count, i == 0 ? OzBehaviorLibrary.TanksLabel : OzBehaviorLibrary.RobotsLabel,
                    () => OzBehaviorLibrary.SelectedName(family), _ => OzBehaviorOptions.Show(family));
                var t = toggles[toggles.Count - 1];
                t.value.At(OptW - 2 * OzLayout.OptX - 16, OzLayout.OptToggleH * .5f, OzH.R, true, OptW - 2 * OzLayout.OptX - 190);
            }
        }
    }
    public partial class OpenZPauseMenu
    {
        void AddBehaviorOptions(RectTransform p)
        {
            AddToggle(p, toggles.Count, OzForkOptions.ChatLabel, null, null, true);
            var chatKey = toggles[toggles.Count - 1];
            chatKey.row.OnClick = c => OzForkOptions.BindChat();
            chatKey.row.OnRightClick = c => OzForkOptions.BindChat();
            AddToggle(p, toggles.Count, OzMapReadabilityOptions.FlagLabel, null, null, true);
            var flagSize = toggles[toggles.Count - 1];
            flagSize.row.OnClick = c => OzMapReadabilityOptions.StepFlag(c.right || c.shift ? -1 : Arrow(flagSize));
            flagSize.row.OnRightClick = c => OzMapReadabilityOptions.StepFlag(-1);
            AddToggle(p, toggles.Count, OzMapReadabilityOptions.BorderLabel, null, null, true);
            var borderSize = toggles[toggles.Count - 1];
            borderSize.row.OnClick = c => OzMapReadabilityOptions.StepBorder(c.right || c.shift ? -1 : Arrow(borderSize));
            borderSize.row.OnRightClick = c => OzMapReadabilityOptions.StepBorder(-1);
            for (int i = 0; i < 2; i++)
            {
                var family = (ZBehaviorFamily)i;
                AddToggle(p, toggles.Count, i == 0 ? OzBehaviorLibrary.TanksLabel : OzBehaviorLibrary.RobotsLabel, null, null, true);
                var t = toggles[toggles.Count - 1];
                t.row.OnClick = _ => OzBehaviorOptions.Show(family);
                t.value.At(OzLayout.PauseW - 2 * OzLayout.OptX - 16, OzLayout.OptToggleH * .5f, OzH.R, true, OzLayout.PauseW - 2 * OzLayout.OptX - 190);
            }
        }
    }
}
