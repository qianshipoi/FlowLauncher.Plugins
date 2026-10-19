"""Generate a flat geometric PortKiller plugin icon as a multi-size favicon.ico.

Design: deep crimson rounded-square tile with a vertical gradient, a white RJ45
jack seen head-on (faceplate + hollow mouth with a clip notch + four contact
pins), and a warm-gold X stamped on the jack's lower-right corner for "terminate".

Follows the same conventions as make-varname-icon.py / make-passwordmanager-icon.py:
flat vector art, 4x supersampled then LANCZOS-downsampled, multi-size ICO, plus a
multi-size preview PNG for judging small-size legibility.
"""
from PIL import Image, ImageDraw, ImageChops
import math
import os
import sys

BASE = r"D:\Desktop\apps\qianshi\FlowLauncher.Plugins"
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    BASE, "src", "FlowLauncher.Plugin.PortKiller", "favicon.ico")
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
    os.environ.get("TEMP", "."), "portkiller-icon-preview.png")

SS = 4  # supersample factor

# ---- palette -------------------------------------------------------------
TILE_TOP = (74, 16, 20, 255)      # #4A1014 deep crimson-brown
TILE_BOTTOM = (170, 44, 40, 255)  # #AA2C28 red, lighter toward the bottom
INK = (255, 251, 245, 255)        # warm white for the jack
ACCENT = (255, 198, 92, 255)      # warm gold for the terminate cross


def _tile_mask(W):
    """Rounded-square tile mask, matching the inset/radius used by the other icons."""
    m = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(m)
    inset = W * 0.05
    d.rounded_rectangle((inset, inset, W - inset, W - inset), radius=W * 0.20, fill=255)
    return m


def _gradient(W):
    """Vertical gradient used as the tile fill."""
    g = Image.new("RGBA", (1, W))
    for y in range(W):
        t = y / max(W - 1, 1)
        g.putpixel((0, y), tuple(
            int(TILE_TOP[i] + (TILE_BOTTOM[i] - TILE_TOP[i]) * t) for i in range(4)))
    return g.resize((W, W), Image.NEAREST)


def _stroke(d, pts, width, fill):
    """Solid stroke: stamp heavily overlapping filled circles along the path.

    ImageDraw.line(..., width=N, joint="curve") leaves pinholes between its
    per-segment polygons on a densely sampled path; once the supersampled
    buffer is downscaled those pinholes read as coloured slivers cutting
    across the glyph. Stamping circles has no seams, and gives round caps
    and joins for free.
    """
    r = width / 2.0
    step = max(r / 4.0, 0.5)
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) / step))
        for s in range(n):
            t = s / n
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            d.ellipse((x - r, y - r, x + r, y + r), fill=fill)
    x, y = pts[-1]
    d.ellipse((x - r, y - r, x + r, y + r), fill=fill)


def _port_mask(W):
    """RJ45 jack, front view, as a white mask.

    The faceplate is solid; the mouth is knocked out so the tile shows through,
    the clip notch makes the mouth taller at the top (what reads as "RJ45"), and
    four pins stand inside the mouth, joined to its bottom edge.
    """
    plate = Image.new("L", (W, W), 0)
    ImageDraw.Draw(plate).rounded_rectangle(
        (W * 0.175, W * 0.225, W * 0.825, W * 0.775), radius=W * 0.088, fill=255)

    mouth = Image.new("L", (W, W), 0)
    md = ImageDraw.Draw(mouth)
    md.rounded_rectangle((W * 0.285, W * 0.385, W * 0.715, W * 0.662), radius=W * 0.026, fill=255)
    md.rounded_rectangle((W * 0.415, W * 0.332, W * 0.585, W * 0.420), radius=W * 0.018, fill=255)

    face = ImageChops.subtract(plate, mouth)

    pins = Image.new("L", (W, W), 0)
    pd = ImageDraw.Draw(pins)
    for cx in (0.358, 0.447, 0.553, 0.642):
        pd.rounded_rectangle((W * cx - W * 0.016, W * 0.528, W * cx + W * 0.016, W * 0.662),
                             radius=W * 0.010, fill=255)
    return ImageChops.lighter(face, ImageChops.darker(pins, mouth))


# The terminate cross, stamped across the jack's lower-right corner so the mouth
# and pins stay readable.
CROSS_CENTER = (0.745, 0.752)
CROSS_HALF = 0.108
CROSS_STROKE = 0.070


def make_icon(size):
    W = size * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))

    tile = _gradient(W)
    tile.putalpha(_tile_mask(W))
    img.alpha_composite(tile)

    jack = Image.new("RGBA", (W, W), INK)
    jack.putalpha(_port_mask(W))
    img.alpha_composite(jack)

    d = ImageDraw.Draw(img)
    cx, cy, h = CROSS_CENTER[0] * W, CROSS_CENTER[1] * W, CROSS_HALF * W
    stroke = CROSS_STROKE * W
    _stroke(d, [(cx - h, cy - h), (cx + h, cy + h)], stroke, ACCENT)
    _stroke(d, [(cx + h, cy - h), (cx - h, cy + h)], stroke, ACCENT)

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
