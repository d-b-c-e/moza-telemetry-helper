"""Rebuild the app's vector-derived PNG/ICO assets. Optional developer tool: pip install Pillow.

Gauge geometry follows Lucide's gauge.svg (ISC); see assets/README.md.
The same simple circular arc/line geometry is emitted as SVG and rasterized below.
No runtime or normal .NET build dependency on Python/Pillow.
"""
from pathlib import Path
from math import cos, sin, radians
from PIL import Image, ImageDraw

assets = Path(__file__).resolve().parents[1] / "assets"
assets.mkdir(exist_ok=True)
svg = '''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 32 32">
  <!-- Gauge adapted from Lucide (ISC). See LICENSE-lucide.txt. -->
  <rect x="1" y="1" width="30" height="30" rx="7" fill="#eea838"/>
  <g transform="translate(4 2)" fill="none" stroke="#171b20" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
    <path d="m12 14 4-4"/>
    <path d="M3.34 19a10 10 0 1 1 17.32 0"/>
  </g>
</svg>
'''
(assets / "app-icon.svg").write_text(svg, encoding="utf-8")

scale = 32  # Supersample the vector geometry before producing each Windows icon size.
canvas = Image.new("RGBA", (32 * scale, 32 * scale))
draw = ImageDraw.Draw(canvas)
amber, ink = "#eea838", "#171b20"
box = lambda xy: tuple(round(value * scale) for value in xy)
draw.rounded_rectangle(box((1, 1, 31, 31)), radius=7 * scale, fill=amber)
# Pillow strokes are inset, so expand the bounds by half the stroke to match the SVG centerline.
stroke = 2.5
draw.arc(box((6 - stroke / 2, 6 - stroke / 2, 26 + stroke / 2, 26 + stroke / 2)),
         start=150, end=390, fill=ink, width=round(stroke * scale))
def cap(x, y):
    radius = stroke / 2
    draw.ellipse(box((x - radius, y - radius, x + radius, y + radius)), fill=ink)

for angle in (150, 390):
    cap(16 + 10 * cos(radians(angle)), 16 + 10 * sin(radians(angle)))
draw.line(box((16, 16, 20, 12)), fill=ink, width=round(stroke * scale))
cap(16, 16)
cap(20, 12)

image = canvas.resize((256, 256), Image.Resampling.LANCZOS)
image.save(assets / "app-icon.png")
image.resize((64, 64), Image.Resampling.LANCZOS).save(assets / "app-icon-preview.png")
image.save(assets / "app-icon.ico", sizes=[(size, size) for size in (16, 20, 24, 32, 40, 48, 64, 128, 256)])
with Image.open(assets / "app-icon.ico") as icon:
    print("ICO sizes:", sorted(icon.ico.sizes()))
