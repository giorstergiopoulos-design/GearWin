# -*- coding: utf-8 -*-
"""Generates the Inno Setup wizard images of GearWin (large Welcome/Finish image and small header image at 100/125/150/200 %
DPI) from wpf/OptimizerWpf/AppIcon.ico. No text is drawn into the images (the installer is translated into 14 languages).
Run: python installer/make_wizard_images.py   (needs Pillow)"""
import os
from PIL import Image, ImageDraw, ImageFilter

here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, 'wizard')
os.makedirs(out, exist_ok=True)
ico = Image.open(os.path.join(here, '..', 'wpf', 'OptimizerWpf', 'AppIcon.ico'))
sizes = ico.info.get('sizes') or {ico.size}
ico.size = max(sizes, key=lambda s: s[0])
logo = ico.convert('RGBA')


def large(w, h, path):
    bg = Image.new('RGB', (w, h))
    px = bg.load()
    for y in range(h):
        for x in range(w):
            t = y / h * 0.7 + x / w * 0.3
            px[x, y] = (int(8 + 16 * t), int(36 + 56 * t), int(78 + 100 * t))
    glow = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    d = int(w * 0.78)
    cx, cy = (w - d) // 2, int(h * 0.16)
    gd.ellipse([cx - d // 4, cy - d // 4, cx + d + d // 4, cy + d + d // 4], fill=(90, 180, 255, 70))
    glow = glow.filter(ImageFilter.GaussianBlur(d * 0.18))
    bg = Image.alpha_composite(bg.convert('RGBA'), glow)
    l = logo.resize((d, d), Image.LANCZOS)
    bg.alpha_composite(l, (cx, cy))
    dr = ImageDraw.Draw(bg, 'RGBA')
    y = int(h * 0.52)
    bh = max(3, int(h * 0.012))
    gap = int(h * 0.04)
    for k in (0.84, 0.66, 0.78, 0.5, 0.72):
        bw = int((w - 2 * w * 0.1) * k)
        dr.rectangle([int(w * 0.1), y, int(w * 0.1) + bw, y + bh], fill=(255, 255, 255, 70))
        y += gap
    bg.convert('RGB').save(path, 'BMP')


def small(s, path):
    bg = Image.new('RGB', (s, s), (10, 40, 84))
    m = int(s * 0.08)
    l = logo.resize((s - 2 * m, s - 2 * m), Image.LANCZOS)
    bg = bg.convert('RGBA')
    bg.alpha_composite(l, (m, m))
    bg.convert('RGB').save(path, 'BMP')


for k, f in (('100', 1.0), ('125', 1.25), ('150', 1.5), ('200', 2.0)):
    large(round(164 * f), round(314 * f), os.path.join(out, 'large-%s.bmp' % k))
    small(round(55 * f), os.path.join(out, 'small-%s.bmp' % k))
print('ok')
