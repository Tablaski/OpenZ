using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>Bounded, scene-owned oil ribbons. No assets or local configuration are written.</summary>
    public sealed class OzCripplingTrails : MonoBehaviour
    {
        const int Capacity = 4096;
        sealed class Mark { public LineRenderer line; public double born, life, hold, fade; }
        readonly List<Mark> marks = new List<Mark>();
        int cursor;
        Material material;
        ZGame game;
        public void Add(ZGame owner, Vector3 from, Vector3 to, float width, double life, double fade = 0)
        {
            game = owner;
            if (!material)
            {
                var shader = Shader.Find("Sprites/Default");
                if (!shader) return;
                material = new Material(shader) { name = "OpenZ-VK fading oil", hideFlags = HideFlags.DontSave };
            }
            Mark mark;
            if (marks.Count < Capacity)
            {
                var go = new GameObject("OpenZ-VK oil trail"); go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(90, 0, 0);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material; line.positionCount = 2; line.useWorldSpace = true;
                line.alignment = LineAlignment.TransformZ; line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                mark = new Mark { line = line }; marks.Add(mark);
            }
            else { mark = marks[cursor]; cursor = (cursor + 1) % Capacity; }
            from.y = game.GroundY(from.x, from.z) + .025f;
            to.y = game.GroundY(to.x, to.z) + .025f;
            mark.born = game.World.Time; mark.life = life + fade;
            mark.hold = fade > 0 ? life : 0;
            mark.fade = fade > 0 ? fade : life;
            mark.line.SetPosition(0, from); mark.line.SetPosition(1, to);
            mark.line.startWidth = mark.line.endWidth = width; mark.line.enabled = true;
            Tint(mark, .6f);
        }
        static void Tint(Mark mark, float alpha)
        { var color = new Color(.055f, .045f, .065f, alpha); mark.line.startColor = mark.line.endColor = color; }
        void LateUpdate()
        {
            if (!game || game.World == null) return;
            foreach (var mark in marks)
            {
                if (!mark.line.enabled) continue;
                double age = game.World.Time - mark.born;
                if (age >= mark.life) mark.line.enabled = false;
                else if (age >= mark.hold) Tint(mark, .6f * Mathf.Clamp01(1 - (float)((age - mark.hold) / mark.fade)));
            }
        }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
