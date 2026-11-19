#!/usr/bin/env node
// Rasterises the layers of a gauge SVG at the largest scale, for scripts/build_art.py to finish.
//
// Usage: node scripts/render_art.js <art.svg> <work folder>
//
// Every <g data-layer="name"> becomes <name>.png, cropped to what the layer draws (glows included), and every
// <g data-layer="name" data-pass="height"> becomes <name>.height.png, cropped to the same box. manifest.json lists
// each layer's box in gauge units, its kind, slot and data-* settings, plus the reference points in
// <g id="anchors">. build_art.py lights the layers that have a height pass, scales everything down to 1x/2x/3x and
// writes the files the plugin embeds; run that script rather than this one.
//
// Needs Node and Playwright with Chromium.
'use strict';

const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const SCALE = 3;
const PAD = 40; // room around the canvas for glows, in gauge units

async function main() {
  const [svgPath, outDir] = process.argv.slice(2);
  if (!svgPath || !outDir) {
    console.error('Usage: node scripts/render_art.js <art.svg> <work folder>');
    process.exit(2);
  }

  const svgText = fs.readFileSync(svgPath, 'utf8');
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent('<!doctype html><html><body></body></html>');
    const result = await page.evaluate(renderInPage, { svgText, scale: SCALE, pad: PAD });

    fs.mkdirSync(outDir, { recursive: true });
    for (const file of result.files) {
      fs.writeFileSync(path.join(outDir, file.name), Buffer.from(file.base64, 'base64'));
    }
    fs.writeFileSync(path.join(outDir, 'manifest.json'), JSON.stringify(result.manifest, null, 2) + '\n');
    console.log(`${result.files.length} raw images written to ${outDir}`);
  } finally {
    await browser.close();
  }
}

// Runs inside Chromium.
async function renderInPage({ svgText, scale, pad }) {
  const doc = new DOMParser().parseFromString(svgText, 'image/svg+xml');
  const root = doc.documentElement;
  const width = Number(root.getAttribute('width'));
  const height = Number(root.getAttribute('height'));

  const keyOf = g => `${g.getAttribute('data-layer')}|${g.getAttribute('data-pass') || ''}`;

  async function rasterize(key) {
    const clone = doc.cloneNode(true);
    const svg = clone.documentElement;
    for (const g of [...svg.querySelectorAll('[data-layer]')]) {
      if (keyOf(g) !== key) g.remove();
    }
    svg.setAttribute('viewBox', `${-pad} ${-pad} ${width + 2 * pad} ${height + 2 * pad}`);
    svg.setAttribute('width', String((width + 2 * pad) * scale));
    svg.setAttribute('height', String((height + 2 * pad) * scale));
    const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(clone)], { type: 'image/svg+xml' }));
    try {
      const img = new Image();
      img.src = url;
      await img.decode();
      const canvas = document.createElement('canvas');
      canvas.width = (width + 2 * pad) * scale;
      canvas.height = (height + 2 * pad) * scale;
      canvas.getContext('2d').drawImage(img, 0, 0);
      return canvas;
    } finally {
      URL.revokeObjectURL(url);
    }
  }

  // Box of the pixels a layer covers, in whole gauge units.
  function boundsOf(canvases) {
    let minX = Infinity, minY = Infinity, maxX = -1, maxY = -1;
    for (const canvas of canvases) {
      const { data, width: w, height: h } = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height);
      for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
          if (data[(y * w + x) * 4 + 3] > 2) {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
          }
        }
      }
    }
    if (maxX < 0) throw new Error('empty layer');
    const x0 = Math.floor(minX / scale) - pad, y0 = Math.floor(minY / scale) - pad;
    const x1 = Math.ceil((maxX + 1) / scale) - pad, y1 = Math.ceil((maxY + 1) / scale) - pad;
    return { x: x0, y: y0, w: x1 - x0, h: y1 - y0 };
  }

  function crop(canvas, box) {
    const out = document.createElement('canvas');
    out.width = box.w * scale;
    out.height = box.h * scale;
    out.getContext('2d').drawImage(canvas, (box.x + pad) * scale, (box.y + pad) * scale, out.width, out.height,
      0, 0, out.width, out.height);
    return out.toDataURL('image/png').split(',')[1];
  }

  const files = [];
  const layers = {};
  for (const g of root.querySelectorAll('[data-layer]:not([data-pass])')) {
    const name = g.getAttribute('data-layer');
    const passes = [...root.querySelectorAll(`[data-layer="${name}"][data-pass]`)].map(p => p.getAttribute('data-pass'));
    const main = await rasterize(`${name}|`);
    const extra = {};
    for (const pass of passes) extra[pass] = await rasterize(`${name}|${pass}`);
    const box = boundsOf([main]);

    files.push({ name: `${name}.png`, base64: crop(main, box) });
    for (const pass of passes) files.push({ name: `${name}.${pass}.png`, base64: crop(extra[pass], box) });

    const settings = {};
    for (const attr of g.attributes) {
      if (attr.name.startsWith('data-') && !['data-layer', 'data-kind', 'data-slot'].includes(attr.name)) {
        const value = Number(attr.value);
        settings[attr.name.slice(5)] = Number.isNaN(value) ? attr.value : value;
      }
    }

    layers[name] = {
      ...box,
      kind: g.getAttribute('data-kind') || 'color',
      slot: g.getAttribute('data-slot') || null,
      passes,
      settings,
    };
  }

  const anchors = {};
  for (const el of root.querySelectorAll('#anchors > *')) {
    const name = el.id.replace(/^anchor-/, '');
    const anchor = el.tagName === 'rect'
      ? { x: +el.getAttribute('x'), y: +el.getAttribute('y'), w: +el.getAttribute('width'), h: +el.getAttribute('height') }
      : { x: +el.getAttribute('cx'), y: +el.getAttribute('cy') };
    for (const attr of el.attributes) {
      if (attr.name.startsWith('data-')) anchor[attr.name.slice(5)] = Number(attr.value);
    }
    anchors[name] = anchor;
  }

  return { files, manifest: { width, height, scale, layers, anchors } };
}

main().catch(e => {
  console.error(e);
  process.exit(1);
});
