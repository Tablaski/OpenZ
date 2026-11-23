// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        sealed class SceneryMesh
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Indices = new List<int>();
            const float Circle = 2 * Mathf.PI;

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                int i = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
                for (int k = 0; k < 4; k++) Colors.Add(color);
                Uv.Add(Vector2.zero); Uv.Add(Vector2.right); Uv.Add(Vector2.up); Uv.Add(Vector2.one);
                Indices.Add(i); Indices.Add(i + 2); Indices.Add(i + 1);
                Indices.Add(i + 1); Indices.Add(i + 2); Indices.Add(i + 3);
            }

            public void Tube(Vector3 a, Vector3 b, float bottom, float top, Color color, int sides = 7)
            {
                var axis = (b - a).normalized;
                var u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
                var v = Vector3.Cross(axis, u);
                for (int i = 0; i < sides; i++)
                {
                    float ang = Circle * i / sides, next = Circle * (i + 1) / sides;
                    var r = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
                    var s = u * Mathf.Cos(next) + v * Mathf.Sin(next);
                    Quad(a + s * bottom, a + r * bottom, b + s * top, b + r * top,
                        color * (i % 2 == 0 ? 1.11f : .89f));
                }
            }

            public void Fan(Vector3 p, float radius, float height, float spin, Color color, int sides, float shoulder)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a = Circle * (i / (float)sides + spin), b = a + Circle / sides;
                    var u = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                    var v = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
                    var h = Vector3.up * height;
                    Quad(p + u, p + v, p + u * shoulder + h * .75f, p + v * shoulder + h * .75f,
                        color * (i % 2 == 0 ? .90f : 1.06f));
                    Quad(p + u * shoulder + h * .75f, p + v * shoulder + h * .75f, p + h, p + h, color * 1.08f);
                }
            }

            public void Palm(Vector3 p, float q, float size)
            {
                var bend = new Vector3(q - .45f, 0, .31f - q) * size * .46f;
                var crown = p + Vector3.up * size + bend;
                var bark = new Color(.31f, .208f, .13f);
                var last = p;
                for (int ring = 1; ring <= 8; ring++)
                {
                    float f = ring / 8f;
                    var next = p + Vector3.up * (size * f) + bend * f * f;
                    Tube(last, next, (.19f - f * .10f) * size / 4, (.17f - f * .09f) * size / 4, bark);
                    last = next;
                }
                for (int leaf = 0; leaf < 11; leaf++)
                {
                    float angle = Circle * (leaf / 11f + q);
                    var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var cross = Vector3.Cross(Vector3.up, direction);
                    float length = size * (.38f + (leaf % 4) * .052f);
                    var color = Color.Lerp(new Color(.14f, .26f, .062f), new Color(.36f, .43f, .13f), (leaf % 5) / 4f);
                    last = crown;
                    for (int part = 1; part <= 6; part++)
                    {
                        float t = part / 6f;
                        var tip = crown + direction * (length * t) + Vector3.up * (length * (.86f * t - 1.19f * t * t));
                        float width = length * Mathf.Sin(t * Mathf.PI) * .16f;
                        var sweep = direction * length * .14f;
                        Quad(last, tip, last + cross * width + sweep, tip + cross * (width * .73f) + sweep, color);
                        Quad(tip, last, tip - cross * (width * .73f) + sweep, last - cross * width + sweep, color * .82f);
                        last = tip;
                    }
                }
            }

            public void Cacti(Vector3 p, float q)
            {
                var green = new Color(.26f, .35f, .115f);
                var violet = new Color(.36f, .20f, .31f);
                if (q > .23f)
                {
                    var joint = p + Vector3.up * (.72f + q * .42f);
                    Tube(p, joint, .17f, .15f, green, 9);
                    Tube(joint, joint + Vector3.up * .52f, .15f, .08f, green, 9);
                    var arm = joint + new Vector3(q > .6f ? .28f : -.28f, -.15f, .07f);
                    Tube(arm, joint, .11f, .09f, green, 8);
                    Tube(arm, arm + Vector3.up * .36f, .11f, .065f, green, 8);
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = Circle * (q + i / 3f);
                    var offset = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (i == 0 ? .17f : .53f);
                    Fan(p + offset, (i + 2) * .062f, .25f + .05f * i, q, i == 1 ? violet : green, 11, .8f);
                    if (i == 1) Fan(p + offset + Vector3.up * .28f, .046f, .07f, q, new Color(.67f, .26f, .38f), 6, .7f);
                }
            }

            public void Lily(Vector3 p, float q)
            {
                var top = new Color(.20f, .33f, .11f);
                for (int i = 0; i < 3; i++)
                {
                    var at = p + new Vector3(i * .27f - .23f, .006f * i, .24f * Mathf.Sin(q * Circle + i));
                    float radius = .13f + i * .045f;
                    for (int blade = 1; blade < 8; blade++)
                    {
                        float a = Circle * (q + blade / 8f), b = a + Circle / 8;
                        var u = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                        var v = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
                        Quad(at + u, at + v, at, at, top * (.8f + .05f * blade));
                    }
                }
                if (q > .86f) Fan(p + new Vector3(.31f, .013f, .08f), .085f, .055f, q, new Color(.66f, .37f, .43f), 8, .54f);
                if (q < .30f)
                    for (int i = 0; i < 4; i++)
                    {
                        var reed = p + new Vector3(-.27f + i * .09f, 0, .23f - i * .10f);
                        Tube(reed, reed + new Vector3(.06f * i, .32f + i * .10f, 0), .018f, .005f, top, 4);
                    }
            }

            public void Grate(Vector3 center, float q)
            {
                var p = center + Vector3.up * .026f;
                var x = Vector3.right * .27f;
                var z = Vector3.forward * .26f;
                Quad(p - x - z, p + x - z, p - x + z, p + x + z, new Color(.075f, .074f, .067f));
                for (int i = -3; i <= 3; i++)
                {
                    var bar = p + Vector3.up * .023f + Vector3.right * (.072f * i);
                    Tube(bar - z, bar + z, .018f, .018f, new Color(.29f + q * .08f, .25f, .19f), 4);
                }
            }

            public void VolcanicCone(Vector3 p, float width, float height, float q)
            {
                for (int side = 0; side < 16; side++)
                {
                    float angle = side * Circle / 16 + q, next = angle + Circle / 16;
                    var a = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var b = new Vector3(Mathf.Cos(next), 0, Mathf.Sin(next));
                    var high = Vector3.up * height;
                    var mid = high * .49f;
                    var dark = new Color(.14f, .089f, .085f);
                    var rim = new Color(.25f, .16f, .13f);
                    Quad(p + a * width, p + b * width, p + a * width * .67f + mid, p + b * width * .67f + mid, dark);
                    Quad(p + a * width * .67f + mid, p + b * width * .67f + mid,
                        p + a * width * .30f + high, p + b * width * .30f + high, rim);
                    Quad(p + a * width * .30f + high, p + b * width * .30f + high,
                        p + a * width * .13f + mid, p + b * width * .13f + mid, dark * .62f);
                }
            }

            public void Cave(Vector3 p, Vector3 into, float q)
            {
                var side = Vector3.Cross(Vector3.up, into);
                var opening = new Color(.045f, .091f, .14f);
                for (int i = 0; i < 12; i++)
                {
                    float a = Mathf.PI * i / 12, b = Mathf.PI * (i + 1) / 12;
                    var e = side * (Mathf.Cos(a) * 2.65f) + Vector3.up * (Mathf.Sin(a) * 3.1f);
                    var f = side * (Mathf.Cos(b) * 2.65f) + Vector3.up * (Mathf.Sin(b) * 3.1f);
                    Quad(p + e + into * .4f, p + f + into * .4f, p + e * 1.31f + into * 1.2f,
                        p + f * 1.31f + into * 1.2f, new Color(.26f, .40f, .54f));
                    Quad(p + e + into * .4f, p + f + into * .4f, p + into * .61f, p + into * .61f, opening);
                    if (i >= 3 && i <= 9)
                    {
                        var point = p + e + into * .17f;
                        Tube(point, point - Vector3.up * (.42f + q * (i % 4) * .3f), .11f, .006f, new Color(.62f, .76f, .89f));
                    }
                }
            }
        }
    }
}
