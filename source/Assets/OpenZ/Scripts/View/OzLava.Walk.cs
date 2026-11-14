// OpenZ (GPL-3.0) - version 1.3, level 7 (LV_CONTRACT.md, kit LV3): the hooks of the walking kit in OzLava. Robots, Paint and Events stay in OzLava.cs.
namespace OpenZ
{
    public partial class OzLava
    {
        OzLavaWade wade;

        partial void Lv3Boot()
        {
            if (OzLavaFx.Off) return;
            wade = gameObject.AddComponent<OzLavaWade>();
            wade.Setup(game, map, lavaY, transform);
        }

        partial void Lv3Down() { if (wade) wade.Shutdown(); }
    }
}
