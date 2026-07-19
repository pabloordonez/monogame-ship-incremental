"""Generate seamless arena tiles + meta-screen backgrounds (neon dither style)."""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / "content" / "source" / "textures" / "backgrounds"
W, H = 640, 360


def save(img: Image.Image, name: str) -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    path = ROOT / name
    img.save(path, format="PNG")
    print(f"wrote {path} {img.size}")


def put(img: Image.Image, x: int, y: int, c: tuple[int, int, int, int]) -> None:
    img.putpixel((x % W, y % H), c)


def get(img: Image.Image, x: int, y: int) -> tuple[int, int, int, int]:
    return img.getpixel((x % W, y % H))


def lerp(a, b, t: float):
    t = max(0.0, min(1.0, t))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)


def wrap_dist2(x, y, cx, cy, tw=W, th=H):
    dx = min((x - cx) % tw, (cx - x) % tw)
    dy = min((y - cy) % th, (cy - y) % th)
    # proper toroidal
    dx = abs(x - cx)
    dy = abs(y - cy)
    if dx > tw // 2:
        dx = tw - dx
    if dy > th // 2:
        dy = th - dy
    return dx * dx + dy * dy


def hash2(x: int, y: int, seed: int) -> float:
    n = (x * 374761393 + y * 668265263 + seed * 982451653) & 0x7FFFFFFF
    n = (n ^ (n >> 13)) * 1274126177
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def value_noise(x: float, y: float, seed: int, scale: float) -> float:
    """Wrapping value noise."""
    xs = (x / scale) % (W / scale)
    ys = (y / scale) % (H / scale)
    # map to grid that wraps
    gw = max(1, int(round(W / scale)))
    gh = max(1, int(round(H / scale)))
    fx = (x / scale) % gw
    fy = (y / scale) % gh
    x0 = int(math.floor(fx)) % gw
    y0 = int(math.floor(fy)) % gh
    x1 = (x0 + 1) % gw
    y1 = (y0 + 1) % gh
    tx = fx - math.floor(fx)
    ty = fy - math.floor(fy)
    tx = tx * tx * (3 - 2 * tx)
    ty = ty * ty * (3 - 2 * ty)
    v00 = hash2(x0, y0, seed)
    v10 = hash2(x1, y0, seed)
    v01 = hash2(x0, y1, seed)
    v11 = hash2(x1, y1, seed)
    return (
        v00 * (1 - tx) * (1 - ty)
        + v10 * tx * (1 - ty)
        + v01 * (1 - tx) * ty
        + v11 * tx * ty
    )


def force_seamless(img: Image.Image, blend: int = 48) -> Image.Image:
    """Cross-blend opposite edges so edge pixels match when tiled."""
    out = img.copy()

    def blend_axis(horizontal: bool) -> None:
        limit = W if horizontal else H
        for fixed in range(H if horizontal else W):
            for i in range(blend):
                t = i / max(1, blend - 1)  # 0 at outer edge, 1 inward
                opposite_w = (1.0 - t) * 0.5
                if horizontal:
                    a = get(out, i, fixed)
                    b = get(out, limit - 1 - i, fixed)
                    put(out, i, fixed, lerp(a, b, opposite_w))
                    put(out, limit - 1 - i, fixed, lerp(b, a, opposite_w))
                else:
                    a = get(out, fixed, i)
                    b = get(out, fixed, limit - 1 - i)
                    put(out, fixed, i, lerp(a, b, opposite_w))
                    put(out, fixed, limit - 1 - i, lerp(b, a, opposite_w))

    blend_axis(horizontal=True)
    blend_axis(horizontal=False)
    # Corners: average the four corner samples so both axes agree.
    for ix in (0, W - 1):
        for iy in (0, H - 1):
            samples = [
                get(img, 0, 0),
                get(img, W - 1, 0),
                get(img, 0, H - 1),
                get(img, W - 1, H - 1),
            ]
            avg = tuple(sum(s[c] for s in samples) // 4 for c in range(3)) + (255,)
            put(out, ix, iy, avg)
    return out


def make_seamless_arena(seed: int, cool: bool) -> Image.Image:
    rng = random.Random(seed)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    base = (14, 10, 32) if cool else (18, 12, 28)
    accent = (30, 70, 95) if cool else (55, 35, 50)
    neb_a = (70, 35, 120, 255) if cool else (100, 40, 80, 255)
    neb_b = (35, 70, 130, 255) if cool else (60, 30, 70, 255)

    # Flat base + wrapping soft modulation (no world-space vertical gradient).
    # Use scales that divide 640 and 360 exactly: 40 and 20.
    for y in range(H):
        for x in range(W):
            n = value_noise(x, y, seed, 40) * 0.55 + value_noise(x, y, seed + 3, 20) * 0.45
            c = lerp(base + (255,), accent + (255,), n * 0.55)
            if (x + y) % 2 == 0 and n > 0.55:
                c = lerp(c, accent + (255,), 0.12)
            put(img, x, y, c)

    # Toroidal nebula blobs
    blobs = []
    for i in range(5):
        blobs.append(
            (
                rng.randrange(W),
                rng.randrange(H),
                rng.randint(70, 140),
                rng.randint(40, 90),
                0.25 + rng.random() * 0.2,
            )
        )
    for cx, cy, rx, ry, dens in blobs:
        for y in range(H):
            for x in range(W):
                # elliptic toroidal
                dx = abs(x - cx)
                dy = abs(y - cy)
                if dx > W // 2:
                    dx = W - dx
                if dy > H // 2:
                    dy = H - dy
                nx, ny = dx / max(1, rx), dy / max(1, ry)
                d = nx * nx + ny * ny
                if d > 1:
                    continue
                fall = 1 - d
                if fall < dens:
                    continue
                src = get(img, x, y)
                pick = neb_a if ((x // 2 + y // 2) % 2 == 0 or fall > 0.75) else neb_b
                put(img, x, y, lerp(src, pick, fall * 0.4))

    # Soft glowing orbs (wrap-aware) instead of hard planets that seam
    for _ in range(4):
        cx, cy = rng.randrange(W), rng.randrange(H)
        r = rng.randint(10, 22)
        col = rng.choice(
            [(50, 180, 200, 255), (150, 80, 190, 255), (80, 120, 200, 255)]
            if cool
            else [(200, 90, 60, 255), (150, 70, 160, 255), (80, 140, 170, 255)]
        )
        shadow = (20, 40, 60, 255)
        for y in range(H):
            for x in range(W):
                d2 = wrap_dist2(x, y, cx, cy)
                if d2 > (r + 1) * (r + 1):
                    continue
                if d2 > r * r:
                    continue
                t = math.sqrt(d2) / r
                src = get(img, x, y)
                body = lerp(col, shadow, t * 0.7)
                if (x + y) % 2 == 0 and t > 0.55:
                    body = lerp(body, shadow, 0.35)
                put(img, x, y, lerp(src, body, 0.85))

    # Stars with wrap copies near edges
    for _ in range(380):
        sx, sy = rng.randrange(W), rng.randrange(H)
        col = rng.choice(
            [
                (255, 255, 255, 255),
                (200, 220, 255, 255),
                (255, 180, 255, 255),
                (160, 255, 220, 255),
                (180, 255, 140, 255),
                (120, 140, 200, 255),
            ]
        )
        put(img, sx, sy, col)
        if rng.random() < 0.07:
            put(img, sx - 1, sy, col)
            put(img, sx + 1, sy, col)
            put(img, sx, sy - 1, col)
            put(img, sx, sy + 1, col)
            put(img, sx, sy, (255, 255, 255, 255))

    return force_seamless(img, blend=48)


def dark_base(top, bot) -> Image.Image:
    img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    for y in range(H):
        t = y / (H - 1)
        c = lerp(top + (255,), bot + (255,), t)
        for x in range(W):
            n = hash2(x // 3, y // 3, 1)
            if (x + y) % 2 == 0 and 0.3 < t < 0.8:
                c2 = lerp(c, bot + (255,), 0.08)
                put(img, x, y, c2)
            else:
                put(img, x, y, c if n > 0.02 else lerp(c, (255, 255, 255, 255), 0.05))
    return img


def draw_rect(img, x, y, w, h, c, filled=True):
    if filled:
        for py in range(y, y + h):
            for px in range(x, x + w):
                if 0 <= px < W and 0 <= py < H:
                    put(img, px, py, c)
    else:
        for px in range(x, x + w):
            if 0 <= px < W:
                if 0 <= y < H:
                    put(img, px, y, c)
                if 0 <= y + h - 1 < H:
                    put(img, px, y + h - 1, c)
        for py in range(y, y + h):
            if 0 <= py < H:
                if 0 <= x < W:
                    put(img, x, py, c)
                if 0 <= x + w - 1 < W:
                    put(img, x + w - 1, py, c)


def draw_window(img, x, y, w, h, glow):
    draw_rect(img, x, y, w, h, (12, 18, 28, 255))
    draw_rect(img, x, y, w, h, (40, 50, 70, 255), filled=False)
    for py in range(y + 1, y + h - 1):
        for px in range(x + 1, x + w - 1):
            if 0 <= px < W and 0 <= py < H:
                # stars through window
                if hash2(px, py, 9) > 0.92:
                    put(img, px, py, (220, 240, 255, 255))
                else:
                    put(img, px, py, lerp((8, 12, 24, 255), glow, 0.15 + 0.1 * ((px + py) % 2)))


def make_station() -> Image.Image:
    img = dark_base((10, 12, 28), (20, 28, 48))
    # hull plates
    for i in range(0, W, 40):
        draw_rect(img, i, 40, 36, H - 80, (28, 36, 55, 255), filled=False)
    # floor deck
    draw_rect(img, 0, 280, W, 80, (22, 30, 48, 255))
    for x in range(0, W, 16):
        draw_rect(img, x, 280, 1, 80, (40, 55, 80, 255))
    # rib beams
    for x in (80, 200, 320, 440, 560):
        draw_rect(img, x, 50, 6, 230, (50, 70, 100, 255))
        draw_rect(img, x + 1, 50, 2, 230, (90, 140, 180, 255))
    # viewport windows
    for i, x in enumerate((40, 160, 280, 400, 520)):
        draw_window(img, x, 70, 70, 50, (40, 180, 220, 255) if i % 2 == 0 else (160, 80, 220, 255))
    # docking lights
    for x in range(60, W, 80):
        put(img, x, 270, (255, 214, 92, 255))
        put(img, x + 1, 270, (255, 137, 60, 255))
        put(img, x, 271, (255, 137, 60, 255))
    # ceiling strip lights
    for x in range(20, W, 24):
        draw_rect(img, x, 48, 12, 2, (96, 239, 255, 255))
    return img


def make_lab() -> Image.Image:
    img = dark_base((12, 18, 28), (18, 40, 48))
    # back wall panels
    for x in range(0, W, 48):
        draw_rect(img, x + 4, 40, 40, 200, (24, 40, 50, 255))
        draw_rect(img, x + 4, 40, 40, 200, (40, 70, 80, 255), filled=False)
    # benches
    draw_rect(img, 0, 250, W, 20, (50, 70, 80, 255))
    draw_rect(img, 0, 270, W, 90, (30, 42, 52, 255))
    # monitors
    for x in (60, 220, 380, 520):
        draw_rect(img, x, 90, 70, 50, (10, 20, 24, 255))
        draw_rect(img, x, 90, 70, 50, (43, 205, 219, 255), filled=False)
        for py in range(x, x + 70, 3):
            pass
        for row in range(5):
            for col in range(8):
                if hash2(x + col, 90 + row, 2) > 0.5:
                    put(img, x + 8 + col * 7, 100 + row * 7, (145, 232, 126, 255) if row % 2 == 0 else (96, 239, 255, 255))
    # glassware glow
    for x in (100, 300, 480):
        draw_rect(img, x, 210, 10, 28, (43, 205, 219, 255))
        put(img, x + 4, 214, (96, 239, 255, 255))
        draw_rect(img, x + 18, 220, 8, 18, (207, 126, 255, 255))
    # cable conduits
    for y in (60, 180):
        draw_rect(img, 0, y, W, 2, (70, 190, 102, 255))
    return img


def make_loadout() -> Image.Image:
    img = dark_base((14, 12, 24), (28, 22, 40))
    # hangar bay opening
    draw_rect(img, 180, 40, 280, 160, (8, 10, 22, 255))
    for y in range(40, 200):
        for x in range(180, 460):
            if hash2(x, y, 4) > 0.97:
                put(img, x, y, (200, 220, 255, 255))
    draw_rect(img, 180, 40, 280, 160, (90, 140, 200, 255), filled=False)
    # catwalk
    draw_rect(img, 0, 240, W, 8, (70, 80, 110, 255))
    for x in range(0, W, 20):
        draw_rect(img, x, 248, 2, 112, (50, 60, 90, 255))
    # weapon racks
    for x in (40, 100, 500, 560):
        draw_rect(img, x, 120, 30, 110, (40, 45, 70, 255))
        for yy in range(130, 220, 18):
            draw_rect(img, x + 6, yy, 18, 4, (255, 137, 60, 255) if x < 300 else (43, 205, 219, 255))
    # floor hazard stripes
    for x in range(0, W, 24):
        draw_rect(img, x, 320, 12, 6, (255, 214, 92, 255))
        draw_rect(img, x + 12, 320, 12, 6, (16, 25, 46, 255))
    return img


def make_upgrades() -> Image.Image:
    img = dark_base((20, 12, 18), (40, 22, 28))
    # workshop benches
    draw_rect(img, 0, 260, W, 100, (36, 28, 34, 255))
    draw_rect(img, 0, 250, W, 10, (80, 50, 40, 255))
    # crates
    for x in (50, 140, 400, 520):
        draw_rect(img, x, 200, 50, 50, (60, 40, 35, 255))
        draw_rect(img, x, 200, 50, 50, (255, 137, 60, 255), filled=False)
        draw_rect(img, x + 5, 220, 40, 2, (255, 214, 92, 255))
    # forge glow
    draw_rect(img, 260, 160, 120, 90, (30, 16, 18, 255))
    for y in range(170, 240):
        for x in range(270, 370):
            t = (y - 170) / 70
            if hash2(x, y, 5) > 0.4:
                put(img, x, y, lerp((237, 75, 55, 255), (255, 214, 92, 255), 1 - t))
    # spark pixels
    rng = random.Random(11)
    for _ in range(40):
        put(img, rng.randint(250, 390), rng.randint(140, 200), (255, 214, 92, 255))
    # overhead pipes
    for y in (50, 70):
        draw_rect(img, 0, y, W, 4, (91, 107, 127, 255))
    return img


def make_extract() -> Image.Image:
    img = dark_base((8, 20, 24), (20, 60, 70))
    # success portal / beacon
    cx, cy = 320, 180
    for r, col in (
        (90, (20, 60, 70, 255)),
        (60, (40, 120, 110, 255)),
        (35, (70, 190, 102, 255)),
        (18, (145, 232, 126, 255)),
        (8, (244, 241, 222, 255)),
    ):
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                if 0 <= x < W and 0 <= y < H and (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                    if (x + y) % 2 == 0 or r < 20:
                        put(img, x, y, col)
    # ring
    for a in range(0, 360, 3):
        rad = math.radians(a)
        x = int(cx + math.cos(rad) * 100)
        y = int(cy + math.sin(rad) * 55)
        if 0 <= x < W and 0 <= y < H:
            put(img, x, y, (96, 239, 255, 255))
    # ascending particles
    rng = random.Random(22)
    for _ in range(80):
        put(img, rng.randint(200, 440), rng.randint(40, 300), (145, 232, 126, 255))
    return img


def make_failed() -> Image.Image:
    img = dark_base((24, 8, 12), (48, 16, 18))
    # debris field
    rng = random.Random(33)
    for _ in range(60):
        x, y = rng.randint(20, W - 20), rng.randint(40, H - 40)
        w, h = rng.randint(4, 18), rng.randint(3, 10)
        draw_rect(img, x, y, w, h, (60, 30, 35, 255))
        draw_rect(img, x, y, w, h, (237, 75, 55, 255), filled=False)
    # warning chevrons
    for i in range(8):
        x = 40 + i * 70
        draw_rect(img, x, 300, 40, 8, (255, 214, 92, 255))
        draw_rect(img, x + 40, 300, 20, 8, (16, 25, 46, 255))
    # cracked red glow center
    cx, cy = 320, 160
    for y in range(H):
        for x in range(W):
            d = math.hypot(x - cx, y - cy) / 180
            if d < 1:
                src = get(img, x, y)
                put(img, x, y, lerp(src, (237, 75, 55, 255), (1 - d) * 0.35))
    for _ in range(50):
        put(img, rng.randint(0, W - 1), rng.randint(0, H - 1), (255, 137, 60, 255))
    return img


def main() -> None:
    save(make_seamless_arena(seed=7, cool=True), "ion-veil.png")
    save(make_seamless_arena(seed=19, cool=False), "cinder-belt.png")
    save(make_station(), "station.png")
    save(make_lab(), "research-lab.png")
    save(make_loadout(), "loadout-hangar.png")
    save(make_upgrades(), "upgrades-workshop.png")
    save(make_extract(), "summary-extract.png")
    save(make_failed(), "summary-failed.png")


if __name__ == "__main__":
    main()
