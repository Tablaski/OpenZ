namespace OpenZ
{
    /// <summary>1.2 v2 units (the MLRS, lesson 6): true while the ripple fires one of the salvo's rockets 2 to 8 (MlrsSalvo.Fire and the
    /// marked rockets' MlrsSalvo.FireMarked, around UnitView.OnFire). The launcher kicks on every rocket; the hull rocks once per salvo, on
    /// the sim's own rocket (UnitView.HeavyRecoil.cs).</summary>
    public partial class MlrsSalvo
    {
        public static bool RippleFiring { get; private set; }
    }
}
