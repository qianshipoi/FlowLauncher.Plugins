"""Generate a flat geometric Aliyun OSS plugin icon as multi-size favicon.ico.

Design: charcoal rounded-square tile, bold orange cloud silhouette,
and a compact 3-disk storage stack in the lower-right (object storage).
No broken highlight crescents; disks sit clear of the cloud body.
"""
from PIL import Image, ImageDraw
import os
import sys

OUT = sys.argv[1] if len(sys.argv) > 1 else r"D:\Desktop\apps\qianshi\FlowLauncher.Plugins\src\FlowLauncher.Plugin.AliyunOss\favicon.ico"
PREVIEW = r"D:\Desktop\apps\qianshi\FlowLauncher.Plugins\new-favicon-preview.png"


def make_icon(size: int) -> Image.Image:
    ss = 4
    W = size * ss
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    tile = (30, 42, 56, 255)
    orange = (255, 106, 0, 255)
    amber = (255, 170, 80, 255)
    cream = (255, 220, 180, 255)

    # Tile
    inset = int(W * 0.05)
    radius = int(W * 0.20)
    d.rounded_rectangle((inset, inset, W - inset, W - inset), radius=radius, fill=tile)

    # ---- Cloud: clean filled silhouette, centered-left ----
    cx = W * 0.40
    cy = W * 0.52

    # Core body first (kills any seam between bar and lobes)
    d.ellipse(
        (cx - W * 0.22, cy - W * 0.16, cx + W * 0.22, cy + W * 0.18),
        fill=orange,
    )

    # bottom bar (slightly taller so it fuses with the core)
    bar_half = W * 0.23
    bar_h = W * 0.14
    bar_y = cy + W * 0.04
    d.rounded_rectangle(
        (cx - bar_half, bar_y, cx + bar_half, bar_y + bar_h),
        radius=int(W * 0.06),
        fill=orange,
    )

    # left lobe
    r1 = W * 0.13
    c1 = (cx - W * 0.14, cy - W * 0.02)
    d.ellipse((c1[0] - r1, c1[1] - r1, c1[0] + r1, c1[1] + r1), fill=orange)

    # top lobe (largest)
    r2 = W * 0.17
    c2 = (cx + W * 0.01, cy - W * 0.14)
    d.ellipse((c2[0] - r2, c2[1] - r2, c2[0] + r2, c2[1] + r2), fill=orange)

    # right lobe
    r3 = W * 0.12
    c3 = (cx + W * 0.15, cy - W * 0.02)
    d.ellipse((c3[0] - r3, c3[1] - r3, c3[0] + r3, c3[1] + r3), fill=orange)

    # ---- Storage stack: 3 disks, lower-right, separated from the cloud ----
    sx = W * 0.76
    ew = W * 0.14
    eh = W * 0.038
    body = W * 0.046
    step = eh * 2 + body

    # dark separator only behind the stack (does not bite into the cloud)
    sep = (22, 32, 44, 255)
    stack_bottom = W * 0.80
    d.ellipse(
        (sx - ew - W * 0.010, stack_bottom - step * 2 - eh * 2,
         sx + ew + W * 0.010, stack_bottom + eh + W * 0.008),
        fill=sep,
    )

    # bottom → top; keep bottom disk a mid-amber so it never matches the cloud
    colors = (amber, cream, (255, 196, 120, 255))
    base_top = stack_bottom - body - eh  # top-face center of the bottom disk
    for i, col in enumerate(colors):
        top_cy = base_top - step * (2 - i)
        d.rectangle((sx - ew, top_cy, sx + ew, top_cy + body), fill=col)
        d.ellipse((sx - ew, top_cy + body - eh, sx + ew, top_cy + body + eh), fill=col)
        d.ellipse((sx - ew, top_cy - eh, sx + ew, top_cy + eh), fill=col)

    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    sizes = [16, 24, 32, 48, 64, 128, 256]
    imgs = [make_icon(s) for s in sizes]
    imgs[-1].save(OUT, format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[:-1])
    make_icon(256).save(PREVIEW)
    print("wrote", OUT)
    print("preview", PREVIEW)
    print("exists", os.path.exists(OUT), os.path.getsize(OUT))


if __name__ == "__main__":
    main()
