using UnityEngine;

namespace OpenZ
{
    public partial class FlagView
    {
        float appliedFlagSize = -1f;
        void ApplyFlagSize()
        {
            float size = OzMapReadabilityOptions.FlagSize;
            if (size == appliedFlagSize) return;
            foreach (var f in flags) if (f.root) f.root.localScale = Vector3.one * size;
            appliedFlagSize = size;
            RebuildLooks();
        }
        public bool FlagsUseSize(float size)
        {
            if (flags.Count == 0) return false;
            foreach (var f in flags) if (f.root && (f.root.localScale - Vector3.one * size).sqrMagnitude > .001f) return false;
            return true;
        }
    }
}
