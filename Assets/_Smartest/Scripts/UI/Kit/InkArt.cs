using System.Collections.Generic;
using Smartest.Core;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// The minigames' illustrated pieces (a cup, a fish, a mole), drawn like everything else in
    /// the skin: flat shapes with a baked ink rim, at twice canvas resolution. The pieces
    /// themselves are in InkArt.Pieces.cs, one shape per line.
    ///
    /// A tinted piece is white where its colour goes, so a game colours it with Image.color
    /// exactly as it colours a kit box (gold, then green when caught). The rest carry their own
    /// colours and want a white Image colour.
    /// </summary>
    public static partial class InkSprites
    {
        public static Sprite Art(string name)
        {
            var piece = ArtBook.Find(name);
            if (piece == null)
            {
                Debug.LogError($"[InkSprites] No art called '{name}'.");
                return Box(3f);
            }
            return Get(ArtName(name), () => RenderArt(piece));
        }

        /// <summary>The piece's size in canvas pixels, as drawn.</summary>
        public static Vector2 ArtSize(string name)
        {
            var piece = ArtBook.Find(name);
            return piece == null ? Vector2.zero : new Vector2(piece.Width, piece.Height);
        }

        private static string ArtName(string name) => "art_" + name;

        private static IEnumerable<Baked> RenderArtCatalogue()
        {
            foreach (var piece in ArtBook.All) yield return RenderArt(piece);
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private static Baked RenderArt(ArtPiece piece)
        {
            string name = ArtName(piece.Name);
            int w = Mathf.RoundToInt(piece.Width * Scale), h = Mathf.RoundToInt(piece.Height * Scale);
            var tex = NewTexture(name, w, h);
            var paint = new Color[w * h]; // premultiplied colour and coverage, built up shape by shape
            Color ink = InkColor;

            foreach (var mark in piece.Marks)
            {
                // Only the texels near the shape: its bounds plus the rim, in canvas pixels, y down.
                var b = mark.Bounds;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(b.xMin * Scale) - 1);
                int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(b.xMax * Scale) + 1);
                int y0 = Mathf.Max(0, h - 1 - (Mathf.CeilToInt(b.yMax * Scale) + 1));
                int y1 = Mathf.Min(h - 1, h - 1 - (Mathf.FloorToInt(b.yMin * Scale) - 1));
                Color fill = mark.Fill;

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2((x + 0.5f) / Scale, (h - y - 0.5f) / Scale); // texture rows run up
                        float d = mark.Distance(p);
                        float a;
                        Color c;
                        if (mark.IsPen)
                        {
                            a = Mathf.Clamp01(0.5f - (d - mark.Rim * 0.5f) * Scale) * fill.a;
                            c = fill;
                        }
                        else if (mark.Rim > 0f)
                        {
                            // The rim straddles the edge: ink on the outside, the fill within.
                            float cover = Mathf.Clamp01(0.5f - (d - mark.Rim * 0.5f) * Scale);
                            float inside = Mathf.Clamp01(0.5f - (d + mark.Rim * 0.5f) * Scale);
                            float wFill = inside * fill.a, wRim = 1f - inside;
                            float total = Mathf.Max(wFill + wRim, 1e-6f);
                            c = (ink * wRim + fill * wFill) / total;
                            a = cover * (wFill + wRim);
                        }
                        else
                        {
                            a = Mathf.Clamp01(0.5f - d * Scale) * fill.a;
                            c = fill;
                        }
                        if (a <= 0f) continue;

                        ref var dst = ref paint[y * w + x];
                        dst.r = c.r * a + dst.r * (1f - a);
                        dst.g = c.g * a + dst.g * (1f - a);
                        dst.b = c.b * a + dst.b * (1f - a);
                        dst.a = a + dst.a * (1f - a);
                    }
                }
            }

            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                var s = paint[i];
                px[i] = s.a > 0f ? (Color32)new Color(s.r / s.a, s.g / s.a, s.b / s.a, s.a) : new Color32(255, 255, 255, 0);
            }
            return Finish(name, tex, px, piece.Border * Scale);
        }

        // ------------------------------------------------------------------
        // Pieces and their shapes
        // ------------------------------------------------------------------

        private sealed class ArtPiece
        {
            public readonly string Name;
            public readonly float Width, Height;
            public readonly bool Tinted;
            public readonly Mark[] Marks;
            public Vector4 Border { get; private set; }

            public ArtPiece(string name, float width, float height, bool tinted, Mark[] marks)
            {
                Name = name; Width = width; Height = height; Tinted = tinted; Marks = marks;
            }

            /// <summary>A 9-slice border, in canvas pixels: left, bottom, right, top.</summary>
            public ArtPiece Sliced(float left, float bottom, float right, float top)
            {
                Border = new Vector4(left, bottom, right, top);
                return this;
            }
        }

        /// <summary>One shape: a filled outline with an ink rim (Rim 0 = none), or a pen line Rim wide.</summary>
        private abstract class Mark
        {
            public Color Fill;
            public float Rim;
            public bool IsPen;
            public Rect Bounds;

            /// <summary>Signed distance in canvas pixels, negative inside; a pen's is its distance to the line.</summary>
            public abstract float Distance(Vector2 p);

            protected void Fit(Vector2 min, Vector2 max)
            {
                float grow = Rim * 0.5f + 1f;
                Bounds = Rect.MinMaxRect(min.x - grow, min.y - grow, max.x + grow, max.y + grow);
            }

            protected void Fit(Vector2[] pts)
            {
                Vector2 min = pts[0], max = pts[0];
                foreach (var v in pts) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
                Fit(min, max);
            }
        }

        private sealed class PolyMark : Mark
        {
            private readonly Vector2[] _v;
            public PolyMark(Vector2[] v) { _v = v; }
            public void Done() => Fit(_v);
            public override float Distance(Vector2 p) => PolygonSD(p, _v);
        }

        private sealed class CircleMark : Mark
        {
            private readonly Vector2 _c;
            private readonly float _r;
            public CircleMark(Vector2 c, float r) { _c = c; _r = r; }
            public void Done() => Fit(_c - new Vector2(_r, _r), _c + new Vector2(_r, _r));
            public override float Distance(Vector2 p) => (p - _c).magnitude - _r;
        }

        private sealed class RoundedMark : Mark
        {
            private readonly Vector2 _c, _half;
            private readonly float _radius;
            public RoundedMark(Vector2 c, Vector2 half, float radius) { _c = c; _half = half; _radius = radius; }
            public void Done() => Fit(_c - _half, _c + _half);
            public override float Distance(Vector2 p) => RoundRectSD(p.x, p.y, _c.x, _c.y, _half.x, _half.y, _radius);
        }

        private sealed class PenMark : Mark
        {
            private readonly Vector2[] _v;
            private readonly bool _closed;
            public PenMark(Vector2[] v, bool closed) { _v = v; _closed = closed; IsPen = true; }
            public void Done() => Fit(_v);

            public override float Distance(Vector2 p)
            {
                float d = float.MaxValue;
                for (int i = 0; i + 1 < _v.Length; i++) d = Mathf.Min(d, SegmentDistance(p, _v[i], _v[i + 1]));
                if (_closed && _v.Length > 2) d = Mathf.Min(d, SegmentDistance(p, _v[_v.Length - 1], _v[0]));
                return d;
            }
        }

        /// <summary>The words the pieces are written in. Angles are degrees, clockwise (y runs down).</summary>
        private static partial class ArtBook
        {
            private static Dictionary<string, ArtPiece> s_byName;

            public static ArtPiece Find(string name)
            {
                if (s_byName == null)
                {
                    s_byName = new Dictionary<string, ArtPiece>();
                    foreach (var piece in All) s_byName[piece.Name] = piece;
                }
                return name != null && s_byName.TryGetValue(name, out var found) ? found : null;
            }

            private static ArtPiece Tinted(string name, float w, float h, params Mark[] marks) => new ArtPiece(name, w, h, true, marks);
            private static ArtPiece Coloured(string name, float w, float h, params Mark[] marks) => new ArtPiece(name, w, h, false, marks);

            // Properties, not fields: All is in the other file, and C# doesn't say which file's
            // static fields are set first.
            private static Color White => Color.white;
            private static Color Ink => Palette.Ink;
            private static Color Ink2 => Palette.Ink2;
            private static Color PaperHi => Palette.PaperHi;
            private static Color Paper2 => Palette.Paper2;
            private static Color Gold => Palette.Gold;
            private static Color GoldSoft => Palette.GoldSoft;
            private static Color Red => Palette.Red;
            private static Color OnRed => Palette.OnRed;
            private static Color Green => Palette.Green;
            private static Color Blue => Palette.Blue;

            /// <summary>A grey for shading inside a tinted piece: the tint turns it into a darker shade.</summary>
            private static Color Tone(float v) => new Color(v, v, v, 1f);
            private static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

            private static Vector2[] Points(float[] xy)
            {
                var v = new Vector2[xy.Length / 2];
                for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
                return v;
            }

            private static Vector2[] Around(float cx, float cy, float rx, float ry, int n, float rotDeg)
            {
                var v = new Vector2[n];
                float rot = rotDeg * Mathf.Deg2Rad, cos = Mathf.Cos(rot), sin = Mathf.Sin(rot);
                for (int i = 0; i < n; i++)
                {
                    float t = 2f * Mathf.PI * i / n, x = rx * Mathf.Cos(t), y = ry * Mathf.Sin(t);
                    v[i] = new Vector2(cx + x * cos - y * sin, cy + x * sin + y * cos);
                }
                return v;
            }

            private static Vector2[] Along(float cx, float cy, float r, float fromDeg, float toDeg, int n)
            {
                var v = new Vector2[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    float a = (fromDeg + (toDeg - fromDeg) * i / n) * Mathf.Deg2Rad;
                    v[i] = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
                }
                return v;
            }

            private static Mark Filled(PolyMark m, Color fill, float rim)
            {
                m.Fill = fill; m.Rim = rim; m.Done();
                return m;
            }

            private static Mark Poly(Color fill, float rim, params float[] xy) => Filled(new PolyMark(Points(xy)), fill, rim);

            private static Mark Oval(float cx, float cy, float rx, float ry, Color fill, float rim, float rotDeg = 0f) =>
                Filled(new PolyMark(Around(cx, cy, rx, ry, 36, rotDeg)), fill, rim);

            /// <summary>A regular polygon, its first corner at rotDeg (straight up by default).</summary>
            private static Mark Ngon(float cx, float cy, float r, int n, Color fill, float rim, float rotDeg = -90f)
            {
                var v = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    float a = (rotDeg + 360f * i / n) * Mathf.Deg2Rad;
                    v[i] = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
                }
                return Filled(new PolyMark(v), fill, rim);
            }

            /// <summary>A star of n points, alternating between radii r1 and r2.</summary>
            private static Mark Spikes(float cx, float cy, float r1, float r2, int n, Color fill, float rim, float rotDeg = -90f)
            {
                var v = new Vector2[2 * n];
                for (int i = 0; i < 2 * n; i++)
                {
                    float r = i % 2 == 0 ? r1 : r2, a = (rotDeg + 180f * i / n) * Mathf.Deg2Rad;
                    v[i] = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
                }
                return Filled(new PolyMark(v), fill, rim);
            }

            /// <summary>An arc closed by its chord: a half disc when it spans 180 degrees.</summary>
            private static Mark Sector(float cx, float cy, float r, float fromDeg, float toDeg, Color fill, float rim) =>
                Filled(new PolyMark(Along(cx, cy, r, fromDeg, toDeg, 24)), fill, rim);

            private static Mark Circle(float cx, float cy, float r, Color fill, float rim)
            {
                var m = new CircleMark(new Vector2(cx, cy), r) { Fill = fill, Rim = rim };
                m.Done();
                return m;
            }

            private static Mark Rounded(float x0, float y0, float x1, float y1, float radius, Color fill, float rim)
            {
                var m = new RoundedMark(new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f), new Vector2((x1 - x0) * 0.5f, (y1 - y0) * 0.5f), radius)
                    { Fill = fill, Rim = rim };
                m.Done();
                return m;
            }

            private static Mark Stroked(Vector2[] v, bool closed, float width, Color colour)
            {
                var m = new PenMark(v, closed) { Fill = colour, Rim = width };
                m.Done();
                return m;
            }

            private static Mark Pen(float width, Color colour, params float[] xy) => Stroked(Points(xy), false, width, colour);
            private static Mark PenLoop(float width, Color colour, params float[] xy) => Stroked(Points(xy), true, width, colour);

            private static Mark PenArc(float cx, float cy, float r, float fromDeg, float toDeg, float width, Color colour) =>
                Stroked(Along(cx, cy, r, fromDeg, toDeg, 20), false, width, colour);

            private static Mark PenRing(float cx, float cy, float r, float width, Color colour) =>
                Stroked(Around(cx, cy, r, r, 48, 0f), true, width, colour);
        }
    }
}
