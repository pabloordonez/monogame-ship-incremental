#!/usr/bin/env python3
"""Pixel-clean AI asteroid candidates into exact authored sprite frame tiers."""

from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

REPO = Path(__file__).resolve().parents[1]
AI_DIR = Path(r"C:\Users\PC\.cursor\projects\c-Repositories-github-ship-game\assets")
OUT = REPO / "content" / "source" / "textures" / "sprites"

# Shared MVP-ish palette (rock + ores).
ROCK = [
    (28, 32, 42, 255),
    (48, 54, 68, 255),
    (72, 80, 96, 255),
    (110, 118, 132, 255),
    (160, 168, 180, 255),
]
FERRITE = [(180, 120, 40, 255), (230, 180, 70, 255), (255, 230, 140, 255)]
LUMEN = [(90, 50, 150, 255), (140, 90, 210, 255), (120, 210, 230, 255), (210, 180, 255, 255)]
CRACK = (12, 14, 18, 255)


def nearest(c: tuple[int, int, int, int], palette: list[tuple[int, int, int, int]]) -> tuple[int, int, int, int]:
    r, g, b, a = c
    if a < 16:
        return (0, 0, 0, 0)
    best = palette[0]
    best_d = 1e9
    for p in palette:
        d = (r - p[0]) ** 2 + (g - p[1]) ** 2 + (b - p[2]) ** 2
        if d < best_d:
            best_d = d
            best = p
    return best


def make_transparent(im: Image.Image, threshold: int = 28) -> Image.Image:
    im = im.convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if r <= threshold and g <= threshold and b <= threshold:
                px[x, y] = (0, 0, 0, 0)
    return im


def quantize(im: Image.Image, palette: list[tuple[int, int, int, int]]) -> Image.Image:
    im = im.convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            px[x, y] = nearest(px[x, y], palette)
    return im


def pixelate_from_ai(path: Path, size: int, palette: list[tuple[int, int, int, int]], seed: int) -> Image.Image:
    src = Image.open(path).convert("RGBA")
    src = make_transparent(src, threshold=36)
    # Crop to opaque bounds with margin.
    bbox = src.getbbox()
    if bbox:
        src = src.crop(bbox)
    # Slight deterministic crop/offset for size uniqueness.
    rng = random.Random(seed)
    pad = max(2, size // 8)
    canvas = Image.new("RGBA", (src.width + pad * 2, src.height + pad * 2), (0, 0, 0, 0))
    ox = pad + rng.randint(-pad // 2, pad // 2)
    oy = pad + rng.randint(-pad // 2, pad // 2)
    canvas.paste(src, (ox, oy), src)
    # Downsample with box then nearest for chunky pixels.
    small = canvas.resize((size, size), Image.Resampling.BOX)
    small = small.resize((size, size), Image.Resampling.NEAREST)
    small = make_transparent(small, threshold=40)
    return quantize(small, palette)


def rock_mask(size: int, seed: int, jagged: float = 1.0) -> Image.Image:
    """Crisp procedural silhouette used to reinforce AI downsample readability."""
    rng = random.Random(seed)
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    cx = cy = size / 2
    base_r = size * 0.38
    pts = []
    n = 10 + (seed % 5)
    for i in range(n):
        ang = (math.tau * i / n) + rng.uniform(-0.08, 0.08)
        r = base_r * rng.uniform(0.72, 1.18) * jagged
        pts.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r))
    draw.polygon(pts, fill=(255, 255, 255, 255))
    # Carve a few craters.
    for _ in range(2 + seed % 3):
        rx = rng.randint(size // 5, size - size // 5)
        ry = rng.randint(size // 5, size - size // 5)
        rr = max(1, size // rng.randint(8, 14))
        draw.ellipse((rx - rr, ry - rr, rx + rr, ry + rr), fill=(0, 0, 0, 0))
    return im


def paint_rock(
    size: int,
    kind: str,
    seed: int,
    ai_path: Path,
) -> Image.Image:
    palette = list(ROCK)
    if kind == "ferrite":
        palette += FERRITE
    elif kind == "lumen":
        palette += LUMEN

    base = pixelate_from_ai(ai_path, size, palette, seed)
    mask = rock_mask(size, seed + 17, jagged=1.0 + (seed % 3) * 0.04)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bp = base.load()
    mp = mask.load()
    op = out.load()
    rng = random.Random(seed + 99)

    for y in range(size):
        for x in range(size):
            if mp[x, y][3] < 128:
                continue
            # Prefer AI color when opaque; else procedural rock shade.
            if bp[x, y][3] >= 128:
                c = nearest(bp[x, y], palette)
            else:
                # Radial shading.
                dx = (x + 0.5) / size - 0.45
                dy = (y + 0.5) / size - 0.45
                t = min(1.0, math.hypot(dx, dy) * 1.6)
                idx = min(len(ROCK) - 1, int(t * (len(ROCK) - 1) + rng.random() * 0.2))
                c = ROCK[idx]
            op[x, y] = c

    # Bake ore chunks for resource rocks (ensure readable veins even if AI muddies).
    if kind in ("ferrite", "lumen"):
        ores = FERRITE if kind == "ferrite" else LUMEN
        chunks = 3 + size // 24 + (seed % 2)
        for i in range(chunks):
            cx = rng.randint(size // 5, size - size // 5)
            cy = rng.randint(size // 5, size - size // 5)
            rr = max(1, size // (10 + (i % 3) * 2))
            color = ores[i % len(ores)]
            for yy in range(cy - rr, cy + rr + 1):
                for xx in range(cx - rr, cx + rr + 1):
                    if 0 <= xx < size and 0 <= yy < size and mp[xx, yy][3] >= 128:
                        if (xx - cx) ** 2 + (yy - cy) ** 2 <= rr * rr + rng.randint(0, 1):
                            op[xx, yy] = color
    return out


def apply_damage(im: Image.Image, tier: str, seed: int, kind: str) -> Image.Image:
    if tier == "healthy":
        return im
    out = im.copy()
    draw = ImageDraw.Draw(out)
    rng = random.Random(seed + (11 if tier == "cracked" else 29))
    size = out.width
    px = out.load()

    # Crack lines.
    cracks = 2 if tier == "cracked" else 4
    for _ in range(cracks):
        x0, y0 = rng.randint(size // 6, size - size // 6), rng.randint(size // 6, size - size // 6)
        x1, y1 = rng.randint(size // 6, size - size // 6), rng.randint(size // 6, size - size // 6)
        draw.line((x0, y0, x1, y1), fill=CRACK, width=1 if size <= 32 else 2)

    # Missing wedges / holes for shattered; lighter chips for cracked.
    holes = 1 if tier == "cracked" else 3
    for i in range(holes):
        cx = rng.randint(size // 5, size - size // 5)
        cy = rng.randint(size // 5, size - size // 5)
        rr = max(2, size // (8 if tier == "shattered" else 12) + i)
        draw.ellipse((cx - rr, cy - rr, cx + rr, cy + rr), fill=(0, 0, 0, 0))

    if tier == "shattered" and kind in ("ferrite", "lumen"):
        ores = FERRITE if kind == "ferrite" else LUMEN
        # Expose brighter core ore after shell breaks.
        cx = cy = size // 2
        rr = size // 5
        for yy in range(cy - rr, cy + rr + 1):
            for xx in range(cx - rr, cx + rr + 1):
                if 0 <= xx < size and 0 <= yy < size and px[xx, yy][3] >= 128:
                    if (xx - cx) ** 2 + (yy - cy) ** 2 <= rr * rr:
                        px[xx, yy] = ores[(xx + yy) % len(ores)]
    return out


def debris_from_ai(path: Path, size: int, palette: list[tuple[int, int, int, int]], seed: int) -> Image.Image:
    im = pixelate_from_ai(path, size, palette, seed)
    # Ensure at least a few opaque pixels with a crisp nugget if AI washed out.
    opaque = sum(1 for p in im.getdata() if p[3] >= 128)
    if opaque < size * size * 0.12:
        rng = random.Random(seed)
        draw = ImageDraw.Draw(im)
        pts = []
        cx = cy = size / 2
        for i in range(6):
            ang = math.tau * i / 6 + rng.random() * 0.4
            r = size * rng.uniform(0.25, 0.42)
            pts.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r))
        draw.polygon(pts, fill=palette[-1])
        draw.point((int(cx) - 1, int(cy) - 1), fill=palette[min(1, len(palette) - 1)])
        im = quantize(im, palette)
    return make_transparent(im, threshold=30)


def save(im: Image.Image, rel: str) -> None:
    path = OUT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path)
    print(f"wrote {path.relative_to(REPO)} {im.size}")


def main() -> None:
    ai = {
        "ordinary": AI_DIR / "ai-asteroid-ordinary.png",
        "ferrite": AI_DIR / "ai-asteroid-ferrite.png",
        "lumen": AI_DIR / "ai-asteroid-lumen.png",
    }
    for p in ai.values():
        if not p.exists():
            raise SystemExit(f"missing AI source: {p}")

    sizes = {"small": 32, "medium": 64, "large": 96}
    kinds = ("ordinary", "ferrite", "lumen")
    tiers = ("healthy", "cracked", "shattered")

    for size_name, px in sizes.items():
        for kind in kinds:
            seed = hash((size_name, kind)) & 0xFFFF
            healthy = paint_rock(px, kind, seed, ai[kind])
            for tier in tiers:
                damaged = apply_damage(healthy, tier, seed, kind)
                if tier == "healthy":
                    rel = f"asteroids/{size_name}/{kind}.png"
                else:
                    rel = f"asteroids/{size_name}/{kind}-{tier}.png"
                save(damaged, rel)

    # Debris + pickups from AI nuggets.
    rock_a = debris_from_ai(AI_DIR / "ai-debris-rock.png", 8, ROCK, 1)
    rock_b = debris_from_ai(AI_DIR / "ai-debris-rock.png", 8, ROCK, 2).transpose(Image.Transpose.ROTATE_90)
    fer_a = debris_from_ai(AI_DIR / "ai-debris-ferrite.png", 8, FERRITE + ROCK[:2], 3)
    fer_b = debris_from_ai(AI_DIR / "ai-debris-ferrite.png", 8, FERRITE + ROCK[:2], 4).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    lum_a = debris_from_ai(AI_DIR / "ai-debris-lumen.png", 8, LUMEN + ROCK[:2], 5)
    lum_b = debris_from_ai(AI_DIR / "ai-debris-lumen.png", 8, LUMEN + ROCK[:2], 6).transpose(Image.Transpose.ROTATE_180)

    save(rock_a, "asteroids/debris/rock-a.png")
    save(rock_b, "asteroids/debris/rock-b.png")
    save(fer_a, "asteroids/debris/ferrite-a.png")
    save(fer_b, "asteroids/debris/ferrite-b.png")
    save(lum_a, "asteroids/debris/lumen-a.png")
    save(lum_b, "asteroids/debris/lumen-b.png")

    # Pickups: 10x10 crisp nuggets.
    save(debris_from_ai(AI_DIR / "ai-debris-ferrite.png", 10, FERRITE + ROCK[:1], 7), "pickups/ferrite.png")
    save(debris_from_ai(AI_DIR / "ai-debris-lumen.png", 10, LUMEN + ROCK[:1], 8), "pickups/lumen.png")

    # Remove obsolete break overlay sprite.
    break_path = OUT / "asteroids" / "break.png"
    if break_path.exists():
        break_path.unlink()
        print("removed asteroids/break.png")


if __name__ == "__main__":
    main()
