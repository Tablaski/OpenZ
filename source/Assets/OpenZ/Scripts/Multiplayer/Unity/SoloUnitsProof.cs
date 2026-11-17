using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ.Multiplayer
{
    /// <summary>Opt-in proof of the saved solo selection reaching a real campaign scene.</summary>
    public sealed class SoloUnitsProof : MonoBehaviour
    {
        string folder,original;bool existed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-oz-solo-units-proof");if(index<0||index+1>=args.Length)return;
            var go=new GameObject("Solo unit selection proof");DontDestroyOnLoad(go);var proof=go.AddComponent<SoloUnitsProof>();proof.folder=args[index+1];proof.StartCoroutine(proof.Run());
        }
        IEnumerator Run()
        {
            Directory.CreateDirectory(folder);existed=PlayerPrefs.HasKey(OzSoloAllowedUnits.Preference);original=PlayerPrefs.GetString(OzSoloAllowedUnits.Preference,"");
            var rules=new DuelRules();rules.SetUnitPreset(DuelUnitPreset.Z1996);OzSoloAllowedUnits.Store(rules);
            yield return SceneManager.LoadSceneAsync("L02_Desert");yield return new WaitForSecondsRealtime(3);
            try{
                var game=FindFirstObjectByType<ZGame>();if(!game||game.World==null)throw new Exception("Campaign world missing.");
                var world=game.World;
                if(world.DuelConfiguration!=null||DuelRuntime.Active||world.Bots.Count==0)throw new Exception("Solo selection changed campaign or bot ownership.");
                if(world.Objects.Any(o=>o.IsUnit&&!world.DuelAllowsUnit(o.IsHero?"heroes":o.Type)))throw new Exception("Excluded starting unit remains.");
                foreach(var fort in world.Objects.Where(o=>o.IsFort&&o.Owner!=Team.Neutral)){
                    fort.Level=5;
                    if(world.ProductionUnits(fort).Any(type=>!rules.Allows(type))||fort.SetBuildingProduction("mtank")||fort.AddBuildingQueue("electrobot"))throw new Exception("Solo production ignores the roster.");
                }
                foreach(var bot in world.Bots)bot.ChooseBuildOrders();
                if(world.Objects.Any(o=>o.IsProducer&&o.CurUnit!=null&&!rules.Allows(o.CurUnit)))throw new Exception("CPU selected a forbidden unit.");
                DuelProofCapture.Save(Path.Combine(folder,"solo-z1996-game.png"));
                File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: saved Z 1996 selection applied to the campaign; starting units and production filtered for both teams; CPU active with permitted production; no multiplayer rules installed.");
                Restore();Application.Quit(0);
            }catch(Exception error){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+error);Restore();Application.Quit(1);}
        }
        void Restore(){if(existed)PlayerPrefs.SetString(OzSoloAllowedUnits.Preference,original);else PlayerPrefs.DeleteKey(OzSoloAllowedUnits.Preference);PlayerPrefs.Save();}
    }
}
