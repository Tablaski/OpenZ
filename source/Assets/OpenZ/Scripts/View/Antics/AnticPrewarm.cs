// OpenZ (GPL-3.0) - version 1.2, run 8: the first phone or drink antic of a level held the game for about half a second (the L16 bench:
// 517-535 ms frames in the views where the first Drink / Phone antic started; none in a run whose first antics were Scan / Neck).
// Their hand props (the phone, the full can, the crushed can) were made, uploaded and drawn for the first time in that frame. Now each is
// made once at the level's start from the prop pool (AnticProps), drawn for two frames in front of the camera, small and far behind the
// near plane's view of nothing important, and handed back to the pool: the first antic takes a warm holder. View only. Off: Prewarm false.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public static class AnticPrewarm
    {
        public static bool Prewarm = true;
        public static int Warmed; public static float Ms; public static string Report = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void First() { if (Object.FindFirstObjectByType<Runner>() == null) OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }

        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (!Prewarm || m != LoadSceneMode.Single) return;
            var go = new GameObject("OZ_AnticPrewarm");
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            IEnumerator Start()
            {
                for (int i = 0; i < 600 && !(ZGame.I && ZGame.I.World != null); i++) yield return null;   // a level only
                if (!(ZGame.I && ZGame.I.World != null)) { Destroy(gameObject); yield break; }
                for (int i = 0; i < 20; i++) yield return null;
                var kit = AnticDirector.Kit;
                if (!kit || !kit.HasHandProps) { Destroy(gameObject); yield break; }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var made = new System.Collections.Generic.List<Transform>();
                foreach (var model in new[] { kit.phone, kit.can, kit.canCrushed })
                {
                    if (!model) continue;
                    var h = AnticProps.Take(model, "OZ_PrewarmProp", model == kit.can || model == kit.canCrushed ? kit.canMat : kit.phoneOwnMats ? null : kit.propMat);
                    if (h) made.Add(h);
                }
                var cam = Camera.main;
                foreach (var h in made)
                {
                    if (cam) { h.position = cam.transform.position + cam.transform.forward * 3f; h.localScale = Vector3.one * 0.02f; }   // drawn, too small to see
                    foreach (var mf in h.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) mf.sharedMesh.UploadMeshData(false);
                }
                sw.Stop(); float ms = (float)sw.Elapsed.TotalMilliseconds;
                yield return null; yield return null;
                sw.Start();
                foreach (var h in made) if (h) { h.localScale = Vector3.one; AnticProps.Release(h); }
                sw.Stop();
                Warmed = made.Count; Ms = (float)sw.Elapsed.TotalMilliseconds;
                Report = $"antic prop prewarm: {Warmed} hand props made and drawn at the level's start ({Ms:0.00} ms of work), back in the pool";
                Debug.Log("[OpenZ] " + Report);
                Destroy(gameObject);
            }
        }
    }
}
