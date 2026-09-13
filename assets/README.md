# Application icon

Amber gauge on a dark glyph, used for the executable, window/taskbar, and notification-area icon.

- Source: [Lucide gauge](https://lucide.dev/icons/gauge).
- Pinned upstream: [gauge.svg at a79b2d131dab2bf20cb224bd0937b439a9c4fa99](https://github.com/lucide-icons/lucide/blob/a79b2d131dab2bf20cb224bd0937b439a9c4fa99/icons/gauge.svg).
- License: ISC, copyright 2026 Lucide Icons and Contributors; see `LICENSE-lucide.txt`.
- Adaptation: rounded amber background, dark stroke, 32-unit canvas, thicker stroke for small icons.
- `app-icon.svg`: editable vector asset.
- `app-icon.ico`: 16, 20, 24, 32, 40, 48, 64, 128, and 256-pixel Windows images.
- `app-icon.png` / `app-icon-preview.png`: previews.

Run `python scripts/render-icon.py` with Pillow installed to regenerate the committed assets. The script rasterizes the SVG's simple circular gauge geometry; no Python dependency is needed to build or run the app.
