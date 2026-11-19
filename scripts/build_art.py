#!/usr/bin/env python3
"""Builds a gauge's art: SVG layers -> lit, textured PNGs at 1x/2x/3x plus layout.json, ready to embed.

Usage: python3 scripts/build_art.py art/viper/viper.svg JobGaugeEvolved/Assets/Viper

1. scripts/render_art.js rasterises every layer of the SVG at 3x (Node + Playwright Chromium).
2. Layers with a height pass (<g data-layer="x" data-pass="height">, white = high) are lit here: the height map
   gives the relief (normals), crevices darken (ambient occlusion), the surface gets grain, scratches and mottling,
   and a light from straight above plus a sky reflection make it read as metal, steel, leather or stone. data-*
   attributes on the layer pick the material and tune it (see MATERIALS and shade()). A matte pass
   (data-pass="matte", white = matte) dulls parts of a layer, such as leather; a glow pass (data-pass="glow") is
   laid on top unlit, for things that shine by themselves.
3. Every layer is scaled down to 1x and 2x (premultiplied, Lanczos) and written with layout.json.

Needs numpy and Pillow. The output is committed, so building the plugin never runs this.
"""
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

SCALES = (1, 2, 3)
ROOT = Path(__file__).resolve().parent.parent

# Lighting per material: ambient, diffuse, sky reflection, specular strength and sharpness, occlusion strength,
# grain (relief noise, in gauge units), mottling (albedo noise) and scratch count per 1000 px².
MATERIALS = {
    'metal': dict(ambient=0.20, diffuse=0.82, sky=0.38, specular=1.0, shininess=60, occlusion=1.5,
                  grain=0.16, mottle=0.24, scratches=0.4),
    'steel': dict(ambient=0.30, diffuse=0.70, sky=0.62, specular=1.0, shininess=60, occlusion=0.6,
                  grain=0.05, mottle=0.06, scratches=0.5),
    'leather': dict(ambient=0.40, diffuse=0.70, sky=0.08, specular=0.12, shininess=12, occlusion=0.9,
                    grain=0.25, mottle=0.22, scratches=0.0),
    'stone': dict(ambient=0.38, diffuse=0.72, sky=0.25, specular=0.6, shininess=30, occlusion=0.8,
                  grain=0.06, mottle=0.12, scratches=0.0),
}


def load(path):
    return np.asarray(Image.open(path).convert('RGBA'), dtype=np.float32) / 255.0


def box_blur(a, r, axes=(0, 1)):
    """Two passes of a box blur of radius r (≈ Gaussian), along the given axes."""
    r = int(round(r))
    if r < 1:
        return a
    for _ in range(2):
        for axis in axes:
            n = a.shape[axis]
            pad = [(0, 0)] * a.ndim
            pad[axis] = (r + 1, r)
            c = np.cumsum(np.pad(a, pad, mode='edge'), axis=axis)
            a = (np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
                 - np.take(c, np.arange(0, n), axis=axis)) / (2 * r + 1)
    return a


def noise(shape, cell, rng):
    """Smooth value noise in [-1, 1] with features about `cell` pixels across."""
    h, w = shape
    small = rng.random((h // max(1, cell) + 3, w // max(1, cell) + 3)).astype(np.float32)
    big = Image.fromarray(small, mode='F').resize((w + 2 * cell, h + 2 * cell), Image.BICUBIC)
    a = np.asarray(big)[cell:cell + h, cell:cell + w]
    return (a - a.mean()) / (a.std() + 1e-6) * 0.5


def scratches(shape, count, scale, rng):
    """Fine scratches: thin random lines, mostly near-horizontal."""
    h, w = shape
    img = Image.new('L', (w, h), 0)
    draw = ImageDraw.Draw(img)
    for _ in range(count):
        x, y = rng.random() * w, rng.random() * h
        length = (4 + rng.random() * 14) * scale
        angle = rng.normal(0, 0.5) + (np.pi if rng.random() < 0.5 else 0)
        draw.line([(x, y), (x + np.cos(angle) * length, y + np.sin(angle) * length)],
                  fill=int(90 + rng.random() * 120), width=1)
    return np.asarray(img, dtype=np.float32) / 255.0


def shade(albedo, height, settings, scale, seed, matte=None):
    """Lights a layer from its height map. Light comes from straight above (so mirrored halves match)."""
    m = dict(MATERIALS[settings.get('shade', 'metal')])
    for key in m:
        if key in settings:
            m[key] = float(settings[key])
    rng = np.random.default_rng(seed)
    shape = albedo.shape[:2]
    alpha = albedo[..., 3]
    rgb = albedo[..., :3]

    depth = float(settings.get('depth', 2.5)) * scale          # height range, in pixels
    h = height[..., 0] * height[..., 3] * depth

    # Surface detail: grain in the relief (streaked along x for brushed steel), scratches.
    if settings.get('grain-dir') == 'x':
        grain = box_blur(rng.standard_normal(shape).astype(np.float32), 9 * scale, axes=(1,))
        grain = grain / (grain.std() + 1e-6) * 0.5
    else:
        grain = noise(shape, max(1, int(scale)), rng) * 0.6 + noise(shape, int(4 * scale), rng) * 0.4
    h = h + grain * m['grain'] * scale
    area = shape[0] * shape[1] / (scale * scale)
    scr = scratches(shape, int(area / 1000 * m['scratches']), scale, rng)
    h = h - scr * 0.25 * scale

    # Normals (central differences, lightly smoothed).
    gy, gx = np.gradient(box_blur(h, max(1, scale // 2)))
    n = np.dstack([-gx, -gy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)

    light = np.array([0.0, -0.62, 0.78])
    light /= np.linalg.norm(light)
    half = light + np.array([0.0, 0.0, 1.0])
    half /= np.linalg.norm(half)
    ndl = np.clip(n @ light, 0, 1)
    spec = np.clip(n @ half, 0, 1) ** m['shininess']

    # Sky reflection: surfaces facing up mirror a bright sky, facing down a dark floor.
    ry = 2 * n[..., 2] * n[..., 1]
    sky = np.clip(0.5 - 0.5 * ry, 0, 1) ** 2

    # Crevices (lower than their surroundings) darken, wide hollows and fine engraved lines alike: grime.
    wide = np.clip((box_blur(h, 3 * scale) - h) / (0.6 * depth + 1e-6), 0, 1)
    fine = np.clip((box_blur(h, scale) - h) / (0.25 * depth + 1e-6), 0, 1)
    occlusion = np.clip(1 - m['occlusion'] * (0.6 * wide + 0.5 * fine), 0.12, 1)

    # Mottled albedo: a little painted wear.
    mottle = 1 + m['mottle'] * (noise(shape, int(3 * scale), rng) + 0.5 * noise(shape, int(12 * scale), rng))

    # A matte pass (white = matte) dulls parts of a layer, e.g. leather on a metal layer.
    shine = 1.0 if matte is None else 1 - 0.9 * matte[..., 0] * matte[..., 3]
    lit = rgb * mottle[..., None] * (m['ambient'] + m['diffuse'] * ndl[..., None]
                                     + (m['sky'] * sky * shine)[..., None])
    lit = lit * occlusion[..., None] + (m['specular'] * spec * occlusion * shine)[..., None] * np.array([1.0, 0.98, 0.95])

    return np.dstack([np.clip(lit, 0, 1), alpha])


def over(top, bottom):
    """Straight-alpha "over" compositing."""
    ta, ba = top[..., 3:4], bottom[..., 3:4]
    a = ta + ba * (1 - ta)
    rgb = (top[..., :3] * ta + bottom[..., :3] * ba * (1 - ta)) / np.maximum(a, 1e-6)
    return np.dstack([rgb, a[..., 0]])


def save_scaled(rgba, box, name, out_dir):
    """Writes name@1x/2x/3x.png; rgba is the 3x image (straight alpha)."""
    img = Image.fromarray(np.clip(rgba * 255 + 0.5, 0, 255).astype(np.uint8), 'RGBA')
    for scale in SCALES:
        size = (box['w'] * scale, box['h'] * scale)
        scaled = img if size == img.size else img.convert('RGBa').resize(size, Image.LANCZOS).convert('RGBA')
        scaled.save(out_dir / f'{name}@{scale}x.png', optimize=True)


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    svg, out_dir = Path(sys.argv[1]), Path(sys.argv[2])

    with tempfile.TemporaryDirectory() as work:
        work = Path(work)
        subprocess.run(['node', str(ROOT / 'scripts' / 'render_art.js'), str(svg), str(work)], check=True)
        manifest = json.loads((work / 'manifest.json').read_text())
        scale = manifest['scale']

        out_dir.mkdir(parents=True, exist_ok=True)
        for old in out_dir.glob('*.png'):
            old.unlink()

        layers = {}
        for index, (name, layer) in enumerate(manifest['layers'].items()):
            rgba = load(work / f'{name}.png')
            if 'height' in layer['passes']:
                matte = load(work / f'{name}.matte.png') if 'matte' in layer['passes'] else None
                rgba = shade(rgba, load(work / f'{name}.height.png'), layer['settings'], scale, seed=index + 1,
                             matte=matte)
            if 'glow' in layer['passes']:
                # Painted on top unlit: things that shine by themselves (eyes, gems set in metal).
                rgba = over(load(work / f'{name}.glow.png'), rgba)
            save_scaled(rgba, layer, name, out_dir)
            layers[name] = {k: layer[k] for k in ('x', 'y', 'w', 'h', 'kind', 'slot')}

        layout = {'version': 1, 'width': manifest['width'], 'height': manifest['height'], 'scales': list(SCALES),
                  'layers': layers, 'anchors': manifest['anchors']}
        (out_dir / 'layout.json').write_text(json.dumps(layout, indent=2) + '\n')
        print(f'{len(layers) * len(SCALES)} images and layout.json written to {out_dir}')


if __name__ == '__main__':
    main()
