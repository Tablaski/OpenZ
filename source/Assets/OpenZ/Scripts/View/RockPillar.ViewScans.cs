// OpenZ 1.4 speed-up 4 (View/OzViewScans.cs): every RockPillar in its registry, so RockSeeThru counts the rocks every 3 s without a search
// of the whole scene. Awake runs even for a disabled component, as the search counts a disabled one too; OnDestroy runs when it goes.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public partial class RockPillar
    {
        void Awake() { OzLiveSet<RockPillar>.Add(this); }
        void OnDestroy() { OzLiveSet<RockPillar>.Remove(this); }
    }

    /// <summary>One Unity update for all active pillars, with the original per-pillar health check interval.</summary>
    internal sealed class RockPillarHealth : MonoBehaviour
    {
        static RockPillarHealth instance;
        readonly LinkedList<RockPillar> pillars = new LinkedList<RockPillar>();
        float nextPoll;

        internal static void Register(LinkedListNode<RockPillar> node)
        {
            if (!instance)
            {
                var host = new GameObject("Rock Pillar Health");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<RockPillarHealth>();
            }
            instance.pillars.AddLast(node);
            instance.nextPoll = 0f;
        }

        void Update()
        {
            float now = Time.time;
            if (now < nextPoll || !ZGame.I || ZGame.I.World == null) return;
            var world = ZGame.I.World;
            nextPoll = float.PositiveInfinity;
            for (var node = pillars.First; node != null; node = node.Next)
                nextPoll = Mathf.Min(nextPoll, node.Value.CheckHealth(world, now));
        }
    }
}
