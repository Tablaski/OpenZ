// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: the banners of the evil HQ (OzEvilStage; owner item 107).
// Like on his poster: tall red banners with a white star, blue banners with the poster's own winged eagle-and-star emblem (cropped
// the doors and in the alcove, hanging from rods on the walls behind the robots in every HQ framing. Each banner is a small cloth
// mesh (3 x 13 vertices) whose rows below the rod sway and ripple on the scene clock, gently (ClothAmp at the hem; CALM SCREEN keeps
// 30 %). The material is the stage's cutout, both faces drawn, so the cloth reads from any side.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        sealed class Cloth { public Mesh mesh; public Vector3[] rest, pos; public float h, phase; }
        readonly List<Cloth> cloths = new List<Cloth>();
        /// <summary>The hem's sway (m); the rows above move with the square of their height share.</summary>
        public static float ClothAmp = 0.045f;
        public int BannerCount => cloths.Count;

        Material CutoutTwoSided(Texture2D tex)
        {
            var m = Cutout(tex); if (!m) return null;
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            return m;
        }

        /// <summary>The banners on the back wall (z -3.21, just before the wall ribs at -3.22, between them), the pennants over the doors on
        /// the lintel and in the alcove: screen right (the wall behind EvilZod's marks) a red star banner, the blue emblem banner and a second
        /// red one; screen left (behind the sidekick) the board, a pennant over it and the blue banner by the side wall.</summary>
        void BuildBanners(Material rodM, Material capM)
        {
            var blueTex = Resources.Load<Texture2D>(SetFolder + "evil_banner");
            var redTex = Resources.Load<Texture2D>(SetFolder + "evil_banner_red");
            var penTex = Resources.Load<Texture2D>(SetFolder + "evil_pennant");
            var blueM = blueTex ? CutoutTwoSided(blueTex) : Lit(new Color32(36, 70, 150, 255), 0.2f, 0f);
            var redM = redTex ? CutoutTwoSided(redTex) : Lit(new Color32(196, 30, 28, 255), 0.2f, 0f);
            var penM = penTex ? CutoutTwoSided(penTex) : redM;
            const float wallZ = -3.21f, rodY = 4.4f;
            Banner("BannerRedA", redM, new Vector3(-1.85f, rodY, wallZ), 0.90f, 2.4f, 0f, rodM, capM, 0.0f);
            Banner("BannerBlueA", blueM, new Vector3(-3.25f, rodY, wallZ), 0.96f, 2.6f, 0f, rodM, capM, 1.9f);
            Banner("BannerRedB", redM, new Vector3(-4.55f, rodY, wallZ), 0.90f, 2.4f, 0f, rodM, capM, 3.7f);
            Banner("BannerBlueB", blueM, new Vector3(4.55f, rodY, wallZ), 0.96f, 2.6f, 0f, rodM, capM, 2.6f);
            Banner("PennantBoard", penM, new Vector3(2.35f, 5.05f, wallZ), 0.34f, 1.5f, 0f, rodM, capM, 4.4f);
            Banner("PennantDoorL", penM, new Vector3(0.98f, 5.1f, wallZ), 0.34f, 1.5f, 0f, rodM, capM, 0.8f);
            Banner("PennantDoorR", penM, new Vector3(-0.98f, 5.1f, wallZ), 0.34f, 1.5f, 0f, rodM, capM, 5.3f);
            // the alcove's pennants hang 0.10 m before its back wall (face at z -5.80): the cloth's sway reaches 0.083 m toward the wall
            foreach (float x in new[] { 1.0f, -1.0f }) Banner("PennantAlcove" + x, penM, new Vector3(x, 3.2f, -5.70f), 0.34f, 1.4f, 0f, rodM, capM, x > 0f ? 1.3f : 2.9f);
        }

        /// <summary>One banner: a rod with hazard caps at topCentre, the cloth hanging from it (w x h), facing the room (+Z turned by yawDeg).</summary>
        void Banner(string name, Material cloth, Vector3 topCentre, float w, float h, float yawDeg, Material rodM, Material capM, float phase)
        {
            var root = new GameObject(name) { layer = Layer }.transform; root.SetParent(transform, false);
            root.localPosition = topCentre; root.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            Box("Rod", new Vector3(0f, 0.02f, 0.03f), new Vector3(w + 0.16f, 0.04f, 0.04f), rodM, root);
            Box("CapA", new Vector3(-(w + 0.16f) * 0.5f, 0.02f, 0.03f), new Vector3(0.05f, 0.08f, 0.08f), capM, root);
            Box("CapB", new Vector3((w + 0.16f) * 0.5f, 0.02f, 0.03f), new Vector3(0.05f, 0.08f, 0.08f), capM, root);
            const int nx = 2, ny = 12;
            var verts = new Vector3[(nx + 1) * (ny + 1)]; var uv = new Vector2[verts.Length]; var tris = new int[nx * ny * 6];
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    int n = j * (nx + 1) + i; float u = i / (float)nx, v = j / (float)ny;   // v 0 = the hem, 1 = the rod
                    verts[n] = new Vector3((u - 0.5f) * w, -(1f - v) * h, 0f); uv[n] = new Vector2(u, v);
                }
            int t = 0;
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {   // a = lower left, b = lower right, c = upper left, d = upper right; (a, b, c) and (b, d, c) face +Z (both faces are drawn anyway)
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = c; tris[t++] = b; tris[t++] = d; tris[t++] = c;
                }
            var mesh = new Mesh { name = name + "Cloth" }; mesh.MarkDynamic();
            mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris; mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(0f, -h * 0.5f, 0f), new Vector3(w + 0.4f, h + 0.2f, 0.4f));
            var go = new GameObject("Cloth") { layer = Layer }; go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>();
            Dress(go, cloth, false);
            cloths.Add(new Cloth { mesh = mesh, rest = verts, pos = (Vector3[])verts.Clone(), h = h, phase = phase });
        }

        /// <summary>The cloth moves on the scene clock: a slow sway, a slower drift and a small ripple, all growing toward the hem.</summary>
        void TickBanners(float dt)
        {
            if (cloths.Count == 0 || dt <= 0f) return;
            float t = clock, amp = ClothAmp * (OzMotion.Reduced ? 0.3f : 1f);
            foreach (var c in cloths)
            {
                for (int n = 0; n < c.rest.Length; n++)
                {
                    Vector3 r = c.rest[n]; float v = Mathf.Clamp01(-r.y / c.h), s = v * v;
                    float sway = Mathf.Sin(t * 2.1f + c.phase + v * 2.6f) * s + 0.5f * Mathf.Sin(t * 0.9f + c.phase * 1.7f) * v;
                    float ripple = 0.35f * Mathf.Sin(t * 3.7f + c.phase * 0.6f + v * 6f + r.x * 3f) * s;
                    float side = 0.3f * Mathf.Sin(t * 0.7f + c.phase) * v;
                    c.pos[n] = new Vector3(r.x + amp * side, r.y, amp * (sway + ripple));
                }
                c.mesh.vertices = c.pos; c.mesh.RecalculateNormals();
            }
        }

        void DisposeBanners() { foreach (var c in cloths) if (c.mesh) Destroy(c.mesh); cloths.Clear(); }
    }
}
