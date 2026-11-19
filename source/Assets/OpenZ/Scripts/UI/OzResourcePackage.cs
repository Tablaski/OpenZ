using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>Checks the independently versioned base resources before allowing a player to run.</summary>
    public sealed class OzResourcePackage : MonoBehaviour
    {
        [Serializable] public sealed class Identity { public string version, resourcePackage, commit, builtUtc; }
        public static bool Compatible { get; private set; } = true;
        public static string Problem { get; private set; }
        public static string Validate(Identity expected, Identity package, Identity embedded)
        {
            if (expected == null || string.IsNullOrEmpty(expected.resourcePackage)) return "This player has no resource package identity. Reinstall the complete game.";
            if (package == null || package.version != expected.resourcePackage)
                return "Required package: " + expected.resourcePackage + "\nInstalled package: " + (package == null ? "missing" : package.version);
            if (embedded == null || embedded.resourcePackage != expected.resourcePackage || embedded.commit != expected.commit || embedded.builtUtc != expected.builtUtc)
                return "The executable and game data belong to different builds. Repair or reinstall the complete game.";
            return null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Application.isEditor) return;
            Identity expected = null, package = null, embedded = null;
            try
            {
                expected = JsonUtility.FromJson<Identity>(File.ReadAllText(Path.Combine(Application.dataPath, "../FORK_BUILD.json")));
                var asset = Resources.Load<TextAsset>("OpenZ/resource_package");
                if (asset) package = JsonUtility.FromJson<Identity>(asset.text);
                asset = Resources.Load<TextAsset>("OpenZ/fork_build_resources");
                if (asset) embedded = JsonUtility.FromJson<Identity>(asset.text);
            }
            catch (Exception e) { Debug.LogWarning("Resource identity could not be read: " + e.Message); }
            Problem = Validate(expected, package, embedded);
            Compatible = Problem == null;
            if (Compatible) { Debug.Log("RESOURCE_PACKAGE_OK: " + expected.resourcePackage); return; }
            Debug.LogError("RESOURCE_PACKAGE_MISMATCH: " + Problem);
            var go = new GameObject("OpenZ-VK resource check", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go); go.AddComponent<OzResourcePackage>();
        }
        void Start()
        {
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            if (!EventSystem.current) new GameObject("Resource check input", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var bg = Hud.Img(transform, "Blocking background", OzUi.GM950); bg.rectTransform.Fill(); bg.raycastTarget = true;
            var panel = Hud.Node("Resource package message", transform); panel.TL(360,200,1200,680);
            Hud.Plate(panel, "md", 0, OzForkOptions.Brand);
            Hud.Label(panel,"Brand","OpenZ-VK",48,OzForkOptions.Brand).At(48,42,OzH.L,false,1100);
            Hud.Label(panel,"Title","RESOURCE UPDATE REQUIRED",32,OzUi.Ink).At(48,135,OzH.L,false,1100);
            Hud.Label(panel,"Problem",Problem,22,OzUi.Ink).At(48,220,OzH.L,false,1100);
            Hud.Label(panel,"Help","Open the installer and repair or update this installation.\nFor a source build, prepare the required Unity resources and build again.\nYour saved preferences are preserved.",22,OzUi.Muted).At(48,345,OzH.L,false,1100);
            var kit = HudButton.Make(panel,"QUIT","QUIT",new Vector2(1100,60),HudButtonKind.Large,24,.05f,HudButtonStyle.Normal);
            kit.Rt.TL(48,550,1100,60); kit.Bg.raycastTarget=true;
            var button=kit.Rt.gameObject.AddComponent<Button>();button.targetGraphic=kit.Bg;button.onClick.AddListener(ExitIncompatible);
            var args=Environment.GetCommandLineArgs();int proof=Array.IndexOf(args,"-oz-resource-proof");
            if(proof>=0&&proof+1<args.Length)StartCoroutine(CaptureMismatch(args[proof+1]));
        }
        System.Collections.IEnumerator CaptureMismatch(string folder)
        {
            Directory.CreateDirectory(folder);yield return new WaitForSecondsRealtime(2);yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"resource-package-mismatch.png"));
            File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: resource mismatch blocked the player. "+Problem);
            yield return new WaitForSecondsRealtime(.5f);ExitIncompatible();
        }
        static void ExitIncompatible()
        {
            OzQuit.ExitCode = 2;
            OzQuit.Request("incompatible OpenZ-VK resource package");
        }
        void LateUpdate() { Time.timeScale = 0; AudioListener.pause = true; }
    }
}
