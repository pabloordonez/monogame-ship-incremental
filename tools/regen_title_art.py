"""One-shot restyle for ships + cosmic backgrounds (arcade / neon dither look)."""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / "content" / "source" / "textures"
WHITE = (255, 255, 255, 255)
INK = (18, 14, 28, 255)
CYAN_CORE = (230, 255, 255, 255)
CYAN_GLOW = (80, 230, 255, 255)


def save_rgba(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG")
    print(f"wrote {path} {img.size}")


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(img: Image.Image, x: int, y: int, c: tuple[int, int, int, int]) -> None:
    w, h = img.size
    if 0 <= x < w and 0 <= y < h:
        img.putpixel((x, y), c)


def outline_mask(mask_pts: set[tuple[int, int]]):
    s = set(mask_pts)
    outline, inner = set(), set()
    for x, y in s:
        edge = False
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            if (x + dx, y + dy) not in s:
                edge = True
                break
        (outline if edge else inner).add((x, y))
    outer = set()
    for x, y in outline:
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1)):
            n = (x + dx, y + dy)
            if n not in s:
                outer.add(n)
    return outer, outline, inner


def ship_from_palette(size: int, build_fn, palette, cockpit_y_range):
    img = new_canvas(size, size)
    pts = build_fn(size)
    outer, edge, body = outline_mask(pts)
    for x, y in outer:
        put(img, x, y, WHITE)
    for x, y in edge:
        put(img, x, y, INK)
    for x, y in body:
        t = y / size
        if t < 0.35:
            c = palette[0]
        elif t < 0.55:
            c = palette[1]
        elif t < 0.75:
            c = palette[2]
        else:
            c = palette[3]
        if abs(x - size // 2) > size * 0.28 and (x + y) % 2 == 0:
            c = palette[3]
        put(img, x, y, c)
    cx = size // 2
    for y in range(*cockpit_y_range):
        for x in range(cx - 1, cx + 2):
            if (x, y) in pts:
                put(img, x, y, CYAN_GLOW)
    put(img, cx, cockpit_y_range[0] + 1, CYAN_CORE)
    return img


def interceptor_mask(size: int):
    pts = set()
    c = size // 2
    for y in range(4, size - 4):
        half = min(6, 1 + (y - 4) // 4)
        if y > size - 10:
            half = max(2, half - (y - (size - 10)))
        for x in range(c - half, c + half + 1):
            pts.add((x, y))
    for y in range(12, 22):
        for x in range(4, 10):
            pts.add((x, y))
            pts.add((size - 1 - x, y))
    return {(x, y) for x, y in pts if 1 <= x < size - 1 and 1 <= y < size - 1}


def gunship_mask(size: int):
    pts = set()
    c = size // 2
    for y in range(8, 54):
        half = 8 if 16 <= y <= 40 else 5
        for x in range(c - half, c + half + 1):
            pts.add((x, y))
    for y in range(20, 44):
        for x in range(8, 20):
            pts.add((x, y))
            pts.add((size - 1 - x, y))
    for y in range(4, 16):
        for x in (22, 23, 40, 41):
            pts.add((x, y))
    return {(x, y) for x, y in pts if 2 <= x < size - 2 and 2 <= y < size - 2}


def sapper_mask(size: int):
    pts = set()
    c = size // 2
    for y in range(6, 40):
        half = max(2, min(8, 3 + (8 - abs(y - 22)) // 3))
        for x in range(c - half, c + half + 1):
            pts.add((x, y))
    for y in range(4, 12):
        pts.add((c - 3, y))
        pts.add((c + 3, y))
    return {(x, y) for x, y in pts if 1 <= x < size - 1 and 1 <= y < size - 1}


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)


def dither_blend(img, x, y, c0, c1, t):
    """Checkerboard dither between two colors by threshold t in [0,1]."""
    use_hi = ((x + y) % 2 == 0 and t > 0.33) or t > 0.66
    put(img, x, y, c1 if use_hi else c0)


def draw_planet(img, cx, cy, r, base, shadow, crater):
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            dx, dy = x - cx, y - cy
            d2 = dx * dx + dy * dy
            if d2 > (r + 1) * (r + 1):
                continue
            if d2 > r * r:
                put(img, x, y, (12, 8, 28, 255))
                continue
            # lighting from bottom-right glow
            lit = (dx * 0.3 + dy * 0.7) / max(1, r)
            t = 0.5 + lit * 0.5
            if t < 0.45:
                dither_blend(img, x, y, shadow, base, t / 0.45)
            else:
                put(img, x, y, lerp(base, (min(255, base[0] + 40), min(255, base[1] + 50), min(255, base[2] + 50)), (t - 0.45) / 0.55))
    # craters
    rng = random.Random(cx * 31 + cy)
    for _ in range(max(2, r // 4)):
        ang = rng.random() * math.tau
        dist = rng.uniform(0.2, 0.7) * r
        cr = max(1, r // 6)
        px = int(cx + math.cos(ang) * dist)
        py = int(cy + math.sin(ang) * dist)
        for y in range(py - cr, py + cr + 1):
            for x in range(px - cr, px + cr + 1):
                if (x - px) ** 2 + (y - py) ** 2 <= cr * cr:
                    put(img, x, y, crater)


def draw_nebula(img, blobs, c_a, c_b):
    w, h = img.size
    for cx, cy, rx, ry, dens in blobs:
        for y in range(max(0, cy - ry), min(h, cy + ry)):
            for x in range(max(0, cx - rx), min(w, cx + rx)):
                nx = (x - cx) / max(1, rx)
                ny = (y - cy) / max(1, ry)
                d = nx * nx + ny * ny
                if d > 1:
                    continue
                fall = 1 - d
                if fall < dens:
                    continue
                src = img.getpixel((x, y))
                t = fall * 0.55
                # dither nebula over existing
                if ((x // 2 + y // 2) % 2 == 0) or fall > 0.75:
                    mix = lerp(src[:3] + (255,), c_a, t)
                else:
                    mix = lerp(src[:3] + (255,), c_b, t * 0.8)
                put(img, x, y, mix)


def make_background(seed: int, warm_accent: bool) -> Image.Image:
    rng = random.Random(seed)
    w, h = 640, 360
    img = new_canvas(w, h)
    top = (12, 8, 36)
    mid = (28, 20, 70)
    bot = (20, 90, 120) if not warm_accent else (40, 50, 90)
    glow = (40, 200, 210) if not warm_accent else (220, 100, 60)

    for y in range(h):
        t = y / (h - 1)
        if t < 0.55:
            c = lerp(top + (255,), mid + (255,), t / 0.55)
        else:
            c = lerp(mid + (255,), bot + (255,), (t - 0.55) / 0.45)
        # bottom glow wash
        if t > 0.7:
            g = (t - 0.7) / 0.3
            c = lerp(c, glow + (255,), g * 0.45)
        for x in range(w):
            # vertical dither banding
            if (x + y) % 2 == 0 and 0.4 < t < 0.85:
                c2 = lerp(c, mid + (255,), 0.15)
                put(img, x, y, c2)
            else:
                put(img, x, y, c)

    # nebulae
    if warm_accent:
        draw_nebula(
            img,
            [(120, 80, 160, 70, 0.35), (480, 140, 180, 90, 0.4), (300, 260, 200, 60, 0.5)],
            (120, 40, 90, 255),
            (60, 20, 80, 255),
        )
    else:
        draw_nebula(
            img,
            [(480, 70, 150, 80, 0.3), (140, 220, 170, 90, 0.35), (360, 160, 220, 100, 0.45)],
            (90, 40, 150, 255),
            (40, 80, 160, 255),
        )

    # planets
    if warm_accent:
        draw_planet(img, 520, 90, 42, (40, 180, 190, 255), (20, 80, 100, 255), (25, 120, 130, 255))
        draw_planet(img, 110, 260, 28, (150, 70, 180, 255), (70, 30, 100, 255), (100, 50, 130, 255))
    else:
        draw_planet(img, 500, 80, 48, (50, 200, 210, 255), (20, 90, 110, 255), (30, 140, 150, 255))
        draw_planet(img, 120, 270, 32, (160, 80, 200, 255), (80, 30, 110, 255), (110, 50, 140, 255))
        draw_planet(img, 280, 100, 12, (80, 100, 200, 255), (40, 50, 120, 255), (50, 60, 140, 255))

    # small orbs
    for _ in range(6):
        ox, oy = rng.randint(40, w - 40), rng.randint(40, h - 40)
        rr = rng.randint(2, 5)
        col = rng.choice([(90, 70, 180, 255), (60, 140, 200, 255), (160, 80, 200, 255)])
        for y in range(oy - rr, oy + rr + 1):
            for x in range(ox - rr, ox + rr + 1):
                if (x - ox) ** 2 + (y - oy) ** 2 <= rr * rr:
                    put(img, x, y, col)

    # stars
    for _ in range(420):
        x, y = rng.randrange(w), rng.randrange(h)
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
        put(img, x, y, col)
        if rng.random() < 0.08:
            # sparkle cross
            put(img, x - 1, y, col)
            put(img, x + 1, y, col)
            put(img, x, y - 1, col)
            put(img, x, y + 1, col)
            put(img, x, y, (255, 255, 255, 255))
        if rng.random() < 0.05:
            put(img, x + 1, y, col)
            put(img, x, y + 1, col)
            put(img, x + 1, y + 1, col)

    return img


def main() -> None:
    sprites = ROOT / "sprites"
    save_rgba(
        ship_from_palette(
            32,
            interceptor_mask,
            [(180, 90, 220, 255), (140, 50, 180, 255), (100, 30, 130, 255), (60, 20, 80, 255)],
            (10, 16),
        ),
        sprites / "enemies/interceptor.png",
    )
    save_rgba(
        ship_from_palette(
            64,
            gunship_mask,
            [(90, 140, 230, 255), (50, 90, 190, 255), (35, 60, 140, 255), (20, 35, 90, 255)],
            (18, 26),
        ),
        sprites / "enemies/gunship.png",
    )
    save_rgba(
        ship_from_palette(
            48,
            sapper_mask,
            [(120, 220, 90, 255), (70, 170, 50, 255), (40, 110, 40, 255), (25, 70, 30, 255)],
            (14, 20),
        ),
        sprites / "enemies/sapper.png",
    )
    elite = new_canvas(64, 64)
    for y in range(64):
        for x in range(64):
            dx, dy = (x - 32) / 28.0, (y - 32) / 28.0
            d = dx * dx + dy * dy
            if 0.85 <= d <= 1.05:
                put(elite, x, y, (255, 220, 80, 230))
            elif 0.75 <= d < 0.85:
                put(elite, x, y, (255, 255, 200, 120))
    save_rgba(elite, sprites / "enemies/elite-outline.png")

    save_rgba(make_background(seed=7, warm_accent=False), ROOT / "backgrounds/ion-veil.png")
    save_rgba(make_background(seed=19, warm_accent=True), ROOT / "backgrounds/cinder-belt.png")


if __name__ == "__main__":
    main()
