using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// <para>Zod: no line of sight at all (range + destroyable barriers, zmap.cpp:2266-2295). DV23 (Sim/ZSight.cs) made every Impassable tile a wall
    /// for direct fire and for the units' eyes, except level 7's lava (DV152). DV205: the impassable tiles that are LOWER ground (a pit, a ravine,
    /// a gorge's rim and walls, a moat and its parapet, a hole under the water line) are no wall for sight: direct fire, auto-targeting, agro,
    /// the attack planner's shot spots and the view's aim all see and shoot across them. Cliffs, raised rock, mountains, city blocks and the ring
    /// round every map stay walls. The tiles stay Impassable: nobody walks or drives into them (movement, paths, the computer: unchanged).</para>
    /// <para>Which tiles: <see cref="LowPitData"/>, one row per level, made off Unity by the kit's data step
    /// unit stands on), the same numbers for every level and biome: a HOLE (an impassable tile, 4 or more tiles in from the map's edge, whose whole
    /// surface lies at least 0.5 m under the playable ground round it: a pond's or lake's bed) or a PIT FEATURE (a group of touching impassable tiles that does not
    /// touch the map's edge, lies beside the playable ground and walls in ground at least 0.5 m lower, its crest at most 7 m over its bank:
    /// every tile of it). Level 2, 17, 18: only the river / canal bank faces under the bridges (already open, bridge rule); level 7: none (its lava
    /// is DV152's); level 16: the two long gorges, the zone 9 ravine by the west gun, the north-east lake bed and the swamp holes. A map not in the table (a Custom level, the
    /// Unit editor) has none. Data only: no float, no random draw, no frame time; the same on every machine.</para>
    /// <para>Switch <see cref="LowPitsDeviation"/>: View/ZGame.cs turns it on at every level start, off in 'Zod exact' (oz_zod_exact = 1) as DV152;
    /// static default off, so headless worlds, the parity report's Zod rows and probes and every older counted check keep DV23 as it was.
    /// Read once, when the world builds its sight grid (ZWorld.Sight). Parity row: Editor/OpenZParity.V15LowPits.cs, counted check
    /// Editor/OzV15LowPitsCount.cs (menu OpenZ/Checks/V1.5/Low pits never block sight (no Play)).</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV205 on: the low impassable tiles of <see cref="LowPitData"/> are no wall for sight (DV23). Off = DV23 as before (every
        /// Impassable tile but lava blocks).</summary>
        public static bool LowPitsDeviation = false;
        /// <summary>Holds DV205 off (the parity report's Zod rows and probes), puts the value back on Dispose.</summary>
        public static IDisposable LowPitsOff() => new LowPitsHold(false);
        /// <summary>Holds DV205 on (its counted check), puts the value back on Dispose.</summary>
        public static IDisposable LowPitsOn() => new LowPitsHold(true);
        sealed class LowPitsHold : IDisposable
        {
            readonly bool was = LowPitsDeviation;
            public LowPitsHold(bool on) { LowPitsDeviation = on; }
            public void Dispose() { LowPitsDeviation = was; }
        }

        bool[] lowPits; bool lowPitsRead;

        /// <summary>DV205 data: tile (tx, ty) is a low impassable tile of this level (a pit, ravine, gorge, moat, hole), whatever the switch.
        /// False off the map, on a lava tile (DV152's), on a tile that is not Impassable, and on a map that is not in the table.</summary>
        public bool LowPitAt(int tx, int ty)
        {
            if (!lowPitsRead) { lowPits = ReadLowPits(); lowPitsRead = true; }
            if (lowPits == null || tx < 0 || ty < 0 || tx >= Map.width || ty >= Map.height) return false;
            return lowPits[ty * Map.width + tx] && Map.ClassAt(tx, ty) == ZClass.Impassable && !Map.LavaAt(tx, ty);
        }

        /// <summary>DV205 in force on (tx, ty): the sight grid leaves this Impassable tile open (ZSight's constructor).</summary>
        public bool LowPitNoWall(int tx, int ty) => LowPitsDeviation && LowPitAt(tx, ty);

        /// <summary>DV205 data: how many low impassable tiles this level has (checks, the parity row).</summary>
        public int LowPitTiles
        {
            get
            {
                int n = 0;
                for (int ty = 0; ty < Map.height; ty++) for (int tx = 0; tx < Map.width; tx++) if (LowPitAt(tx, ty)) n++;
                return n;
            }
        }

        bool[] ReadLowPits()
        {
            if (Map == null || string.IsNullOrEmpty(Map.file)) return null;
            foreach (var row in LowPitData)
            {
                if (row.File != Map.file || row.W != Map.width || row.H != Map.height) continue;
                var m = new bool[row.W * row.H];
                foreach (var run in row.Runs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int plus = run.IndexOf('+');
                    int start = int.Parse(run.Substring(0, plus), System.Globalization.CultureInfo.InvariantCulture), len = int.Parse(run.Substring(plus + 1), System.Globalization.CultureInfo.InvariantCulture);
                    for (int i = start; i < start + len && i < m.Length; i++) if (i >= 0) m[i] = true;
                }
                return m;
            }
            return null;
        }

        /// <summary>One level's low impassable tiles: the map file and size it was made for, row-major runs "start+length" (tile index = ty * W + tx).</summary>
        public readonly struct LowPitLevel
        {
            public readonly string File, Runs; public readonly int W, H, Count;
            public LowPitLevel(string file, int w, int h, int count, string runs) { File = file; W = w; H = h; Count = count; Runs = runs; }
        }

        /// <summary>DV205 data (kit pits_no_block_r3, classify_low.py, 13 Nov 2026). Level 7: no row (its pits are lava, DV152).</summary>
        public static readonly LowPitLevel[] LowPitData =
        {
            new LowPitLevel("p02_bb_orig02.map", 72, 90, 59, "1889+1,1892+1,1961+1,1964+2,2033+1,2036+2,2105+1,2108+2,2177+1,2180+2,2249+1,2252+2,3194+5,3209+5,3410+2,3413+2,3425+2,3428+2,3483+3,3498+3,4227+1,4230+2,4299+1,4302+2,4371+1,4374+2,4443+1,4446+2,4515+1,4518+2,4587+1,4590+2"),   // level 02
            new LowPitLevel("p02_bb_orig16.map", 104, 128, 645, "859+1,1173+1,1379+3,1590+1,1652+1,1861+7,1965+7,2069+7,2173+7,2275+1,2277+7,2381+7,2481+3,2485+5,2589+2,2595+1,2604+1,2691+1,2708+1,2812+1,3055+8,3072+6,3158+10,3176+6,3262+10,3280+3,3284+2,3366+1,3375+2,3383+2,3389+1,3470+1,3473+1,3479+2,3487+2,3493+1,3575+3,3583+1,3590+2,3597+2,3682+1,3687+2,3694+2,3701+2,3786+1,3791+2,3798+1,3805+1,3890+1,3895+1,3902+1,3909+1,3994+1,3999+1,4007+2,4013+1,4098+1,4103+1,4112+1,4117+1,4202+1,4207+1,4216+1,4221+2,4306+1,4311+2,4320+1,4325+2,4410+1,4415+2,4424+1,4429+1,4514+1,4519+1,4527+2,4533+1,4618+1,4623+1,4631+2,4637+1,4722+1,4727+1,4734+2,4741+2,4791+2,4825+2,4831+1,4838+2,4845+2,4895+2,4929+2,4935+2,4942+1,4949+1,4998+3,5032+2,5039+2,5047+2,5053+1,5136+2,5143+1,5152+1,5157+1,5240+1,5247+1,5256+1,5261+2,5345+2,5351+2,5360+1,5365+2,5403+1,5450+1,5455+3,5464+1,5469+1,5554+1,5560+2,5567+2,5573+1,5608+1,5658+1,5664+2,5671+2,5677+1,5760+3,5768+1,5775+1,5780+1,5864+3,5871+1,5884+1,5968+3,5975+1,5984+1,5988+2,6073+2,6079+2,6087+2,6092+2,6178+1,6183+2,6191+2,6197+1,6282+1,6287+1,6295+1,6301+1,6348+1,6386+1,6391+1,6405+1,6490+1,6495+1,6504+1,6509+1,6594+1,6599+2,6608+1,6613+1,6651+1,6660+1,6698+1,6703+3,6712+1,6717+1,6764+1,6801+2,6808+2,6815+2,6820+1,6868+1,6905+2,6912+2,6919+2,6924+1,7008+2,7016+1,7022+2,7028+2,7112+2,7119+1,7126+2,7132+2,7180+1,7216+1,7223+1,7230+1,7237+1,7321+2,7327+1,7335+2,7341+1,7350+1,7353+1,7426+1,7431+2,7440+1,7445+1,7454+1,7457+1,7530+1,7535+2,7544+1,7549+2,7558+1,7561+1,7634+1,7639+1,7648+1,7653+2,7662+1,7665+1,7738+1,7743+1,7752+1,7757+2,7766+1,7769+1,7842+1,7847+2,7855+2,7861+1,7870+1,7873+1,7929+1,7946+1,7951+3,7959+2,7965+1,7974+1,7977+1,8023+23,8050+1,8056+2,8063+1,8069+1,8078+1,8081+1,8126+25,8153+2,8160+2,8173+2,8182+1,8185+1,8230+5,8236+13,8250+5,8257+2,8263+2,8272+1,8277+2,8334+1,8356+1,8358+1,8361+1,8367+1,8376+1,8381+2,8438+1,8441+1,8446+2,8452+1,8457+1,8460+3,8465+1,8471+1,8480+1,8485+1,8543+6,8550+4,8555+2,8558+2,8561+1,8563+1,8575+2,8584+1,8589+1,8652+1,8663+1,8674+1,8679+2,8688+1,8693+2,8776+3,8783+1,8792+1,8797+2,8804+1,8879+4,8887+1,8895+2,8901+1,8983+3,8991+2,8999+2,9005+1,9009+4,9087+1,9095+2,9102+2,9109+6,9191+1,9200+1,9206+2,9213+9,9304+2,9310+1,9317+5,9323+3,9400+1,9408+2,9415+2,9423+3,9427+2,9504+1,9511+3,9520+1,9531+2,9609+2,9614+3,9624+1,9626+1,9632+4,9715+4,9729+3,9733+2,9736+3,9838+1"),   // level 16
            new LowPitLevel("p02_bb_orig17.map", 110, 128, 24, "10392+6,10722+6,12592+6,12922+6"),   // level 17
            new LowPitLevel("p02_bb_orig18.map", 128, 120, 96, "3441+1,3444+1,3569+1,3572+1,3697+1,3700+1,3825+1,3828+1,3953+1,3956+1,4081+1,4084+1,6952+1,6955+1,6973+1,6976+1,6994+1,6997+1,7080+1,7083+1,7101+1,7104+1,7122+1,7125+1,7208+1,7211+1,7229+1,7232+1,7250+1,7253+1,7336+1,7339+1,7357+1,7360+1,7378+1,7381+1,7464+1,7467+1,7485+1,7488+1,7506+1,7509+1,7592+1,7595+1,7613+1,7616+1,7634+1,7637+1,7720+1,7723+1,7741+1,7744+1,7762+1,7765+1,7848+1,7851+1,7869+1,7872+1,7890+1,7893+1,7976+1,7979+1,7997+1,8000+1,8018+1,8021+1,8104+1,8107+1,8125+1,8128+1,8146+1,8149+1,8232+1,8235+1,8253+1,8256+1,8274+1,8277+1,8360+1,8363+1,8381+1,8384+1,8402+1,8405+1,13975+6,14359+6"),   // level 18
        };
    }
}
