using System.Collections.Generic;
using UnityEngine;

namespace FortressClash.Cutscene
{
    /// <summary>
    /// Small procedural textures for the staging - parchment, leather, soft shadows, gradients,
    /// dust.  Everything is generated once on first use and cached, so the cutscene needs no art
    /// assets beyond the book's own slides.
    /// </summary>
    public static class CutsceneArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>1x1 white sprite; scale it to size and tint the renderer to get a flat quad.</summary>
        public static Sprite Solid()
        {
            if (!Cache.TryGetValue("solid", out Sprite s))
            {
                s = Build("solid", 1, 1, (u, v) => Color.white);
            }

            return s;
        }

        /// <summary>A soft round blob - the workhorse for shadows, glows and dust motes.</summary>
        public static Sprite SoftBlob(float falloff = 1.9f, float core = 0.14f)
        {
            string key = $"blob{falloff:0.0}{core:0.00}";
            if (!Cache.TryGetValue(key, out Sprite s))
            {
                s = Build(key, 96, 96, (u, v) =>
                {
                    float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    float a = Mathf.Clamp01(1f - Mathf.InverseLerp(core, 1f, d));
                    return new Color(1f, 1f, 1f, Mathf.Pow(a, falloff));
                });
            }

            return s;
        }

        /// <summary>Soft-edged rectangle - the shadow a lifted card casts on the page.</summary>
        public static Sprite SoftRect()
        {
            if (Cache.TryGetValue("softrect", out Sprite s))
            {
                return s;
            }

            s = Build("softrect", 96, 96, (u, v) =>
            {
                float fx = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.17f);
                float fy = Mathf.Clamp01(Mathf.Min(v, 1f - v) / 0.30f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Min(fx, fy));
                return new Color(1f, 1f, 1f, a);
            });
            return s;
        }

        /// <summary>Warm parchment with grain, blotches and a darkened deckle edge.</summary>
        public static Sprite Paper()
        {
            if (Cache.TryGetValue("paper", out Sprite s))
            {
                return s;
            }

            int w = 640, h = 427;
            Color paper = CutsceneTuning.Paper;
            Color deep = CutsceneTuning.PaperDeep;

            s = Build("paper", w, h, (u, v) =>
            {
                // age blotches: two octaves of blobby noise
                float blotch = Fbm(u * 3.1f, v * 3.1f, 11, 4);
                float fibre = Fbm(u * 46f, v * 46f, 27, 2);
                Color c = Color.Lerp(paper, deep, Mathf.Clamp01(blotch * 0.55f) * 0.55f);
                c = Color.Lerp(c, c * 0.86f, fibre * 0.5f);

                // deckle edge: darker and slightly translucent at the border
                float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v * 1.16f, (1f - v) * 1.16f));
                float edge = Mathf.InverseLerp(0.035f, 0f, e);
                c = Color.Lerp(c, deep * 0.82f, edge * 0.85f);

                float alpha = Mathf.Lerp(1f, 0.72f, edge);
                return new Color(c.r, c.g, c.b, alpha);
            });
            return s;
        }

        /// <summary>Worn leather for the cover boards.</summary>
        public static Sprite Leather()
        {
            if (Cache.TryGetValue("leather", out Sprite s))
            {
                return s;
            }

            Color basec = CutsceneTuning.Leather;
            Color dark = CutsceneTuning.LeatherEdge;

            s = Build("leather", 320, 240, (u, v) =>
            {
                float grain = Fbm(u * 26f, v * 20f, 5, 3);
                float blotch = Fbm(u * 4.2f, v * 3.6f, 91, 3);
                Color c = Color.Lerp(dark, basec, Mathf.Clamp01(0.35f + blotch * 0.9f));
                c = Color.Lerp(c, dark, Mathf.Clamp01(grain - 0.42f) * 0.7f);
                return new Color(c.r, c.g, c.b, 1f);
            });
            return s;
        }

        /// <summary>Warm room wall behind the book: a vertical vignette gradient.</summary>
        public static Sprite Backdrop()
        {
            if (Cache.TryGetValue("backdrop", out Sprite s))
            {
                return s;
            }

            Color top = CutsceneTuning.RoomWarm;
            Color bottom = CutsceneTuning.RoomDark;
            s = Build("backdrop", 64, 256, (u, v) =>
            {
                Color c = Color.Lerp(bottom, top, Mathf.Pow(v, 1.5f));
                float side = Mathf.Lerp(0.55f, 1f, Mathf.Sin(u * Mathf.PI));
                return new Color(c.r * side, c.g * side, c.b * side, 1f);
            });
            return s;
        }

        /// <summary>Dark at one side, clear at the other - shades a sheet while it turns.</summary>
        public static Sprite TurnShade()
        {
            if (Cache.TryGetValue("turnshade", out Sprite s))
            {
                return s;
            }

            s = Build("turnshade", 128, 4, (u, v) => new Color(0.05f, 0.03f, 0.06f, Mathf.Pow(u, 1.15f)));
            return s;
        }

        /// <summary>Finished page block seen edge-on: fine stacked lines.</summary>
        public static Sprite PageEdge()
        {
            if (Cache.TryGetValue("pageedge", out Sprite s))
            {
                return s;
            }

            s = Build("pageedge", 4, 256, (u, v) =>
            {
                float line = Mathf.Abs(Mathf.Sin(v * 210f));
                float shade = 0.72f + 0.28f * line;
                Color c = CutsceneTuning.Paper * shade;
                return new Color(c.r, c.g, c.b, 1f);
            });
            return s;
        }

        /// <summary>Darkened corners pulled over the whole frame.  Kept gentle: a heavy vignette
        /// was doing a lot of the darkening before, and it sat on top of the slides.</summary>
        public static Sprite Vignette()
        {
            if (Cache.TryGetValue("vignette", out Sprite s))
            {
                return s;
            }

            s = Build("vignette", 128, 128, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f;
                float dy = (v - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx * 0.85f + dy * dy);
                float a = Mathf.Clamp01(Mathf.InverseLerp(0.78f, 1.42f, d));
                return new Color(0.02f, 0.01f, 0.03f, a * 0.58f);
            });
            return s;
        }

        private static Sprite Build(string key, int w, int h, System.Func<float, float, Color> shader)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = key,
            };

            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = shader((x + 0.5f) / w, v);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + seed * 1442695041;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi, seed);
            float b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed);
            float d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y, seed + i * 17) * amp;
                norm += amp;
                x *= 2.03f;
                y *= 2.01f;
                amp *= 0.5f;
            }

            return norm > 0f ? sum / norm : 0f;
        }
    }
}
