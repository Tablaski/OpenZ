// OpenZ-VK (GPL-3.0).
using UnityEditor;

namespace OpenZ.EditorTools
{
    internal sealed class OzCarParkPerimeterImport : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (assetPath == "Assets/OpenZ/Models/Environment/CliffsV3/L17_outer_v3.fbx")
                ((ModelImporter)assetImporter).isReadable = true;
        }
    }
}
