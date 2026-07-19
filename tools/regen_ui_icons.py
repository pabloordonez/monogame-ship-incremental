#!/usr/bin/env python3
"""Regenerate clear 32x32 UI glyphs (no AI composite)."""

from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "content" / "source" / "textures" / "sprites" / "ui" / "icons"

PALETTE = [
    (6, 10, 22), (16, 25, 46), (42, 55, 79), (91, 107, 127), (190, 207, 214),
    (244, 241, 222), (43, 205, 219), (96, 239, 255), (237, 75, 55), (255, 137, 60),
    (255, 214, 92), (70, 190, 102), (145, 232, 126), (137, 85, 211), (207, 126, 255),
]
OUTLINE = (16, 25, 46)
SHADOW = (42, 55, 79)
MID = (91, 107, 127)
LIT = (190, 207, 214)
WHITE = (244, 241, 222)
CYAN = (43, 205, 219)
CYAN_LIT = (96, 239, 255)
RED = (237, 75, 55)
ORANGE = (255, 137, 60)
YELLOW = (255, 214, 92)
GREEN = (70, 190, 102)
GREEN_LIT = (145, 232, 126)
VIOLET = (137, 85, 211)
VIOLET_LIT = (207, 126, 255)


def nearest(rgb):
    r, g, b = rgb
    return min(PALETTE, key=lambda c: (r - c[0]) ** 2 + (g - c[1]) ** 2 + (b - c[2]) ** 2)


def clean(img: Image.Image) -> Image.Image:
    img = img.convert("RGBA")
    px = img.load()
    w, h = img.size
    opaque = [[px[x, y][3] >= 128 for x in range(w)] for y in range(h)]
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dst = out.load()
    for y in range(h):
        for x in range(w):
            if opaque[y][x]:
                r, g, b, _ = px[x, y]
                pr, pg, pb = nearest((r, g, b))
                dst[x, y] = (pr, pg, pb, 255)
            else:
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= nx < w and 0 <= ny < h and opaque[ny][nx]:
                        dst[x, y] = (*OUTLINE, 255)
                        break
    for x in range(w):
        dst[x, 0] = (0, 0, 0, 0)
        dst[x, h - 1] = (0, 0, 0, 0)
    for y in range(h):
        dst[0, y] = (0, 0, 0, 0)
        dst[w - 1, y] = (0, 0, 0, 0)
    return out


def canvas():
    return Image.new("RGBA", (32, 32), (0, 0, 0, 0))


def fill(d, box, color):
    d.rectangle(box, fill=(*color, 255))


def poly(d, pts, color):
    d.polygon(pts, fill=(*color, 255))


def draw_all():
    icons = {}

    def ore():
        im, d = canvas(), None
        d = ImageDraw.Draw(im)
        poly(d, [(16, 5), (26, 12), (24, 24), (10, 26), (6, 14)], YELLOW)
        poly(d, [(14, 8), (20, 12), (18, 18), (12, 16)], WHITE)
        return im

    def lumen():
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(d, [(16, 4), (26, 16), (16, 28), (6, 16)], VIOLET_LIT)
        poly(d, [(16, 10), (20, 16), (16, 22), (12, 16)], WHITE)
        return im

    def core():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (8, 8, 23, 23), CYAN)
        fill(d, (11, 11, 20, 20), CYAN_LIT)
        fill(d, (14, 14, 17, 17), WHITE)
        return im

    def lock():
        im = canvas()
        d = ImageDraw.Draw(im)
        d.arc((10, 5, 22, 19), 200, 340, fill=(*LIT, 255), width=3)
        fill(d, (9, 14, 23, 26), MID)
        fill(d, (12, 17, 20, 23), SHADOW)
        fill(d, (15, 18, 17, 22), YELLOW)
        return im

    def chevron():
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(d, [(16, 4), (26, 14), (16, 12), (6, 14)], RED)
        poly(d, [(16, 12), (26, 24), (16, 22), (6, 24)], ORANGE)
        return im

    def drill():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (12, 3, 20, 9), MID)
        poly(d, [(16, 8), (22, 18), (16, 28), (10, 18)], YELLOW)
        poly(d, [(16, 12), (19, 18), (16, 24), (13, 18)], WHITE)
        return im

    def shield(color=CYAN):
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(d, [(16, 4), (26, 10), (24, 22), (16, 28), (8, 22), (6, 10)], color)
        poly(d, [(16, 8), (22, 12), (20, 20), (16, 24), (12, 20), (10, 12)], color if color != CYAN else CYAN_LIT)
        return im

    def engine():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (11, 5, 21, 16), MID)
        fill(d, (13, 7, 19, 14), LIT)
        poly(d, [(10, 16), (22, 16), (19, 28), (13, 28)], CYAN_LIT)
        fill(d, (15, 20, 17, 26), WHITE)
        return im

    def drone():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (5, 13, 10, 19), MID)
        fill(d, (22, 13, 27, 19), MID)
        d.ellipse((10, 9, 22, 23), fill=(*GREEN, 255))
        d.ellipse((13, 12, 19, 18), fill=(*GREEN_LIT, 255))
        fill(d, (15, 14, 17, 16), WHITE)
        return im

    def arrow(color):
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(d, [(16, 4), (26, 15), (20, 15), (20, 27), (12, 27), (12, 15), (6, 15)], color)
        return im

    def bars():
        im = canvas()
        d = ImageDraw.Draw(im)
        for i, ht in enumerate((12, 18, 24)):
            x = 7 + i * 7
            fill(d, (x, 28 - ht, x + 4, 26), ORANGE)
        return im

    def fork():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (14, 14, 18, 27), LIT)
        poly(d, [(16, 14), (8, 5), (12, 5), (16, 11)], YELLOW)
        poly(d, [(16, 14), (24, 5), (20, 5), (16, 11)], YELLOW)
        return im

    def spear():
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(d, [(16, 3), (20, 12), (16, 28), (12, 12)], LIT)
        fill(d, (15, 5, 17, 10), WHITE)
        return im

    def reboot():
        im = canvas()
        d = ImageDraw.Draw(im)
        d.arc((7, 7, 25, 25), 45, 310, fill=(*GREEN_LIT, 255), width=3)
        poly(d, [(22, 6), (28, 13), (18, 13)], GREEN_LIT)
        return im

    def hull():
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(d, [(16, 5), (25, 12), (23, 26), (9, 26), (7, 12)], MID)
        poly(d, [(16, 8), (22, 13), (20, 22), (12, 22), (10, 13)], LIT)
        fill(d, (12, 15, 20, 17), SHADOW)
        return im

    def mobility():
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(d, [(5, 16), (14, 7), (14, 12), (27, 12), (27, 20), (14, 20), (14, 25)], CYAN)
        return im

    def spiral():
        im = canvas()
        d = ImageDraw.Draw(im)
        d.ellipse((6, 6, 26, 26), outline=(*VIOLET_LIT, 255))
        d.ellipse((10, 10, 22, 22), outline=(*CYAN, 255))
        d.ellipse((14, 14, 18, 18), fill=(*WHITE, 255))
        return im

    def bolt():
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(d, [(18, 3), (11, 15), (16, 15), (13, 28), (23, 13), (17, 13)], YELLOW)
        return im

    def beam():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (13, 3, 19, 28), CYAN)
        fill(d, (15, 5, 17, 26), CYAN_LIT)
        fill(d, (16, 8, 16, 22), WHITE)
        return im

    def orb():
        im = canvas()
        d = ImageDraw.Draw(im)
        d.ellipse((7, 7, 25, 25), fill=(*CYAN, 255))
        d.ellipse((11, 11, 19, 19), fill=(*CYAN_LIT, 255))
        fill(d, (14, 14, 16, 16), WHITE)
        return im

    def blink():
        im = canvas()
        d = ImageDraw.Draw(im)
        d.ellipse((4, 9, 15, 23), outline=(*VIOLET, 255))
        d.ellipse((17, 9, 28, 23), fill=(*VIOLET_LIT, 255))
        fill(d, (20, 14, 24, 18), WHITE)
        return im

    def cross(color):
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (13, 5, 19, 27), color)
        fill(d, (5, 13, 27, 19), color)
        return im

    def star():
        im = canvas()
        d = ImageDraw.Draw(im)
        poly(
            d,
            [(16, 3), (19, 12), (28, 12), (21, 18), (24, 28), (16, 22), (8, 28), (11, 18), (4, 12), (13, 12)],
            YELLOW,
        )
        return im

    def hand():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (12, 12, 22, 26), LIT)
        fill(d, (10, 7, 15, 16), WHITE)
        fill(d, (15, 6, 19, 14), WHITE)
        return im

    def pause():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (9, 7, 14, 25), WHITE)
        fill(d, (18, 7, 23, 25), WHITE)
        return im

    def keyboard():
        im = canvas()
        d = ImageDraw.Draw(im)
        fill(d, (5, 10, 27, 23), MID)
        for y in (12, 16, 19):
            for x in range(7, 25, 4):
                fill(d, (x, y, x + 2, y + 2), LIT)
        return im

    def gamepad():
        im = canvas()
        d = ImageDraw.Draw(im)
        d.ellipse((5, 11, 27, 24), fill=(*MID, 255))
        d.ellipse((9, 14, 14, 19), fill=(*CYAN, 255))
        d.ellipse((18, 14, 23, 19), fill=(*RED, 255))
        return im

    icons = {
        "resource-ferrite": ore,
        "resource-lumen": lumen,
        "resource-data-core": core,
        "lock": lock,
        "module-weapon": chevron,
        "module-mining": drill,
        "module-shield": lambda: shield(CYAN),
        "module-engine": engine,
        "module-utility": drone,
        "upgrade-damage": lambda: arrow(RED),
        "upgrade-rate": bars,
        "upgrade-fork": fork,
        "upgrade-pierce": spear,
        "upgrade-shield": lambda: shield(CYAN_LIT),
        "upgrade-reboot": reboot,
        "upgrade-hull": hull,
        "upgrade-speed": lambda: arrow(GREEN),
        "upgrade-mobility": mobility,
        "upgrade-mining": drill,
        "upgrade-tractor": spiral,
        "upgrade-shock": bolt,
        "research-hull": hull,
        "research-shield": lambda: shield(CYAN),
        "research-beam": beam,
        "research-seeker": orb,
        "research-mining": drill,
        "research-assay": lumen,
        "research-engine": engine,
        "research-blink": blink,
        "research-drone": drone,
        "research-tractor": spiral,
        "research-ion": bolt,
        "research-recovery": lambda: cross(GREEN),
        "objective": star,
        "interact": hand,
        "pause": pause,
        "input-keyboard": keyboard,
        "input-gamepad": gamepad,
        "hull": hull,
        "shield": lambda: shield(CYAN_LIT),
    }

    OUT.mkdir(parents=True, exist_ok=True)
    for name, fn in icons.items():
        img = clean(fn())
        path = OUT / f"{name}.png"
        img.save(path)
        print("wrote", path.relative_to(ROOT))


if __name__ == "__main__":
    draw_all()
