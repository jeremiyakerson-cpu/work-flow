using System;

namespace TowerDefense.Visuals.Pure
{
    /// <summary>How a shape is painted: drop shadow, outline, fill with bevel shading.</summary>
    public struct ShapeStyle
    {
        public Rgba Fill;
        public Rgba Outline;
        /// <summary>Outline thickness in normalized units (0 = none).</summary>
        public float OutlineWidth;
        /// <summary>0..1 darkening of the lower inner rim and bottom of the fill.</summary>
        public float Shading;
        /// <summary>0..1 lightening of the upper inner rim.</summary>
        public float Highlight;
        /// <summary>Width of the bevel rim in normalized units.</summary>
        public float RimWidth;
        public Rgba Shadow;
        public float ShadowDx;
        public float ShadowDy;
        public float ShadowSoftness;

        public static ShapeStyle Flat(Rgba fill) => new ShapeStyle { Fill = fill };

        /// <summary>The house style: dark outline, soft bevel, no shadow.</summary>
        public static ShapeStyle Toon(Rgba fill, float outlineWidth = 0.05f, float shading = 0.35f, float highlight = 0.3f) =>
            new ShapeStyle
            {
                Fill = fill,
                Outline = ArtPalette.Outline,
                OutlineWidth = outlineWidth,
                Shading = shading,
                Highlight = highlight,
                RimWidth = 0.14f,
            };

        public ShapeStyle WithShadow(float dx = 0.04f, float dy = -0.07f, float softness = 0.08f, float alpha = 0.3f)
        {
            ShapeStyle s = this;
            s.Shadow = new Rgba(0f, 0f, 0f, alpha);
            s.ShadowDx = dx;
            s.ShadowDy = dy;
            s.ShadowSoftness = softness;
            return s;
        }

        public ShapeStyle WithOutline(Rgba color, float width)
        {
            ShapeStyle s = this;
            s.Outline = color;
            s.OutlineWidth = width;
            return s;
        }
    }

    /// <summary>
    /// Engine-free RGBA raster target. Coordinates are normalized: y spans
    /// [-1, 1] bottom to top and x spans [-aspect, aspect], so a 128 px canvas
    /// shown at 64 pixels-per-unit maps one normalized unit to one world unit.
    /// Row 0 is the bottom row, matching Texture2D.SetPixels32. Internally
    /// premultiplied; exported as straight alpha with colour bled into fully
    /// transparent texels so bilinear filtering never shows dark fringes.
    /// </summary>
    public sealed class PixelCanvas
    {
        private const int Block = 8;

        public readonly int Width;
        public readonly int Height;
        /// <summary>Size of one pixel in normalized units.</summary>
        public readonly float PixelSize;
        public readonly float Aspect;

        // Premultiplied RGBA, row-major from the bottom row.
        private readonly float[] px;

        public PixelCanvas(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
            Height = height;
            PixelSize = 2f / height;
            Aspect = (float)width / height;
            px = new float[width * height * 4];
        }

        public PixelCanvas(int size) : this(size, size) { }

        /// <summary>Normalized x of a pixel column's centre.</summary>
        public float X(int i) => ((i + 0.5f) / Width * 2f - 1f) * Aspect;
        /// <summary>Normalized y of a pixel row's centre.</summary>
        public float Y(int j) => (j + 0.5f) / Height * 2f - 1f;

        // ------------------------------------------------------------ drawing

        /// <summary>Flat fill with anti-aliased (or softened) edges.</summary>
        public void Fill(Sdf shape, Rgba color, float softness = 0f)
        {
            float aa = PixelSize + softness;
            ForEachNear(shape, aa, (i, x, y, d) => Over(i, color, color.A * Coverage.FromDistance(d, aa)));
        }

        /// <summary>Paint a shape with the full style: shadow, outline, bevelled fill.</summary>
        public void Draw(Sdf shape, in ShapeStyle style)
        {
            if (style.Shadow.A > 0f)
            {
                Sdf shadow = SdfShapes.Translate(SdfShapes.Round(shape, style.OutlineWidth), style.ShadowDx, style.ShadowDy);
                Fill(shadow, style.Shadow, style.ShadowSoftness);
            }
            if (style.OutlineWidth > 0f && style.Outline.A > 0f)
                Fill(SdfShapes.Round(shape, style.OutlineWidth), style.Outline);
            FillShaded(shape, style);
        }

        /// <summary>Fill with a vertical gradient and a bevelled inner rim (lit from above).</summary>
        public void FillShaded(Sdf shape, in ShapeStyle style)
        {
            float aa = PixelSize;
            Rgba fill = style.Fill;
            float shading = style.Shading, highlight = style.Highlight;
            float rim = style.RimWidth > 0f ? style.RimWidth : 0.12f;
            float e = PixelSize;
            ForEachNear(shape, aa, (i, x, y, d) =>
            {
                float cov = Coverage.FromDistance(d, aa);
                if (cov <= 0f) return;
                Rgba c = fill;
                if (shading > 0f || highlight > 0f)
                {
                    // Gentle top-to-bottom gradient over the canvas height.
                    float g = (y + 1f) * 0.5f;
                    c = Rgba.Lerp(c.Darken(shading * 0.35f), c.Lighten(highlight * 0.15f), g);
                    float depth = -d;
                    if (depth < rim)
                    {
                        // Outward normal's y via central differences: up-facing rims catch light.
                        float ny = (shape(x, y + e) - shape(x, y - e)) / (2f * e);
                        float w = 1f - Math.Max(depth, 0f) / rim;
                        w *= w;
                        if (ny > 0f) c = c.Lighten(highlight * 0.55f * w * ny);
                        else c = c.Darken(shading * 0.45f * w * -ny);
                    }
                }
                Over(i, c, fill.A * cov);
            });
        }

        /// <summary>Radial soft glow: alpha falls from centre to radius as (1-r)^power.</summary>
        public void Glow(float cx, float cy, float radius, Rgba color, float power = 2f)
        {
            Sdf bound = SdfShapes.Circle(cx, cy, radius);
            ForEachNear(bound, PixelSize, (i, x, y, d) =>
            {
                float r = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                if (r >= 1f) return;
                float a = MathF.Pow(1f - r, power);
                Over(i, color, color.A * a);
            });
        }

        /// <summary>Per-pixel callback for procedural textures (noise etc.). Composited over.</summary>
        public void Paint(Func<float, float, Rgba> shader)
        {
            for (int j = 0; j < Height; j++)
            {
                float y = Y(j);
                for (int i = 0; i < Width; i++)
                {
                    Rgba c = shader(X(i), y);
                    Over((j * Width + i) * 4, c, c.A);
                }
            }
        }

        /// <summary>Same as <see cref="Paint"/> but with pixel indices (for tileable textures).</summary>
        public void PaintPixels(Func<int, int, Rgba> shader)
        {
            for (int j = 0; j < Height; j++)
                for (int i = 0; i < Width; i++)
                {
                    Rgba c = shader(i, j);
                    Over((j * Width + i) * 4, c, c.A);
                }
        }

        /// <summary>Replace RGB with white keeping alpha (hit-flash silhouettes).</summary>
        public PixelCanvas ToSilhouette(Rgba color)
        {
            var copy = new PixelCanvas(Width, Height);
            for (int k = 0; k < px.Length; k += 4)
            {
                float a = px[k + 3] * color.A;
                copy.px[k] = color.R * a;
                copy.px[k + 1] = color.G * a;
                copy.px[k + 2] = color.B * a;
                copy.px[k + 3] = a;
            }
            return copy;
        }

        /// <summary>Copy this canvas into a region of a larger one (atlas packing).</summary>
        public void CopyInto(PixelCanvas atlas, int offsetX, int offsetY)
        {
            for (int j = 0; j < Height; j++)
            {
                int ty = offsetY + j;
                if (ty < 0 || ty >= atlas.Height) continue;
                for (int i = 0; i < Width; i++)
                {
                    int tx = offsetX + i;
                    if (tx < 0 || tx >= atlas.Width) continue;
                    int s = (j * Width + i) * 4, t = (ty * atlas.Width + tx) * 4;
                    atlas.px[t] = px[s];
                    atlas.px[t + 1] = px[s + 1];
                    atlas.px[t + 2] = px[s + 2];
                    atlas.px[t + 3] = px[s + 3];
                }
            }
        }

        // ------------------------------------------------------------ readback / export

        /// <summary>Straight-alpha colour of a pixel.</summary>
        public Rgba GetPixel(int i, int j)
        {
            int k = (j * Width + i) * 4;
            float a = px[k + 3];
            if (a <= 1e-6f) return Rgba.Clear;
            return new Rgba(px[k] / a, px[k + 1] / a, px[k + 2] / a, a);
        }

        public float AlphaAt(int i, int j) => px[(j * Width + i) * 4 + 3];

        /// <summary>Sum of alpha over all pixels (coverage area in pixels).</summary>
        public float TotalAlpha()
        {
            float sum = 0f;
            for (int k = 3; k < px.Length; k += 4) sum += px[k];
            return sum;
        }

        /// <summary>
        /// Straight-alpha RGBA32 bytes, bottom row first. Fully transparent texels
        /// take the colour of the nearest opaque-ish neighbour (one dilation pass
        /// plus a fallback colour) to avoid dark bilinear fringes.
        /// </summary>
        public byte[] ToRgba32(Rgba bleed)
        {
            var bytes = new byte[Width * Height * 4];
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    int k = (j * Width + i) * 4;
                    float a = px[k + 3];
                    Rgba c;
                    if (a > 1e-4f) c = new Rgba(px[k] / a, px[k + 1] / a, px[k + 2] / a, a);
                    else c = NeighbourColour(i, j, bleed);
                    bytes[k] = Rgba.ToByte(c.R);
                    bytes[k + 1] = Rgba.ToByte(c.G);
                    bytes[k + 2] = Rgba.ToByte(c.B);
                    bytes[k + 3] = Rgba.ToByte(a);
                }
            }
            return bytes;
        }

        private Rgba NeighbourColour(int i, int j, Rgba fallback)
        {
            float r = 0f, g = 0f, b = 0f, w = 0f;
            for (int dj = -1; dj <= 1; dj++)
            {
                int y = j + dj;
                if (y < 0 || y >= Height) continue;
                for (int di = -1; di <= 1; di++)
                {
                    int x = i + di;
                    if (x < 0 || x >= Width) continue;
                    int k = (y * Width + x) * 4;
                    float a = px[k + 3];
                    if (a <= 1e-4f) continue;
                    r += px[k]; g += px[k + 1]; b += px[k + 2]; w += a;
                }
            }
            return w > 0f ? new Rgba(r / w, g / w, b / w, 0f) : fallback.WithAlpha(0f);
        }

        // ------------------------------------------------------------ internals

        private void Over(int k, Rgba c, float a)
        {
            if (a <= 0f) return;
            if (a > 1f) a = 1f;
            float inv = 1f - a;
            px[k] = c.R * a + px[k] * inv;
            px[k + 1] = c.G * a + px[k + 1] * inv;
            px[k + 2] = c.B * a + px[k + 2] * inv;
            px[k + 3] = a + px[k + 3] * inv;
        }

        private delegate void PixelAction(int index, float x, float y, float distance);

        /// <summary>
        /// Visit every pixel whose distance to the shape is below the filter
        /// width, culling 8x8 blocks whose centre is provably far outside
        /// (SDFs here are distance lower bounds, so |d| &gt; block radius is safe).
        /// </summary>
        private void ForEachNear(Sdf shape, float reach, PixelAction action)
        {
            float blockRadius = Block * PixelSize * 0.7072f * 1.25f; // half-diagonal plus safety margin
            for (int bj = 0; bj < Height; bj += Block)
            {
                for (int bi = 0; bi < Width; bi += Block)
                {
                    int ei = Math.Min(bi + Block, Width), ej = Math.Min(bj + Block, Height);
                    float cx = ((bi + ei) * 0.5f / Width * 2f - 1f) * Aspect;
                    float cy = (bj + ej) * 0.5f / Height * 2f - 1f;
                    if (shape(cx, cy) > blockRadius + reach) continue;
                    for (int j = bj; j < ej; j++)
                    {
                        float y = Y(j);
                        for (int i = bi; i < ei; i++)
                        {
                            float x = X(i);
                            float d = shape(x, y);
                            if (d > reach) continue;
                            action((j * Width + i) * 4, x, y, d);
                        }
                    }
                }
            }
        }
    }
}
