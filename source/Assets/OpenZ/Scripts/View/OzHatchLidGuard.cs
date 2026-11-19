// OpenZ (GPL-3.0) - version 1.5 kit hatch_lid: the guard that keeps every tank's hatch lid drawn and on its hinge.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 kit hatch_lid, the net under the fix (View/OzHatchLid.cs): once a frame, after BoardingView (70) has posed every hatch and
    /// after every other LateUpdate that can touch a vehicle part (WeaponHold 80, LightV2HatchArms 85, RobotPopDeath 96), each shown light,
    /// medium and heavy tank and mech keeps its hatch lid drawn and on its hinge between closed and open (BoardingView.HatchLidGuardPass in
    /// View/BoardingView.HatchLidGuard.cs). It sits beside BoardingView on the ZGame object (the same install as BoardingView's). View only.
    /// </summary>
    [DefaultExecutionOrder(97)]
    public sealed class OzHatchLidGuard : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; Attach(); }
        static void OnScene(Scene s, LoadSceneMode m) => Attach();
        static void Attach()
        {
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (g && !g.GetComponent<OzHatchLidGuard>()) g.gameObject.AddComponent<OzHatchLidGuard>();
        }

        void LateUpdate()
        {
            var bv = BoardingView.I;
            if (bv && bv.isActiveAndEnabled) bv.HatchLidGuardPass();
        }
    }
}
