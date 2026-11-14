using System.IO;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The words and folders of the dev checks that stay out of a player release. The release build (define OZ_RELEASE, set by the
    /// release and Mac build menus for the build only) compiles plain words and a folder inside the player's own data folder
    /// instead, so the game's code carries no dev folder name and no test screen name. The editor and the dev builds keep the
    /// old words and folders unchanged, so every check reads and writes exactly as before.
    /// </summary>
    public static class OzProofWords
    {
#if OZ_RELEASE
        /// <summary>The 16:10 test screen of the counted layout checks (they never run in a release).</summary>
        public const string Screen1610 = "16:10";
        static string RecordsRoot => Path.Combine(Application.persistentDataPath, "checks");
#else
        /// <summary>The 16:10 test screen of the counted layout checks.</summary>
        public const string Screen1610 = "2560x1600";
        static string RecordsRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records"));
#endif

        /// <summary>The folder a dev check writes its report into: sub is "qc", "gate/L02" and the like (the editor: the job's
        /// records folder, as before).</summary>
        public static string Records(string sub) => Path.GetFullPath(Path.Combine(RecordsRoot, sub));
    }
}
