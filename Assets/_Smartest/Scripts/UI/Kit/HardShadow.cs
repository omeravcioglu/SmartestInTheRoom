using System.Collections.Generic;
using Smartest.Core;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// The Tabloid "offset print" shadow: one solid, unblurred copy of the box, shifted right
    /// and down. Unlike UnityEngine.UI.Shadow it is always a flat colour (a gold shadow stays
    /// gold even though the box sprite has an ink border baked in), and it skips the sprite's
    /// transparent margin so a 6 px shadow measures 6 px.
    /// Rectangles only — a round token never casts one.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Smartest/Hard Shadow")]
    public class HardShadow : BaseMeshEffect
    {
        [SerializeField] private Vector2 offset = new Vector2(6f, -6f);
        [SerializeField] private Color color = new Color(0.102f, 0.098f, 0.086f, 1f);
        [Tooltip("Ignore this much of the sprite's edge (its transparent margin).")]
        [SerializeField] private float inset = InkSprites.Pad;

        public Vector2 Offset
        {
            get => offset;
            set { offset = value; Refresh(); }
        }

        public Color Color
        {
            get => color;
            set { color = value; Refresh(); }
        }

        /// <summary>Distance as the design writes it: "6px 6px 0" is 6.</summary>
        public float Distance
        {
            get => offset.x;
            set => Offset = new Vector2(value, -value);
        }

        public void Set(float distance, Color c)
        {
            offset = new Vector2(distance, -distance);
            color = c;
            Refresh();
        }

        private void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            if (offset == Vector2.zero || color.a <= 0f) return;

            var verts = ListPool<UIVertex>.Get();
            vh.GetUIVertexStream(verts);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            Vector4 uvMin = new Vector4(float.MaxValue, float.MaxValue, 0, 0), uvMax = -uvMin;
            for (int i = 0; i < verts.Count; i++)
            {
                var p = verts[i].position;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
                var uv = verts[i].uv0;
                uvMin = Vector4.Min(uvMin, uv);
                uvMax = Vector4.Max(uvMax, uv);
            }
            min += new Vector2(inset, inset);
            max -= new Vector2(inset, inset);
            // The middle of any Ink sprite is solid white, so sampling there gives a flat colour.
            Vector4 uvMid = (uvMin + uvMax) * 0.5f;

            float alpha = verts[0].color.a / 255f; // follow the graphic's own alpha
            Color32 c = new Color(color.r, color.g, color.b, color.a * alpha);

            var shadow = ListPool<UIVertex>.Get();
            AddQuad(shadow, min + offset, max + offset, c, uvMid);
            shadow.AddRange(verts);

            vh.Clear();
            vh.AddUIVertexTriangleStream(shadow);

            ListPool<UIVertex>.Release(shadow);
            ListPool<UIVertex>.Release(verts);
        }

        private static void AddQuad(List<UIVertex> list, Vector2 min, Vector2 max, Color32 c, Vector4 uv)
        {
            var v = UIVertex.simpleVert;
            v.color = c;
            v.uv0 = uv;

            v.position = new Vector3(min.x, min.y); var bl = v;
            v.position = new Vector3(min.x, max.y); var tl = v;
            v.position = new Vector3(max.x, max.y); var tr = v;
            v.position = new Vector3(max.x, min.y); var br = v;

            list.Add(bl); list.Add(tl); list.Add(tr);
            list.Add(tr); list.Add(br); list.Add(bl);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Refresh();
        }
#endif

        /// <summary>Ink shadow unless told otherwise.</summary>
        public static HardShadow On(Graphic g, float distance, Color? shadowColor = null)
        {
            if (g == null) return null;
            var s = g.GetComponent<HardShadow>();
            if (s == null) s = g.gameObject.AddComponent<HardShadow>();
            s.Set(distance, shadowColor ?? Palette.Ink);
            return s;
        }
    }
}
