// OpenZ (GPL-3.0) - version 1.3, level 7, kit LV5: the boot of the melting of what lands on lava. The work is in OzLavaMelt.cs.
namespace OpenZ
{
    public partial class OzLava
    {
        partial void Lv5Boot()
        {
            if (OzLavaFx.Off) return;
            gameObject.AddComponent<OzLavaMelt>().Setup(game, this, lavaY);
        }
    }
}
