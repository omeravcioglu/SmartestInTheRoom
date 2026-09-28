using System;
using System.Collections.Generic;
using Smartest.Core;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// Every shape in the Tabloid skin, drawn in code from a handful of numbers at twice canvas
    /// resolution: bordered boxes (9-sliced), dashed boxes (tiled), halftone tokens, the timer
    /// burst, and a few icons.
    ///
    /// Shapes are white with the ink border baked in. An Image's colour multiplies the texture,
    /// so tinting a box gold turns the inside gold while the near-black border stays ink —
    /// which is why code everywhere can keep setting <c>image.color</c> and get a bordered box.
    ///
    /// SceneBuilder bakes the catalogue to PNGs under Resources/Ink so scenes can reference them.
    /// At runtime they load from there; anything not baked (a dot of an unusual size) is drawn
    /// on the spot and cached.
    /// </summary>
    public static class InkSprites
    {
        /// <summary>Texels per canvas pixel. Sprites carry a matching pixelsPerUnit.</summary>
        public const float Scale = 2f;
        public const float PixelsPerUnit = 100f * Scale;
        public const string Folder = "Ink";

        /// <summary>Transparent margin around every box, so rotated edges stay smooth.</summary>
        public const float Pad = 1f;

        /// <summary>One sprite ready to be written to disk: texels plus its 9-slice border (L, B, R, T).</summary>
        public struct Baked
        {
            public string Name;
            public Texture2D Texture;
            public Vector4 Border;
        }

        private static readonly Dictionary<string, Sprite> s_cache = new Dictionary<string, Sprite>();

        public static void ClearCache() => s_cache.Clear();

        // ------------------------------------------------------------------
        // Catalogue
        // ------------------------------------------------------------------

        /// <summary>A box with an ink border of <paramref name="border"/> px (0 = plain fill).</summary>
        public static Sprite Box(float border, float radius = 0f)
        {
            string name = BoxName(border, radius);
            return Get(name, () => RenderBox(name, border, radius, false));
        }

        /// <summary>Just the ring of a box, white, nothing inside: colour it with the image.</summary>
        public static Sprite Frame(float border)
        {
            string name = $"frame_b{Tenths(border)}";
            return Get(name, () => RenderFrame(name, border));
        }

        /// <summary>A dashed border. Use with Image.Type.Tiled so the dashes repeat instead of stretching.</summary>
        public static Sprite Dashed(float border)
        {
            string name = $"dash_b{Tenths(border)}";
            return Get(name, () => RenderBox(name, border, 0f, true));
        }

        /// <summary>A round token: ink ring, and optionally a halftone dot screen inside.</summary>
        public static Sprite Disc(float diameter, float border, bool halftone = false)
        {
            string name = DiscName(diameter, border, halftone);
            return Get(name, () => RenderDisc(name, diameter, border, halftone));
        }

        public static Sprite Burst16 => Get("burst16", () => RenderBurst("burst16", 16, 86f, 68f, 4f));
        public static Sprite Burst18 => Get("burst18", () => RenderBurst("burst18", 18, 88f, 70f, 4f));
        public static Sprite Spark => Get("spark10", () => RenderBurst("spark10", 10, 86f, 40f, 8f));
        public static Sprite Star => Get("star", () => RenderStar("star"));
        public static Sprite Check => Get("check", () => RenderStrokes("check", 96, 24f, 3.6f, CheckStrokes()));
        public static Sprite Cross => Get("cross", () => RenderStrokes("cross", 96, 24f, 3.6f, CrossStrokes()));
        public static Sprite Diamond => Get("diamond", () => RenderPolygon("diamond", 128, 40f,
            new[] { new Vector2(20, 2), new Vector2(38, 20), new Vector2(20, 38), new Vector2(2, 20) }, 2.5f));
        public static Sprite Dot => Get("dot", () => RenderDisc("dot", 64f, 0f, false));
        public static Sprite TriDown => Get("tri_down", () => RenderPolygon("tri_down", 48, 12f,
            new[] { new Vector2(0, 0), new Vector2(12, 0), new Vector2(6, 11) }, 0f));
        public static Sprite TriUp => Get("tri_up", () => RenderPolygon("tri_up", 48, 12f,
            new[] { new Vector2(6, 1), new Vector2(12, 11), new Vector2(0, 11) }, 0f));
        public static Sprite Speaker => Get("speaker", () => RenderStrokes("speaker", 96, 24f, 2.4f, SpeakerStrokes()));
        public static Sprite Mouse => Get("mouse", () => RenderMouse("mouse"));
        public static Sprite Checker => Get("checker", () => RenderChecker("checker"));
        public static Sprite Hatch => Get("hatch", () => RenderHatch("hatch"));
        /// <summary>The speech bubble's tail: paper inside, ink edge, open at the top.</summary>
        public static Sprite Tail => Get("tail", () => RenderPolygon("tail", 64, 26f,
            new[] { new Vector2(1f, 0f), new Vector2(25f, 0f), new Vector2(4f, 19f) }, 3f));
        /// <summary>A 4 px dotted rule, one dot per 8 px. Use with Image.Type.Tiled at 4 px tall.</summary>
        public static Sprite Dots => Get("dots", () => RenderDots("dots"));

        /// <summary>Everything SceneBuilder writes to Resources/Ink. Anything else is drawn on demand.</summary>
        public static IEnumerable<Baked> RenderCatalogue()
        {
            foreach (var (b, r) in StandardBoxes) yield return RenderBox(BoxName(b, r), b, r, false);
            foreach (float b in new[] { 3f, 4f }) yield return RenderBox($"dash_b{Tenths(b)}", b, 0f, true);
            foreach (float b in new[] { 2f, 2.5f, 3f, 4f }) yield return RenderFrame($"frame_b{Tenths(b)}", b);
            foreach (var (d, b, h) in StandardDiscs) yield return RenderDisc(DiscName(d, b, h), d, b, h);
            yield return RenderBurst("burst16", 16, 86f, 68f, 4f);
            yield return RenderBurst("burst18", 18, 88f, 70f, 4f);
            yield return RenderBurst("spark10", 10, 86f, 40f, 8f);
            yield return RenderStar("star");
            yield return RenderStrokes("check", 96, 24f, 3.6f, CheckStrokes());
            yield return RenderStrokes("cross", 96, 24f, 3.6f, CrossStrokes());
            yield return RenderPolygon("diamond", 128, 40f,
                new[] { new Vector2(20, 2), new Vector2(38, 20), new Vector2(20, 38), new Vector2(2, 20) }, 2.5f);
            yield return RenderDisc("dot", 64f, 0f, false);
            yield return RenderPolygon("tri_down", 48, 12f, new[] { new Vector2(0, 0), new Vector2(12, 0), new Vector2(6, 11) }, 0f);
            yield return RenderPolygon("tri_up", 48, 12f, new[] { new Vector2(6, 1), new Vector2(12, 11), new Vector2(0, 11) }, 0f);
            yield return RenderStrokes("speaker", 96, 24f, 2.4f, SpeakerStrokes());
            yield return RenderMouse("mouse");
            yield return RenderChecker("checker");
            yield return RenderHatch("hatch");
            yield return RenderDots("dots");
            yield return RenderPolygon("tail", 64, 26f, new[] { new Vector2(1f, 0f), new Vector2(25f, 0f), new Vector2(4f, 19f) }, 3f);
        }

        private static readonly (float border, float radius)[] StandardBoxes =
        {
            (0f, 0f), (2f, 0f), (2.5f, 0f), (3f, 0f), (3f, 4f), (4f, 0f), (4f, 6f), (5f, 0f), (3f, 31f)
        };

        private static readonly (float d, float b, bool h)[] StandardDiscs =
        {
            (36f, 2.5f, false), (36f, 2.5f, true), (38f, 2.5f, true), (40f, 2.5f, true), (46f, 2.5f, true),
            (60f, 3f, true), (64f, 3f, true), (64f, 3f, false), (84f, 3f, true), (320f, 5f, true), (30f, 4f, false),
            (46f, 3f, false)
        };

        public static string BoxName(float border, float radius) => $"box_b{Tenths(border)}_r{Mathf.RoundToInt(radius)}";

        public static string DiscName(float d, float b, bool h) =>
            $"disc{Mathf.RoundToInt(d)}_b{Tenths(b)}" + (h ? "_h" : string.Empty);

        private static int Tenths(float v) => Mathf.RoundToInt(v * 10f);

        // ------------------------------------------------------------------
        // Lookup
        // ------------------------------------------------------------------

        private static Sprite Get(string name, Func<Baked> render)
        {
            if (s_cache.TryGetValue(name, out var cached) && cached != null) return cached;

            var sprite = Resources.Load<Sprite>(Folder + "/" + name);
            if (sprite == null)
            {
                var baked = render();
                sprite = Sprite.Create(baked.Texture, new Rect(0, 0, baked.Texture.width, baked.Texture.height),
                    new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, baked.Border);
                sprite.name = name;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    baked.Texture.hideFlags = HideFlags.DontSave;
                    sprite.hideFlags = HideFlags.DontSave;
                    Debug.LogWarning($"[InkSprites] '{name}' is not baked into Resources/Ink, so a scene can't keep a " +
                                     "reference to it. Add it to InkSprites.RenderCatalogue and rebuild.");
                }
#endif
            }
            s_cache[name] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        private static readonly Color InkColor = Palette.Ink;

        private static Texture2D NewTexture(string name, int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private static Baked Finish(string name, Texture2D tex, Color32[] px, Vector4 border)
        {
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return new Baked { Name = name, Texture = tex, Border = border };
        }

        /// <summary>Blend of a white inside and an ink rim, with coverage as alpha.</summary>
        private static Color32 Shade(float inside, float coverage, float tone = 1f)
        {
            var c = Color.Lerp(InkColor, new Color(tone, tone, tone, 1f), inside);
            c.a = coverage;
            return c;
        }

        private static float RoundRectSD(float px, float py, float cx, float cy, float hx, float hy, float r)
        {
            r = Mathf.Min(r, Mathf.Min(hx, hy));
            float qx = Mathf.Abs(px - cx) - (hx - r);
            float qy = Mathf.Abs(py - cy) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(ox * ox + oy * oy);
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - r;
        }

        private static Baked RenderBox(string name, float border, float radius, bool dashed)
        {
            float slicePx = Mathf.Ceil(Pad + Mathf.Max(border, radius) + 1f);
            int s = Mathf.RoundToInt(slicePx * Scale);
            float dash = dashed ? Mathf.Max(6f, border * 3f) : 0f;
            float gap = dashed ? Mathf.Max(4f, border * 2f) : 0f;
            int centre = dashed ? Mathf.RoundToInt((dash + gap) * Scale) : 2;
            int size = 2 * s + centre;

            var tex = NewTexture(name, size, size);
            var px = new Color32[size * size];
            float pad = Pad * Scale, b = border * Scale, r = radius * Scale, half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float sd = RoundRectSD(fx, fy, half, half, half - pad, half - pad, r);
                    float cover = Mathf.Clamp01(0.5f - sd);
                    if (cover <= 0f) { px[y * size + x] = new Color32(255, 255, 255, 0); continue; }

                    float inside = 1f;
                    if (b > 0f)
                    {
                        float sdIn = RoundRectSD(fx, fy, half, half, half - pad - b, half - pad - b, Mathf.Max(r - b, 0f));
                        inside = Mathf.Clamp01(0.5f - sdIn);
                        if (dashed && inside < 1f)
                        {
                            // Edge slices carry one dash period each; the corners stay solid.
                            bool midX = x >= s && x < s + centre, midY = y >= s && y < s + centre;
                            float t = midX && !midY ? x - s : midY && !midX ? y - s : -1f;
                            if (t >= dash * Scale) inside = 1f; // a gap shows the fill
                        }
                    }
                    px[y * size + x] = Shade(inside, cover);
                }
            }
            return Finish(name, tex, px, new Vector4(s, s, s, s));
        }

        private static Baked RenderFrame(string name, float border)
        {
            float slicePx = Mathf.Ceil(Pad + border + 1f);
            int s = Mathf.RoundToInt(slicePx * Scale);
            int size = 2 * s + 2;
            var tex = NewTexture(name, size, size);
            var px = new Color32[size * size];
            float pad = Pad * Scale, b = border * Scale, half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cover = Mathf.Clamp01(0.5f - RoundRectSD(fx, fy, half, half, half - pad, half - pad, 0f));
                    float inside = Mathf.Clamp01(0.5f - RoundRectSD(fx, fy, half, half, half - pad - b, half - pad - b, 0f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * (1f - inside) * 255f));
                }
            }
            return Finish(name, tex, px, new Vector4(s, s, s, s));
        }

        private static Baked RenderDisc(string name, float diameter, float border, bool halftone)
        {
            int n = Mathf.CeilToInt((diameter + 2f * Pad) * Scale);
            var tex = NewTexture(name, n, n);
            var px = new Color32[n * n];
            float c = n * 0.5f, r = diameter * 0.5f * Scale, b = border * Scale;
            float pitch = (diameter >= 150f ? diameter / 29f : diameter >= 60f ? 6f : 5f) * Scale;
            float dotR = pitch * 0.25f;

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cover = Mathf.Clamp01(0.5f - (d - r));
                    if (cover <= 0f) { px[y * n + x] = new Color32(255, 255, 255, 0); continue; }
                    float inside = b > 0f ? Mathf.Clamp01(0.5f - (d - (r - b))) : 1f;

                    float tone = 1f;
                    if (halftone)
                    {
                        float gx = dx / pitch, gy = dy / pitch;
                        float ox = (gx - Mathf.Round(gx)) * pitch, oy = (gy - Mathf.Round(gy)) * pitch;
                        float dot = Mathf.Clamp01(0.5f - (Mathf.Sqrt(ox * ox + oy * oy) - dotR));
                        tone = Mathf.Lerp(1f, 0.75f, dot);
                    }
                    px[y * n + x] = Shade(inside, cover, tone);
                }
            }
            return Finish(name, tex, px, Vector4.zero);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len = Vector2.Dot(ab, ab);
            float t = len > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Signed distance to a polygon: negative inside.</summary>
        private static float PolygonSD(Vector2 p, Vector2[] v)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            {
                Vector2 a = v[j], b = v[i];
                d = Mathf.Min(d, SegmentDistance(p, a, b));
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside ? -d : d;
        }

        /// <summary>A polygon given in SVG coordinates (y down) inside a square viewBox.</summary>
        private static Baked RenderPolygon(string name, int size, float viewBox, Vector2[] points, float stroke)
        {
            var tex = NewTexture(name, size, size);
            var px = new Color32[size * size];
            float k = size / viewBox;           // texels per viewBox unit
            float halfStroke = stroke * 0.5f * k;
            var pts = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) pts[i] = points[i] * k;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, size - (y + 0.5f)); // SVG y runs down
                    float sd = PolygonSD(p, pts);
                    float cover = Mathf.Clamp01(0.5f - (sd - halfStroke));
                    if (cover <= 0f) { px[y * size + x] = new Color32(255, 255, 255, 0); continue; }
                    float inside = stroke > 0f ? Mathf.Clamp01(0.5f - (sd + halfStroke)) : 1f;
                    px[y * size + x] = Shade(inside, cover);
                }
            }
            return Finish(name, tex, px, Vector4.zero);
        }

        /// <summary>The comic-book timer burst: a star of <paramref name="points"/> spikes in a 180 box.</summary>
        private static Baked RenderBurst(string name, int points, float r1, float r2, float stroke)
        {
            var pts = new Vector2[points * 2];
            for (int k = 0; k < points * 2; k++)
            {
                float r = k % 2 == 0 ? r1 : r2;
                float a = Mathf.PI * k / points - Mathf.PI / 2f;
                pts[k] = new Vector2(90f + r * Mathf.Cos(a), 90f + r * Mathf.Sin(a));
            }
            return RenderPolygon(name, 360, 180f, pts, stroke);
        }

        private static Baked RenderStar(string name)
        {
            var pts = new[]
            {
                new Vector2(12f, 1.5f), new Vector2(15f, 8.6f), new Vector2(22.7f, 9.2f), new Vector2(16.8f, 14.2f),
                new Vector2(18.6f, 21.7f), new Vector2(12f, 17.7f), new Vector2(5.4f, 21.7f), new Vector2(7.2f, 14.2f),
                new Vector2(1.3f, 9.2f), new Vector2(9f, 8.6f)
            };
            return RenderPolygon(name, 96, 24f, pts, 1.8f);
        }

        // ---- stroked icons (white; tint them) ----

        private abstract class Stroke
        {
            public abstract float Distance(Vector2 p);
        }

        private sealed class Line : Stroke
        {
            private readonly Vector2[] _pts;
            private readonly bool _closed;
            public Line(bool closed, params Vector2[] pts) { _pts = pts; _closed = closed; }

            public override float Distance(Vector2 p)
            {
                float d = float.MaxValue;
                for (int i = 0; i + 1 < _pts.Length; i++) d = Mathf.Min(d, SegmentDistance(p, _pts[i], _pts[i + 1]));
                if (_closed && _pts.Length > 2) d = Mathf.Min(d, SegmentDistance(p, _pts[_pts.Length - 1], _pts[0]));
                return d;
            }
        }

        private sealed class Arc : Stroke
        {
            private readonly Vector2 _c;
            private readonly float _r, _from, _to;
            private readonly Vector2 _a, _b;

            public Arc(Vector2 centre, float radius, float fromDeg, float toDeg)
            {
                _c = centre; _r = radius; _from = fromDeg * Mathf.Deg2Rad; _to = toDeg * Mathf.Deg2Rad;
                _a = centre + new Vector2(Mathf.Cos(_from), Mathf.Sin(_from)) * radius;
                _b = centre + new Vector2(Mathf.Cos(_to), Mathf.Sin(_to)) * radius;
            }

            public override float Distance(Vector2 p)
            {
                var d = p - _c;
                float ang = Mathf.Atan2(d.y, d.x);
                if (ang >= _from && ang <= _to) return Mathf.Abs(d.magnitude - _r);
                return Mathf.Min((p - _a).magnitude, (p - _b).magnitude);
            }
        }

        private static Stroke[] CheckStrokes() => new Stroke[]
        {
            new Line(false, new Vector2(5f, 12.5f), new Vector2(10f, 17f), new Vector2(19f, 7f))
        };

        private static Stroke[] CrossStrokes() => new Stroke[]
        {
            new Line(false, new Vector2(6.5f, 6.5f), new Vector2(17.5f, 17.5f)),
            new Line(false, new Vector2(17.5f, 6.5f), new Vector2(6.5f, 17.5f))
        };

        private static Stroke[] SpeakerStrokes() => new Stroke[]
        {
            new Line(true, new Vector2(11f, 5f), new Vector2(6f, 9f), new Vector2(3f, 9f), new Vector2(3f, 15f),
                new Vector2(6f, 15f), new Vector2(11f, 19f)),
            new Arc(new Vector2(11.93f, 12f), 5f, -44.4f, 44.4f),
            new Arc(new Vector2(12.28f, 12f), 9f, -46.3f, 46.3f)
        };

        private static Baked RenderStrokes(string name, int size, float viewBox, float width, Stroke[] strokes)
        {
            var tex = NewTexture(name, size, size);
            var px = new Color32[size * size];
            float k = size / viewBox, half = width * 0.5f * k;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / k, (size - (y + 0.5f)) / k);
                    float d = float.MaxValue;
                    foreach (var s in strokes) d = Mathf.Min(d, s.Distance(p));
                    float cover = Mathf.Clamp01(0.5f - (d * k - half));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255f));
                }
            }
            return Finish(name, tex, px, Vector4.zero);
        }

        // ---- baked-colour pieces (use with a white Image colour) ----

        private static Baked RenderMouse(string name)
        {
            // 34 × 46 body, 3 px ink rim, paper inside, a 4 × 12 wheel.
            int w = Mathf.RoundToInt(36 * Scale), h = Mathf.RoundToInt(48 * Scale);
            var tex = NewTexture(name, w, h);
            var px = new Color32[w * h];
            float cx = w * 0.5f, cy = h * 0.5f, hx = 17f * Scale, hy = 23f * Scale, r = 17f * Scale, b = 3f * Scale;
            Color paper = Palette.PaperHi;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float sd = RoundRectSD(fx, fy, cx, cy, hx, hy, r);
                    float cover = Mathf.Clamp01(0.5f - sd);
                    if (cover <= 0f) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float inside = Mathf.Clamp01(0.5f - RoundRectSD(fx, fy, cx, cy, hx - b, hy - b, r - b));
                    // Wheel: 4 × 12, 8 px below the top edge.
                    float wheel = Mathf.Clamp01(0.5f - RoundRectSD(fx, fy, cx, cy + hy - (8f + 6f) * Scale, 2f * Scale, 6f * Scale, 0f));
                    var c = Color.Lerp(InkColor, paper, inside);
                    c = Color.Lerp(c, InkColor, wheel);
                    c.a = cover;
                    px[y * w + x] = c;
                }
            }
            return Finish(name, tex, px, Vector4.zero);
        }

        private static Baked RenderChecker(string name)
        {
            // The finish flag: 22 × 40, 2 px ink border, 4.5 px checks.
            int w = Mathf.RoundToInt(22 * Scale), h = Mathf.RoundToInt(40 * Scale), b = Mathf.RoundToInt(2 * Scale);
            int check = Mathf.RoundToInt(4.5f * Scale);
            var tex = NewTexture(name, w, h);
            tex.filterMode = FilterMode.Point;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool rim = x < b || y < b || x >= w - b || y >= h - b;
                    int ty = (h - 1 - y - b) / check, tx = (x - b) / check;
                    bool dark = rim || ((tx + ty) % 2 == 0);
                    px[y * w + x] = dark ? (Color32)InkColor : (Color32)Palette.Paper;
                }
            }
            return Finish(name, tex, px, Vector4.zero);
        }

        private static Baked RenderDots(string name)
        {
            int w = Mathf.RoundToInt(8 * Scale), h = Mathf.RoundToInt(4 * Scale);
            var tex = NewTexture(name, w, h);
            var px = new Color32[w * h];
            float r = 2f * Scale, cx = r, cy = h * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float cover = Mathf.Clamp01(0.5f - (Mathf.Sqrt(dx * dx + dy * dy) - r));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255f));
                }
            }
            return Finish(name, tex, px, Vector4.zero);
        }

        private static Baked RenderHatch(string name)
        {
            // Mirror's gold line: 45° stripes, 8 px each. Tiles seamlessly at 16 px.
            int n = Mathf.RoundToInt(16 * Scale), stripe = Mathf.RoundToInt(8 * Scale);
            var tex = NewTexture(name, n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = ((x + y) / stripe) % 2 == 0 ? (Color32)Palette.GoldSoft : (Color32)Palette.GoldHatch;
            return Finish(name, tex, px, new Vector4(0, 0, 0, 0));
        }
    }
}
