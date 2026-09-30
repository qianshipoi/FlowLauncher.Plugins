"""Generate a flat geometric VarName plugin icon as a multi-size favicon.ico.

Design: deep amber rounded-square tile with a vertical gradient, a bold white
curly-brace pair (code, i.e. an identifier slot), a white "V" monogram for
VarName inside it, and a small warm-gold four-point sparkle above the V to
signal AI-generated candidates.

Follows the same conventions as make-passwordmanager-icon.py / make-aliyunoss-icon.py:
flat vector art, 4x supersampled then LANCZOS-downsampled, multi-size ICO, plus a
multi-size preview PNG for judging small-size legibility.
"""
from PIL import Image, ImageDraw
import math
import os
import sys

BASE = r"D:\Desktop\apps\qianshi\FlowLauncher.Plugins"
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    BASE, "src", "FlowLauncher.Plugin.VarName", "favicon.ico")
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
    os.environ.get("TEMP", "."), "varname-icon-preview.png")

SS = 4  # supersample factor

# ---- palette -------------------------------------------------------------
TILE_TOP = (74, 42, 5, 255)      # #4A2A05 deep amber-brown
TILE_BOTTOM = (168, 95, 10, 255)  # #A85F0A amber, lighter toward the bottom
INK = (255, 251, 245, 255)        # warm white for the glyphs
SPARKLE = (255, 205, 92, 255)     # warm gold accent

# The sparkle is only a couple of pixels wide below this size and smears into
# the brace, so tiny variants keep just the braces and the V.
SPARKLE_MIN_SIZE = 32


def _lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def _cubic(p0, p1, p2, p3, steps=56):
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


def _gradient(W):
    """Vertical gradient used as the tile fill."""
    g = Image.new("RGBA", (1, W))
    for y in range(W):
        t = y / max(W - 1, 1)
        g.putpixel((0, y), tuple(
            int(TILE_TOP[i] + (TILE_BOTTOM[i] - TILE_TOP[i]) * t) for i in range(4)))
    return g.resize((W, W), Image.NEAREST)


def _brace_pts(side, W):
    """Curly-brace centerline in unit coords, mirrored for side=+1 ('}')."""
    spine, tip, cusp = 0.315, 0.375, 0.245
    y_top, y_bot, y_mid = 0.232, 0.768, 0.500
    r = 0.095

    pts = [(tip, y_top)]
    pts += _cubic((tip, y_top), (spine + 0.030, y_top),
                  (spine, y_top + 0.018), (spine, y_top + r))
    pts.append((spine, y_mid - r))
    pts += _cubic((spine, y_mid - r), (spine, y_mid - 0.030),
                  (cusp + 0.030, y_mid), (cusp, y_mid))
    pts += _cubic((cusp, y_mid), (cusp + 0.030, y_mid),
                  (spine, y_mid + 0.030), (spine, y_mid + r))
    pts.append((spine, y_bot - r))
    pts += _cubic((spine, y_bot - r), (spine, y_bot - 0.018),
                  (spine + 0.030, y_bot), (tip, y_bot))

    if side > 0:
        pts = [(1.0 - x, y) for (x, y) in pts]
    return [(x * W, y * W) for (x, y) in pts]


def _v_pts(W):
    """The 'V' monogram centerline."""
    hw, y_top, y_bot = 0.100, 0.400, 0.660
    return [((0.5 - hw) * W, y_top * W), (0.5 * W, y_bot * W), ((0.5 + hw) * W, y_top * W)]


def _stroke(d, pts, width, fill):
    """Solid stroke: stamp heavily overlapping filled circles along the path.

    ImageDraw.line(..., width=N, joint="curve") leaves pinholes between its
    per-segment polygons on a densely sampled bezier; once the supersampled
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


def _sparkle(d, W):
    """Four-point concave star, reads as an 'AI generated' flourish."""
    cx, cy = W * 0.500, W * 0.300
    outer, inner = W * 0.060, W * 0.022
    pts = []
    for i in range(8):
        ang = i * math.pi / 4 - math.pi / 2
        rad = outer if i % 2 == 0 else inner
        pts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang)))
    d.polygon(pts, fill=SPARKLE)


def make_icon(size):
    W = size * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))

    tile = _gradient(W)
    tile.putalpha(_tile_mask(W))
    img.alpha_composite(tile)

    d = ImageDraw.Draw(img)
    stroke = 0.064 * W
    _stroke(d, _brace_pts(-1, W), stroke, INK)
    _stroke(d, _brace_pts(+1, W), stroke, INK)
    _stroke(d, _v_pts(W), stroke, INK)
    if size >= SPARKLE_MIN_SIZE:
        _sparkle(d, W)

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
