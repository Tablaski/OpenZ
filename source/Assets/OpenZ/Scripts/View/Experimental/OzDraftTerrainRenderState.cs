using UnityEngine;
namespace OpenZ.Experimental
{
    /// <summary>Keep generated terrain on the ordinary renderer path without changing the game's global pipeline settings.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class OzDraftTerrainRenderState : MonoBehaviour
    {
        void OnEnable()
        {
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                properties.SetFloat("_OZDraftTerrain",1);renderer.SetPropertyBlock(properties);
            }
        }
    }
}
