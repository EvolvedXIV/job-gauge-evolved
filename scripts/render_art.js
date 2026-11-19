#!/usr/bin/env node
// Renders gauge art from SVG into the PNGs and layout.json the plugin embeds.
//
// Usage: node scripts/render_art.js art/viper/viper.svg JobGaugeEvolved/Assets/Viper
//
// Every <g data-layer="name"> in the SVG becomes <name>@1x.png, @2x.png and @3x.png, cropped to what the layer
// actually draws (glows included). layout.json records each layer's box in gauge units (1 unit = 1 px at 100%),
// its kind (color or mask) and slot, plus the reference points in <g id="anchors">. The PNGs are committed, so
// building the plugin never needs this script; run it again after changing the SVG.
//
// Needs Node and Playwright with Chromium (`npm i -g playwright && npx playwright install chromium`).
'use strict';

const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const SCALES = [1, 2, 3];
const PAD = 40; // room around the canvas for glows, in gauge units

async function main() {
  const [svgPath, outDir] = process.argv.slice(2);
  if (!svgPath || !outDir) {
    console.error('Usage: node scripts/render_art.js <art.svg> <output folder>');
    process.exit(2);
  }

  const svgText = fs.readFileSync(svgPath, 'utf8');
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent('<!doctype html><html><body></body></html>');
    const result = await page.evaluate(renderInPage, { svgText, scales: SCALES, pad: PAD });

    fs.mkdirSync(outDir, { recursive: true });
    for (const old of fs.readdirSync(outDir)) {
      if (old.endsWith('.png')) fs.unlinkSync(path.join(outDir, old));
    }
    for (const file of result.files) {
      fs.writeFileSync(path.join(outDir, file.name), Buffer.from(file.base64, 'base64'));
    }
    fs.writeFileSync(path.join(outDir, 'layout.json'), JSON.stringify(result.layout, null, 2) + '\n');
    console.log(`${result.files.length} images and layout.json written to ${outDir}`);
  } finally {
    await browser.close();
  }
}

// Runs inside Chromium.
async function renderInPage({ svgText, scales, pad }) {
  const doc = new DOMParser().parseFromString(svgText, 'image/svg+xml');
  const root = doc.documentElement;
  const width = Number(root.getAttribute('width'));
  const height = Number(root.getAttribute('height'));
  const maxScale = Math.max(...scales);

  async function rasterize(layer, scale) {
    const clone = doc.cloneNode(true);
    const svg = clone.documentElement;
    for (const g of [...svg.querySelectorAll('[data-layer]')]) {
      if (g.getAttribute('data-layer') !== layer) g.remove();
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
  function boundsOf(canvas, scale) {
    const { data, width: w, height: h } = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height);
    let minX = w, minY = h, maxX = -1, maxY = -1;
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
    if (maxX < 0) throw new Error('empty layer');
    const x0 = Math.floor(minX / scale) - pad, y0 = Math.floor(minY / scale) - pad;
    const x1 = Math.ceil((maxX + 1) / scale) - pad, y1 = Math.ceil((maxY + 1) / scale) - pad;
    return { x: x0, y: y0, w: x1 - x0, h: y1 - y0 };
  }

  const files = [];
  const layers = {};
  for (const g of root.querySelectorAll('[data-layer]')) {
    const name = g.getAttribute('data-layer');
    const full = await rasterize(name, maxScale);
    const box = boundsOf(full, maxScale);
    // Rendered once at the largest scale and scaled down for the others: SVG lighting filters work per device
    // pixel, so rendering each scale separately would make bevels look different at each size.
    const master = document.createElement('canvas');
    master.width = box.w * maxScale;
    master.height = box.h * maxScale;
    master.getContext('2d').drawImage(full, (box.x + pad) * maxScale, (box.y + pad) * maxScale, master.width,
      master.height, 0, 0, master.width, master.height);
    for (const scale of scales) {
      const out = document.createElement('canvas');
      out.width = box.w * scale;
      out.height = box.h * scale;
      const ctx = out.getContext('2d');
      ctx.imageSmoothingEnabled = true;
      ctx.imageSmoothingQuality = 'high';
      ctx.drawImage(master, 0, 0, out.width, out.height);
      files.push({ name: `${name}@${scale}x.png`, base64: out.toDataURL('image/png').split(',')[1] });
    }
    layers[name] = { ...box, kind: g.getAttribute('data-kind') || 'color', slot: g.getAttribute('data-slot') || null };
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

  return { files, layout: { version: 1, width, height, scales, layers, anchors } };
}

main().catch(e => {
  console.error(e);
  process.exit(1);
});
