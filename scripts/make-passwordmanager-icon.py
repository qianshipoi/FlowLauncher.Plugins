"""Generate a flat geometric Password Manager plugin icon as multi-size favicon.ico.

Design: deep-teal rounded-square tile with a subtle vertical gradient, a white
shield silhouette, a keyhole punched through so the tile gradient shows in the
negative space, and a small warm-gold sparkle on the shield's shoulder to signal
random password generation (the plugin's other half).

Follows the same conventions as make-aliyunoss-icon.py: pillowed flat vector art,
supersampled then LANCZOS-downsampled, multi-size ICO with a 256px preview strip.
"""
from PIL import Image, ImageDraw, ImageChops
import math
import os
import sys

BASE = r"D:\Desktop\apps\qianshi\FlowLauncher.Plugins"
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    BASE, "src", "FlowLauncher.Plugin.PasswordManager", "favicon.ico")
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
    os.environ.get("TEMP", "."), "pwdpm-icon-preview.png")

SS = 4  # supersample factor

# ---- palette -------------------------------------------------------------
TILE_TOP = (21, 58, 66, 255)      # #153A42 deep teal
TILE_BOTTOM = (13, 84, 78, 255)   # #0D544E teal, lighter toward the bottom
SHIELD = (255, 255, 255, 255)
SPARKLE = (255, 194, 75, 255)     # warm gold


def _lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def _quad(p0, p1, p2, steps=48):
    """Sample a quadratic bezier."""
    pts = []
    for i in range(steps + 1):
        t = i / steps
        pts.append(_lerp(_lerp(p0, p1, t), _lerp(p1, p2, t), t))
    return pts


def _cubic(p0, p1, p2, p3, steps=64):
    """Sample a cubic bezier."""
    pts = []
    for i in range(steps + 1):
        t = i / steps
        a, b, c = _lerp(p0, p1, t), _lerp(p1, p2, t), _lerp(p2, p3, t)
        pts.append(_lerp(_lerp(a, b, t), _lerp(b, c, t), t))
    return pts


def _tile_mask(W):
    """Rounded-square tile mask, matching the inset/radius used by the other icons."""
    m = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(m)
    inset = W * 0.05
    d.rounded_rectangle(
        (inset, inset, W - inset, W - inset), radius=W * 0.20, fill=255)
    return m


def _shield_mask(W):
    """Shield silhouette: rounded top corners, straight shoulders, convex taper to a point."""
    m = Image.new("L", (W, W), 0)

    x0, x1 = W * 0.245, W * 0.755
    y_top, y_bot = W * 0.205, W * 0.845
    y_mid = W * 0.520        # where the straight shoulder hands off to the taper
    cx = W * 0.5
    rc = W * 0.075           # top corner radius

    r_top = x1 - rc
    l_top = x0 + rc

    pts = []
    pts.append((l_top, y_top))
    pts.append((r_top, y_top))                                   # top edge
    pts += _quad((r_top, y_top), (x1, y_top), (x1, y_top + rc))  # top-right corner
    pts.append((x1, y_mid))                                      # right shoulder
    pts += _cubic(                                               # convex taper, right
        (x1, y_mid),
        (x1, y_mid + W * 0.20),
        (cx + W * 0.150, y_bot - W * 0.155),
        (cx, y_bot),
    )
    pts += _cubic(                                               # convex taper, left
        (cx, y_bot),
        (cx - W * 0.150, y_bot - W * 0.155),
        (x0, y_mid + W * 0.20),
        (x0, y_mid),
    )
    pts.append((x0, y_top + rc))                                 # left shoulder
    pts += _quad((x0, y_top + rc), (x0, y_top), (l_top, y_top))  # top-left corner

    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def _keyhole_mask(W):
    """Circular bow plus a slightly flared slot below it."""
    m = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(m)
    cx = W * 0.5

    r = W * 0.068
    cy = W * 0.455
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)

    d.polygon(
        [
            (cx - W * 0.030, cy),
            (cx + W * 0.030, cy),
            (cx + W * 0.052, W * 0.620),
            (cx - W * 0.052, W * 0.620),
        ],
        fill=255,
    )
    return m


def _sparkle_mask(W):
    """Four-point star, concave — reads as a 'generate' flourish."""
    m = Image.new("L", (W, W), 0)
    cx, cy = W * 0.822, W * 0.238
    outer, inner = W * 0.072, W * 0.026

    pts = []
    for i in range(8):
        ang = i * math.pi / 4 - math.pi / 2
        rad = outer if i % 2 == 0 else inner
        pts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang)))

    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def _gradient(W):
    """Vertical gradient used as the tile fill."""
    g = Image.new("RGBA", (1, W))
    for y in range(W):
        t = y / max(W - 1, 1)
        g.putpixel((0, y), tuple(
            int(TILE_TOP[i] + (TILE_BOTTOM[i] - TILE_TOP[i]) * t) for i in range(4)))
    return g.resize((W, W), Image.NEAREST)


# Below this size the sparkle is only a few pixels wide and smears into a blob
# at the tile's corner, so the tiny variants drop it and keep just shield+keyhole.
SPARKLE_MIN_SIZE = 32


def make_icon(size):
    W = size * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))

    # Tile: gradient clipped to the rounded square
    tile = _gradient(W)
    tile.putalpha(_tile_mask(W))
    img.alpha_composite(tile)

    # Shield, with the keyhole knocked out so the gradient shows through
    shield = ImageChops.subtract(_shield_mask(W), _keyhole_mask(W))
    layer = Image.new("RGBA", (W, W), SHIELD)
    layer.putalpha(shield)
    img.alpha_composite(layer)

    # Sparkle sits last so it stays crisp over both tile and shield
    if size >= SPARKLE_MIN_SIZE:
        spark = Image.new("RGBA", (W, W), SPARKLE)
        spark.putalpha(_sparkle_mask(W))
        img.alpha_composite(spark)

    return img.resize((size, size), Image.LANCZOS)


def build_preview(out_path, sizes=(256, 48, 32, 16)):
    """Icon at several sizes on a mid-grey canvas, to judge small-size legibility."""
    pad, gap = 16, 14
    imgs = [make_icon(s) for s in sizes]
    w = pad * 2 + sum(i.width for i in imgs) + gap * (len(imgs) - 1)
    h = pad * 2 + max(i.height for i in imgs)
    canvas = Image.new("RGBA", (w, h), (122, 122, 128, 255))

    x = pad
    for im in imgs:
        canvas.alpha_composite(im, (x, pad + (h - pad * 2 - im.height) // 2))
        x += im.width + gap

    canvas.convert("RGB").save(out_path)
    return out_path


def main():
    sizes = [16, 24, 32, 48, 64, 128, 256]
    imgs = [make_icon(s) for s in sizes]
    imgs[-1].save(OUT, format="ICO", sizes=[(s, s) for s in sizes],
                  append_images=imgs[:-1])
    build_preview(PREVIEW)
    print("wrote", OUT, os.path.getsize(OUT), "bytes")
    print("preview", PREVIEW)


if __name__ == "__main__":
    main()
