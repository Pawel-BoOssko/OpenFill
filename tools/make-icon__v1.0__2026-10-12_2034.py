# OpenFill - generates the application icon (Assets\OpenFill*.ico): a form card with fields being filled and a green check badge.
# Metadata:
#   wersja: 1.0
#   data:   2026-10-12 20:34
# Usage: python tools\make-icon__v1.0__2026-10-12_2015.py <output.ico>      (needs Pillow)
from PIL import Image, ImageDraw, ImageFilter
import sys

S = 2048          # supersampled canvas
OUT = sys.argv[1]

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))

# background: diagonal gradient, rounded square
bg = Image.new("RGBA", (S, S))
px = bg.load()
c1, c2 = (24, 98, 235), (124, 58, 237)   # blue -> violet
for y in range(S):
    for x in range(S):
        t = (x + y) / (2 * S - 2)
        px[x, y] = lerp(c1, c2, t) + (255,)
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([int(S*0.02), int(S*0.02), int(S*0.98), int(S*0.98)], radius=int(S*0.225), fill=255)
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
img.paste(bg, (0, 0), mask)

# soft top highlight
hl = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ImageDraw.Draw(hl).ellipse([int(-S*0.2), int(-S*0.65), int(S*1.2), int(S*0.42)], fill=(255, 255, 255, 38))
hl = Image.composite(hl, Image.new("RGBA", (S, S), (0, 0, 0, 0)), mask)
img = Image.alpha_composite(img, hl)

# form card with shadow
cx0, cy0, cx1, cy1 = int(S*0.20), int(S*0.15), int(S*0.74), int(S*0.82)
sh = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ImageDraw.Draw(sh).rounded_rectangle([cx0+int(S*0.012), cy0+int(S*0.03), cx1+int(S*0.012), cy1+int(S*0.03)], radius=int(S*0.06), fill=(20, 10, 80, 120))
sh = sh.filter(ImageFilter.GaussianBlur(S*0.018))
img = Image.alpha_composite(img, sh)
d = ImageDraw.Draw(img)
d.rounded_rectangle([cx0, cy0, cx1, cy1], radius=int(S*0.06), fill=(255, 255, 255, 255))

# card header bar
pad = int(S*0.055)
hx0, hx1 = cx0 + pad, cx1 - pad
y = cy0 + pad
d.rounded_rectangle([hx0, y, hx0 + int((hx1-hx0)*0.55), y + int(S*0.04)], radius=int(S*0.02), fill=(30, 58, 138, 255))

# three fields: label (small grey bar) + input box with a filled part
field_h = int(S*0.095)
label_h = int(S*0.026)
gap = int(S*0.045)
y += int(S*0.04) + int(S*0.05)
fills = [0.82, 0.62, 0.0]      # how much of each field is already "filled"
fill_col = (59, 130, 246, 255)
for i, f in enumerate(fills):
    d.rounded_rectangle([hx0, y, hx0 + int((hx1-hx0)*0.32), y + label_h], radius=label_h//2, fill=(148, 163, 184, 255))
    y += label_h + int(S*0.018)
    d.rounded_rectangle([hx0, y, hx1, y + field_h], radius=int(S*0.028), fill=(241, 245, 249, 255), outline=(203, 213, 225, 255), width=int(S*0.006))
    if f > 0:
        inset = int(S*0.016)
        d.rounded_rectangle([hx0 + inset, y + inset, hx0 + inset + int((hx1-hx0-2*inset)*f), y + field_h - inset], radius=int(S*0.018), fill=fill_col)
    y += field_h + gap

# check badge (bottom-right): white ring + green disc
bx, by, br = int(S*0.70), int(S*0.745), int(S*0.20)
ring = Image.new("RGBA", (S, S), (0, 0, 0, 0))
rd = ImageDraw.Draw(ring)
rd.ellipse([bx-br-int(S*0.012), by-br-int(S*0.012)+int(S*0.02), bx+br+int(S*0.012), by+br+int(S*0.012)+int(S*0.02)], fill=(10, 20, 60, 110))
ring = ring.filter(ImageFilter.GaussianBlur(S*0.012))
img = Image.alpha_composite(img, ring)
d = ImageDraw.Draw(img)
d.ellipse([bx-br-int(S*0.018), by-br-int(S*0.018), bx+br+int(S*0.018), by+br+int(S*0.018)], fill=(255, 255, 255, 255))
disc = Image.new("RGBA", (2*br, 2*br))
dp = disc.load()
for yy in range(2*br):
    for xx in range(2*br):
        t = yy / (2*br)
        dp[xx, yy] = lerp((52, 211, 120), (16, 160, 80), t) + (255,)
dm = Image.new("L", (2*br, 2*br), 0)
ImageDraw.Draw(dm).ellipse([0, 0, 2*br-1, 2*br-1], fill=255)
img.paste(disc, (bx-br, by-br), dm)
d = ImageDraw.Draw(img)
w = int(S*0.04)
pts = [(bx-int(br*0.46), by+int(br*0.02)), (bx-int(br*0.10), by+int(br*0.38)), (bx+int(br*0.50), by-int(br*0.34))]
d.line(pts, fill=(255, 255, 255, 255), width=w, joint="curve")
for p in (pts[0], pts[2]):
    d.ellipse([p[0]-w//2, p[1]-w//2, p[0]+w//2, p[1]+w//2], fill=(255, 255, 255, 255))

sizes = [256, 128, 64, 48, 32, 24, 16]
frames = [img.resize((s, s), Image.LANCZOS) for s in sizes]
frames[0].save(OUT, format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[1:])
print("ok", OUT)