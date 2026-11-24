// OpenZ (GPL-3.0) - version 1.4 MAYHEM difficulty: the battlefield screen's C key and the checks' hooks.
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The battlefield screen's DIFFICULTY row and the OPTIONS row have four buttons,
    /// EASY | MEDIUM | HARD | MAYHEM (UI/OpenZMainMenu.DeployBar.cs, UI/OpenZMainMenu.cs); the C key steps EASY, MEDIUM, HARD, MAYHEM, EASY.
    /// The choice is saved at once (OpenZDifficulty.Choice: oz_difficulty, plus oz_mayhem for MAYHEM). The rules and the computer play MAYHEM
    /// as HARD; the fog of war is on in a MAYHEM match only (View/OzFogOfWar.cs WhyOff). Checks: Editor/OzV14MayhemCheck.cs.
    /// </summary>
    public partial class OpenZMainMenu
    {
        /// <summary>The C key cycles the difficulties of the selected AI generation.</summary>
        void DifficultyKey()
        {
            var level = OzAiSoloChoice.Level;
            int index = System.Array.IndexOf(ZAiDifficulty.Modern, level);
            if (index >= 0) OzAiSoloChoice.Level = ZAiDifficulty.Modern[(index + 1) % ZAiDifficulty.Modern.Length];
            else SetDifficulty(OpenZDifficulty.Next(OpenZDifficulty.Choice));
            Sfx("ui_tab_switch");
        }

        // ---------------------------------------------------------------- proof hooks (Editor/OzV14MayhemCheck.cs, Play only)
        /// <summary>The C key's own step, as the key does it.</summary>
        public void ProofDifficultyKey() => DifficultyKey();
        /// <summary>The battlefield screen's lit DIFFICULTY button(s) by name ("CpuMayhem"; "" none; " + " between two).</summary>
        public string ProofDifficultyLit => Lit(("CpuEasy", cpuEasy), ("CpuMedium", cpuMedium), ("CpuHard", cpuNormal), ("CpuMayhem", cpuMayhem));
        /// <summary>The OPTIONS row's lit button(s) by name.</summary>
        public string ProofOptDifficultyLit => Lit(("OptEasy", optEasy), ("OptMedium", optMedium), ("OptHard", optNormal), ("OptMayhem", optMayhem));
        /// <summary>The DIFFICULTY block's hint as shown.</summary>
        public string ProofDifficultyHint => cpuHint != null ? cpuHint.text : "";
        /// <summary>DIFFICULTY button i of the battlefield screen (0 EASY, 1 MEDIUM, 2 HARD, 3 MAYHEM); null when it is not built.</summary>
        public RectTransform ProofDifficultyButton(int i)
        {
            var b = i == 0 ? cpuEasy : i == 1 ? cpuMedium : i == 2 ? cpuNormal : i == 3 ? cpuMayhem : null;
            return b != null ? b.Rt : null;
        }

        static string Lit(params (string name, HudButton b)[] bs)
        {
            var s = new StringBuilder();
            foreach (var (n, b) in bs) if (b != null && b.Selected) { if (s.Length > 0) s.Append(" + "); s.Append(n); }
            return s.ToString();
        }
    }
}
