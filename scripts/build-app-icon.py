#!/usr/bin/env python3
"""Builds the app icon and the in-app logo from assets/app-icon/app.svg.

Usage: uv run --with resvg-py==0.5.0 --with pillow==12.1.1 scripts/build-app-icon.py

Every size is drawn from the SVG itself, so small sizes stay sharp instead of being scaled down from 256 px.
"""

import io
import pathlib

import resvg_py
from PIL import Image

# The sizes Windows picks from for the taskbar, title bar, Start and Explorer at 100-200 % scaling.
ICO_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
LOGO_SIZE = 256

root = pathlib.Path(__file__).resolve().parent.parent
source = root / "assets" / "app-icon" / "app.svg"
assets = root / "src" / "App" / "Contr.App" / "Assets"


def render(size: int) -> Image.Image:
    png = resvg_py.svg_to_bytes(svg_path=str(source), width=size, height=size)
    return Image.open(io.BytesIO(bytes(png))).convert("RGBA")


frames = {size: render(size) for size in ICO_SIZES}
largest = frames[max(ICO_SIZES)]
largest.save(
    assets / "app.ico",
    format="ICO",
    sizes=[(size, size) for size in ICO_SIZES],
    append_images=[frames[size] for size in ICO_SIZES if size != max(ICO_SIZES)],
)
render(LOGO_SIZE).save(assets / "ContrLogo.png", optimize=True)
