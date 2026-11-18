// OpenZ-VK (GPL-3.0).
using System;
using UnityEngine;

namespace OpenZ.Experimental
{
    /// <summary>Shared OpenZ bridge stages for maps whose reference scene does not contain the full biome kit.</summary>
    public sealed class OzAiBridgeKit : ScriptableObject
    {
        public GameObject[] prefabs;

        internal static GameObject Find(string name)
        {
            var kit = Resources.Load<OzAiBridgeKit>("OpenZ/AiMaps/BridgePrefabs");
            return kit ? Array.Find(kit.prefabs, prefab => prefab && prefab.name == name) : null;
        }
    }
}
