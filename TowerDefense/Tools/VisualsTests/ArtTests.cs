using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using TowerDefense.Visuals.Pure;

namespace TowerDefense.Visuals.Tests
{
    /// <summary>
    /// Every procedural sprite renders something sensible (non-empty, inside the
    /// canvas, no NaNs). Set VISUALS_DUMP_DIR to also write each one as a PNG
    /// (plus a contact sheet) for eyeballing the art without Unity.
    /// </summary>
    public class ArtTests
    {
        private static IEnumerable<TestCaseData> Sprites()
        {
            foreach (TowerArtKind k in Enum.GetValues(typeof(TowerArtKind)))
            {
                var kind = k;
                yield return Case("tower_base_" + kind, 128, c => ShapeArt.TowerBase(c, kind));
                yield return Case("tower_turret_" + kind, 128, c => ShapeArt.TowerTurret(c, kind));
                yield return Case("tower_accent_" + kind, 128, c => ShapeArt.TowerAccent(c, kind));
            }
            foreach (ProjectileArtKind k in Enum.GetValues(typeof(ProjectileArtKind)))
            {
                var kind = k;
                yield return Case("projectile_" + kind, 64, c => ShapeArt.Projectile(c, kind));
            }
            var tints = new[] { Rgba.Hex(0xE07A3C), Rgba.Hex(0x6CBF4A), Rgba.Hex(0x6F86B5), Rgba.Hex(0x9A5BD6), Rgba.Hex(0xB83A3A) };
            int t = 0;
            foreach (EnemyArtKind k in Enum.GetValues(typeof(EnemyArtKind)))
            {
                var kind = k;
                var tint = tints[t++ % tints.Length];
                yield return Case("enemy_" + kind, 128, c => ShapeArt.Enemy(c, kind, tint));
            }
            yield return Case("wing", 128, c => ShapeArt.Wing(c, Rgba.Hex(0x9A5BD6)));
            yield return Case("crown", 64, ShapeArt.Crown);
            yield return Case("hero", 128, ShapeArt.Hero);
            yield return Case("barricade", 128, ShapeArt.Barricade);
            yield return Case("cracks1", 128, c => ShapeArt.Cracks(c, 1));
            yield return Case("cracks2", 128, c => ShapeArt.Cracks(c, 2));
            yield return Case("slot", 128, ShapeArt.Slot);
            yield return Case("portal_base", 128, ShapeArt.PortalBase);
            yield return Case("portal_swirl", 128, ShapeArt.PortalSwirl);
            yield return Case("exit_flag", 128, ShapeArt.ExitFlag);
            yield return Case("glow", 64, ShapeArt.SoftGlow);
            yield return Case("disc", 64, c => ShapeArt.SoftDisc(c));
            yield return Case("ring", 128, c => ShapeArt.ThinRing(c));
            yield return Case("sparkle", 64, ShapeArt.Sparkle);
            yield return Case("flash", 64, ShapeArt.Flash);
            yield return Case("ground", 128, c => ShapeArt.GroundTile(c, 3u));
            foreach (DecorKind k in Enum.GetValues(typeof(DecorKind)))
                for (int v = 0; v < 2; v++)
                {
                    var kind = k; int variant = v;
                    yield return Case($"decor_{kind}_{variant}", 128, c => ShapeArt.Decor(c, kind, variant, Rgba.Hex(0x4E8F3A)));
                }
            foreach (char ch in DigitFont.Characters)
            {
                char g = ch;
                yield return Case("glyph_" + (g == '+' ? "plus" : g == '-' ? "minus" : g.ToString()), 64, c => ShapeArt.Glyph(c, g));
            }
            var shapes = new (string, Func<Sdf>)[]
            {
                ("circle", () => SdfShapes.Circle(0, 0, 0.9f)), ("ring", () => SdfShapes.UnitRing()),
                ("rounded_rect", () => SdfShapes.RoundedBox(0, 0, 0.9f, 0.6f, 0.25f)), ("triangle", SdfShapes.UnitTriangle),
                ("diamond", SdfShapes.UnitDiamond), ("hexagon", SdfShapes.UnitHexagon), ("star", SdfShapes.UnitStar),
                ("shield", SdfShapes.UnitShield), ("crown", SdfShapes.UnitCrown), ("arrow", SdfShapes.UnitArrow), ("droplet", SdfShapes.UnitDroplet),
            };
            foreach (var (name, make) in shapes)
            {
                var f = make;
                yield return Case("prim_" + name, 64, c => ShapeArt.Primitive(c, f(), true, true));
            }
        }

        private static TestCaseData Case(string name, int size, Action<PixelCanvas> draw) =>
            new TestCaseData(name, size, draw).SetName("Art_" + name);

        [TestCaseSource(nameof(Sprites))]
        public void RendersVisibleContentInsideCanvas(string name, int size, Action<PixelCanvas> draw)
        {
            var c = new PixelCanvas(size);
            draw(c);
            float total = c.TotalAlpha();
            Assert.That(float.IsNaN(total), Is.False, "NaN pixels");
            Assert.That(total, Is.GreaterThan(size * size * 0.01f), "sprite is (nearly) empty");

            if (!name.StartsWith("ground") && !name.StartsWith("glow") && !name.StartsWith("disc"))
            {
                // Nothing important touches the outer border (would be cut off / bleed when tiled).
                float edge = 0f;
                for (int i = 0; i < size; i++)
                    edge += c.AlphaAt(i, 0) + c.AlphaAt(i, size - 1) + c.AlphaAt(0, i) + c.AlphaAt(size - 1, i);
                Assert.That(edge, Is.LessThan(size * 0.25f), "art is clipped by the canvas edge");
            }

            string dir = Environment.GetEnvironmentVariable("VISUALS_DUMP_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                Png.Write(Path.Combine(dir, name + ".png"), c, checker: true);
            }
        }

        [Test]
        public void FullArtSetRendersQuickly()
        {
            var sw = Stopwatch.StartNew();
            int n = 0;
            foreach (var data in Sprites())
            {
                var c = new PixelCanvas((int)data.Arguments[1]);
                ((Action<PixelCanvas>)data.Arguments[2])(c);
                c.ToRgba32(Rgba.White);
                n++;
            }
            sw.Stop();
            TestContext.WriteLine($"{n} sprites in {sw.ElapsedMilliseconds} ms");
            // Desktop budget; an iPhone is roughly 2-4x slower and this runs once at boot.
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(4000));
        }

        [Test]
        public void ContactSheet()
        {
            string dir = Environment.GetEnvironmentVariable("VISUALS_DUMP_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Pass("Set VISUALS_DUMP_DIR to write the contact sheet.");
            var list = new List<PixelCanvas>();
            foreach (var data in Sprites())
            {
                var c = new PixelCanvas(128);
                int size = (int)data.Arguments[1];
                if (size == 128) ((Action<PixelCanvas>)data.Arguments[2])(c);
                else
                {
                    var small = new PixelCanvas(size);
                    ((Action<PixelCanvas>)data.Arguments[2])(small);
                    small.CopyInto(c, (128 - size) / 2, (128 - size) / 2);
                }
                list.Add(c);
            }
            int cols = 10, rows = (list.Count + cols - 1) / cols;
            var sheet = new PixelCanvas(cols * 128, rows * 128);
            for (int k = 0; k < list.Count; k++)
                list[k].CopyInto(sheet, (k % cols) * 128, (rows - 1 - k / cols) * 128);
            Directory.CreateDirectory(dir);
            Png.Write(Path.Combine(dir, "_contact_sheet.png"), sheet, checker: true);
        }
    }

    /// <summary>Minimal PNG writer (RGBA8, no filtering) for test dumps.</summary>
    public static class Png
    {
        public static void Write(string path, PixelCanvas c, bool checker)
        {
            int w = c.Width, h = c.Height;
            var raw = new byte[(w * 4 + 1) * h];
            for (int row = 0; row < h; row++)
            {
                int j = h - 1 - row; // PNG is top-down
                int o = row * (w * 4 + 1);
                raw[o++] = 0;
                for (int i = 0; i < w; i++)
                {
                    Rgba p = c.GetPixel(i, j);
                    if (checker)
                    {
                        float bg = ((i / 8 + j / 8) & 1) == 0 ? 0.55f : 0.65f;
                        p = new Rgba(p.R * p.A + bg * (1 - p.A), p.G * p.A + bg * (1 - p.A), p.B * p.A + bg * (1 - p.A), 1f);
                    }
                    raw[o++] = Rgba.ToByte(p.R); raw[o++] = Rgba.ToByte(p.G); raw[o++] = Rgba.ToByte(p.B); raw[o++] = Rgba.ToByte(p.A);
                }
            }
            using var fs = File.Create(path);
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            BE(ihdr, 0, (uint)w); BE(ihdr, 4, (uint)h);
            ihdr[8] = 8; ihdr[9] = 6;
            Chunk(fs, "IHDR", ihdr);
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true)) z.Write(raw, 0, raw.Length);
                Chunk(fs, "IDAT", ms.ToArray());
            }
            Chunk(fs, "IEND", Array.Empty<byte>());
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; BE(len, 0, (uint)data.Length); s.Write(len);
            var t = System.Text.Encoding.ASCII.GetBytes(type); s.Write(t); s.Write(data);
            uint crc = Crc(t, 0xFFFFFFFFu); crc = Crc(data, crc) ^ 0xFFFFFFFFu;
            var cb = new byte[4]; BE(cb, 0, crc); s.Write(cb);
        }

        private static void BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        private static uint Crc(byte[] data, uint crc)
        {
            foreach (byte d in data)
            {
                crc ^= d;
                for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
            return crc;
        }
    }
}
