namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units, the light tank v2 (its second model): the head kick's rock pivot. The head rocks its front up about a point behind its
    /// turn axis; for every unit that point is half-way to the head mesh's rear. The light tank v2's turret sits flat on the hull deck
    /// (2.6 mm over it all round), so rocking 2 deg about the half-way point pressed its rear 1.6 cm into the deck at the peak of every
    /// shot. Its pivot is the turret's rear edge instead (<see cref="LightPivotShareV2"/> = 1): the rear edge stays on the deck and the
    /// front lifts, the same 2 deg; the whole turret then stays at least 4 mm clear of the hull at all 24 headings tried, slide and rock at
    /// their peaks together. Every other unit and the light tank v1 keep the half-way point.
    /// </summary>
    public partial class HeavyRecoil
    {
        /// <summary>The light tank v2's rock pivot behind the turn axis, as a share of the turret mesh's rear (every other head: 0.5).</summary>
        public static float LightPivotShareV2 = 1f;

        /// <summary>Attach: the share of the head mesh's rear where its rock pivot sits.</summary>
        static float PivotShare(string type, int model) => type == OzLightTank.Type && model == 2 ? LightPivotShareV2 : 0.5f;
    }
}
