#!/usr/bin/env python3
"""Generate FitForge launcher icons.

Original artwork drawn in the clean single-weight line style of the reference
icons. Everything is rendered at 8x and downsampled, which is what gives the
curves proper antialiasing — PIL has no native AA for shape outlines.

A kettlebell is the mark: it reads instantly at 48px, it's the most
recognizable single object in the app's world, and its silhouette stays
legible when Android crops it to a circle or squircle.
"""
from PIL import Image, ImageDraw

INK = (20, 23, 28)
EMBER = (255, 91, 53)
CHALK = (243, 241, 234)
SS = 8  # supersample factor


def draw_kettlebell(d, cx, cy, scale, color, weight):
    """Kettlebell centered on (cx, cy). `scale` is roughly half the mark height.

    The handle is built as a closed outline (outer arc + inner arc joined by two
    short lines) whose feet land exactly on the body circle, so the two pieces
    read as one object instead of a ring hovering over a ball.
    """
    w = max(1, int(weight))

    body_r = scale * 0.58
    body_cy = cy + scale * 0.32

    # Where the handle meets the body, and how wide the body is at that height.
    foot_y = cy - scale * 0.10
    dy = foot_y - body_cy
    foot_x = (body_r ** 2 - dy ** 2) ** 0.5   # solve the circle for x at foot_y

    # --- body ---
    d.ellipse([cx - body_r, body_cy - body_r, cx + body_r, body_cy + body_r],
              outline=color, width=w)

    # --- handle: outer arc, inner arc, and the two short joins ---
    outer_rx, outer_ry = foot_x, scale * 0.54
    inner_rx, inner_ry = foot_x * 0.58, scale * 0.32

    d.arc([cx - outer_rx, foot_y - outer_ry, cx + outer_rx, foot_y + outer_ry],
          start=180, end=360, fill=color, width=w)
    d.arc([cx - inner_rx, foot_y - inner_ry, cx + inner_rx, foot_y + inner_ry],
          start=180, end=360, fill=color, width=w)
    d.line([cx - outer_rx, foot_y, cx - inner_rx, foot_y], fill=color, width=w)
    d.line([cx + inner_rx, foot_y, cx + outer_rx, foot_y], fill=color, width=w)


def render(size, bg, fg, mark_scale, filename, ring=False):
    S = size * SS
    img = Image.new("RGB", (S, S), bg)
    d = ImageDraw.Draw(img)

    cx = cy = S / 2
    scale = S * mark_scale
    weight = S * 0.042

    if ring:
        r = S * 0.42
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=fg, width=int(S * 0.018))

    draw_kettlebell(d, cx, cy, scale, fg, weight)

    img = img.resize((size, size), Image.LANCZOS)
    img.save(filename, "PNG")
    return filename


out = "/mnt/user-data/outputs/fitforge-pwa/public/"

# Standard icons: generous mark, subtle ring for definition on light wallpapers.
render(192, INK, EMBER, 0.30, out + "icon-192.png", ring=True)
render(512, INK, EMBER, 0.30, out + "icon-512.png", ring=True)

# Maskable: art must sit inside the middle 80% so Android can crop to any shape,
# so the mark shrinks and the ring is dropped (it would be clipped).
render(512, INK, EMBER, 0.23, out + "icon-maskable-512.png", ring=False)

# Favicon + a chalk-on-ink variant for anywhere the ember reads too hot.
render(32, INK, EMBER, 0.32, out + "favicon-32.png")
render(180, INK, CHALK, 0.30, out + "apple-touch-icon.png", ring=True)

print("generated:")
import os
for f in sorted(os.listdir(out)):
    if f.endswith(".png"):
        p = os.path.join(out, f)
        im = Image.open(p)
        print(f"  {f:28} {im.size[0]}x{im.size[1]}  {os.path.getsize(p):>6} bytes")
