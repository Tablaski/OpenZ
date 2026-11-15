using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        void AddBehaviorOptions(RectTransform p)
        {
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
