#!/usr/bin/env python3
"""RimWard art: patient landing spot + UI icons, drawn with the Hearth & Harbor SVG house style.
uv run --with resvg-py --with pillow python3 tools/art/build.py"""
import sys, math
from pathlib import Path
sys.path.insert(0, '/Users/julius/Documents/Coding/22_RimWorld/HearthHarbor/tools/art')
from PIL import Image, ImageDraw
from hhart.style import *
from hhart.registry import render_png, add_grain

ROOT = Path(__file__).resolve().parents[2]
TEX = ROOT / "Textures"
OUTD = Path(__file__).resolve().parent / "out"
CROSS = P["red_l"]
MED = P["teal"]


def cross(c, cx, cy, s, fill, sw=3.5):
    t = s * 0.36
    pts = [(-t, -s), (t, -s), (t, -t), (s, -t), (s, t), (t, t), (t, s), (-t, s), (-t, t), (-s, t), (-s, -t), (-t, -t)]
    c.poly([(cx + x, cy + y) for x, y in pts], fill, sw=sw, hl=False)
    # light on top/left, shadow bottom/right
    c.pline([(cx - t + 3, cy - s + 4), (cx + t - 3, cy - s + 4)], light(fill, .35), 2)
    c.pline([(cx - s + 4, cy - t + 3), (cx - t - 1, cy - t + 3)], light(fill, .35), 2)
    c.pline([(cx - t + 3, cy + s - 4), (cx + t - 3, cy + s - 4)], dark(fill, .3), 2)
    c.pline([(cx + t + 1, cy + t - 3), (cx + s - 4, cy + t - 3)], dark(fill, .3), 2)


def spot():
    c = Canvas(1, 1)
    # painted floor pad: teal plate with cream inner field, corner brackets, red cross in a ring
    c.rect(10, 10, 108, 108, MED, r=14, sw=4)
    c.rect(20, 20, 88, 88, P["cream"], r=8, sw=3, hl=False, sh=False)
    # hazard-free corner brackets (stretcher parking ticks)
    for sx, sy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        x = 26 + sx * 76; y = 26 + sy * 76
        dx = 12 * (1 if not sx else -1); dy = 12 * (1 if not sy else -1)
        c.pline([(x, y + dy), (x, y), (x + dx, y)], dark(MED, .1), 5)
    c.circle(64, 64, 33, P["paper"], sw=3.5)
    c.circle(64, 64, 26, P["cream2"], sw=2.2, hl=False, sh=False)
    cross(c, 64, 64, 19, CROSS, sw=3.2)
    # little downward chevrons = "drop here"
    for k in range(2):
        yy = 14 + k * 5
    return c


def hexpts(cx, cy, r, rot=-90):
    return [(cx + r * math.cos(math.radians(rot + 60 * i)), cy + r * math.sin(math.radians(rot + 60 * i))) for i in range(6)]


def icon():
    c = Canvas(1, 1)
    c.circle(64, 64, 58, P["navy"], sw=4)
    c.circle(64, 64, 50, dark(P["navy"], .25), sw=0, hl=False, sh=False)
    # heartbeat line behind
    c.circle(64, 64, 45, P["teal"], sw=0, hl=False, sh=False)
    c.poly(hexpts(64, 64, 40), P["steel"], sw=4, hl=False)
    c.poly(hexpts(64, 64, 31), P["dsteel"], sw=2.5, hl=False)
    cross(c, 64, 64, 22, P["cream"], sw=3.5)
    return c


def save(cv, rel, ppc, grain=0):
    im = render_png(cv.svg(), int(cv.wc * ppc), int(cv.hc * ppc))
    add_grain(im, rel, grain)
    dst = TEX / rel
    dst.parent.mkdir(parents=True, exist_ok=True)
    im.save(dst, optimize=True)
    return im


def main():
    OUTD.mkdir(exist_ok=True)
    ims = {}
    ims["spot"] = save(spot(), "Things/RimWard/PatientLandingSpot.png", 64, 2)
    ims["icon"] = save(icon(), "UI/RimWard/Icon.png", 256)
    ims["small"] = save(icon(), "UI/RimWard/IconSmall.png", 64)
    # contact sheet: floor colour, 2x
    S = 2
    W, H = 900, 380
    sheet = Image.new("RGB", (W, H), (150, 128, 96))
    d = ImageDraw.Draw(sheet)
    x = 20
    for k in ("spot", "icon", "small"):
        im = ims[k]
        if k == "icon":
            im2 = im.resize((256, 256), Image.LANCZOS)
        else:
            im2 = im.resize((im.width * S, im.height * S), Image.NEAREST)
        sheet.paste(im2, (x, 20), im2); x += im2.width + 30
    # readability: 64px cell and 32px
    x = 20
    for k in ("spot", "icon"):
        for sz in (64, 32):
            im2 = ims[k].resize((sz, sz), Image.LANCZOS)
            sheet.paste(im2, (x, 300), im2); x += sz + 12
    # on UI dark bg
    pnl = Image.new("RGB", (200, 70), (52, 50, 48)); sheet.paste(pnl, (680, 290))
    for i, sz in enumerate((64, 32)):
        im2 = ims["icon"].resize((sz, sz), Image.LANCZOS); sheet.paste(im2, (690 + i * 80, 295), im2)
    sheet.save(OUTD / "contact_sheet.png")
    print("ok")

main()
