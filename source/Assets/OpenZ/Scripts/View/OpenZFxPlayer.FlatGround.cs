// (View/OzExpInGame.cs, synchronously), the effect player's ground is the roof plane: the shock ring and the ground light lie flat on the
// roof (BuildPolar conforms to Ground), and no scorch mark is put on the ground under the fort. NaN (always, but inside that one call) =
// the ground exactly as before.
namespace OpenZ
{
    public partial class OpenZFxPlayer
    {
        /// <summary>The flat ground height of the beat being drawn now (NaN = none: the real ground).</summary>
        [System.NonSerialized] public float UeFlatY = float.NaN;
    }
}
