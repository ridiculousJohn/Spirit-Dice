"""Author the Sunken Larder's ambient steam: wisps rising from the cauldron on two drifting tile layers, seen through a plume-shaped window.

Python 3.10 or later with numpy, Pillow and scipy (on this machine Docs/work/merchant-table-clear/.venv/Scripts/python.exe).
Run from the repository root:

    python Tools/Presentation/author-sunken-larder-ambient.py                 # fresh Docs/work/sunken-larder-ambient-* folder
    python Tools/Presentation/author-sunken-larder-ambient.py --write-assets  # also installs the textures and clips

It reads the installed Sunken Larder plates and the kept steam donor in Docs/Reference/Art (see
Docs/Reference/Art/SunkenLarderLayers.md, "Ambient"), so reruns give the same files. The method is the Bound Tree's mist
(author-bound-tree-ambient.py, whose tile, clip and image helpers it imports): a transparent generated donor made into a
wrapping tile, drifted by a legacy clip moving a RawImage's uvRect, with an occluder drawing the background's lit plates
above it through a mask.

Steam: the donor (an even field of rising wisps on transparency) has its low-frequency density flattened, so no denser
band pulses past as it scrolls, and is made to wrap both ways by a premultiplied cross-fade of its last columns into its
first and its last rows into its first, so the tile can rise and drift sideways forever. Each layer (LAYERS) scales,
optionally flips and softens it into a tile at concept resolution. The fire-lit tile, the scene's light 0 reference, has
its alpha-weighted mean colour set in linear light to the painted steam's bright colour (the mean of the brightest tenth
of the plate's pixels inside the plume's core); the unlit tile, the base, is the lit one times the plates' own
unlit-over-lit ratio in the plume (median per channel, linear light), so the steam dims and flickers with the fire as a
lit layer. Each layer's clip moves its uvRect over one tile sideways and a whole number of tiles upward in one period,
so the wisps rise and drift right along the painted plume's lean and the loop is seamless; the broad layer also swells
its opacity in whole cycles of its period.

Plume window: the painted steam stays on the plate. The occluder, drawn after both layers, draws the background's lit
plates through the inverted plume mask (ENVELOPE: a soft band around the painted plume's centre line, rising from zero
at the pot's rim, thinning with height and gone below the herb bundles, kept off the toy horse), so the moving wisps show
only inside the painted plume and read as that steam moving. Her figure is drawn after the occluder, so no steam crosses
her.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import math
import tempfile
import uuid

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, grey_opening, map_coordinates

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / 'Tools/Presentation'
ASSETS = ROOT / 'Dice-Unity/Assets/Dice'
ART = ASSETS / 'Art/SunkenLarder'
RELIGHT = ART / 'Relight'
CONTENT = ASSETS / 'Content/Presentation/SunkenLarder'
REF = ROOT / 'Docs/Reference/Art'
WORK = ROOT / 'Docs/work'
W, H = 941, 1672
# The encounter's Sunken Larder Scene frame is 540 x 960 units over the concept's 941 x 1672 pixels.
FRAME = (540.0, 960.0)
SX, SY = FRAME[0] / W, FRAME[1] / H


def _load_module(name, file):
    spec = importlib.util.spec_from_file_location(name, TOOLS / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


BTA = _load_module('author_bound_tree_ambient', 'author-bound-tree-ambient.py')
to_linear, to_srgb = BTA.to_linear, BTA.to_srgb
load, save, rel, labelled, row, column = BTA.load, BTA.save, BTA.rel, BTA.labelled, BTA.row, BTA.column

CONCEPT = ART / 'SunkenLarderOriginal.png'
BACKGROUND = ART / 'SunkenLarderBackground.png'          # light 0 reference: the plate as painted
UNLIT = RELIGHT / 'SunkenLarderAmbientUnlit.png'          # the base: the room with the fire out
HAG = ART / 'SunkenLarderHag.png'
DONOR = REF / 'SunkenLarderSteamDonor.png'

# ----------------------------------------------------------------------------------------------------------- steam
DENSITY_SIGMA = 160.0   # donor px: the donor's alpha is divided by its Gaussian average at this scale (density flattened)
DENSITY_FLOOR = 0.25    # ... never dividing by less than this fraction of its mean
OVERLAP = (192, 256)    # donor columns and rows cross-faded at the wrap
# The drifting layers, in draw order. scale: concept px per donor px; flip: mirrored left to right, so the two layers
# never repeat one pattern; soften: sigma (donor px) of a blur of the tile, for broader, softer clouds; opacity: the
# layer colour's alpha at rest; rise and drift: concept px per s upward and to the right (the painted plume leans right
# as it rises); swell: the opacity's swell either way, swell_cycles times per period. The drift moves one tile per
# period and the rise a whole number of tiles, the nearest to the asked speed.
LAYERS = {
    'Broad': {'scale': 0.48, 'flip': True, 'soften': 5.0, 'opacity': 0.7, 'rise': 13.0, 'drift': 2.4,
              'swell': 0.08, 'swell_cycles': 2},
    'Wisps': {'scale': 0.30, 'flip': False, 'soften': 0.0, 'opacity': 0.9, 'rise': 23.0, 'drift': 3.6,
              'swell': 0.0, 'swell_cycles': 0},
}
# The plume window: the painted plume's centre line and half width by concept row (row, centre x, half width), the
# lateral profile (full inside FULL_INSIDE of the half width, none beyond NONE_BEYOND) and the profile by height (rows
# and levels, interpolated with smoothstep between them): zero at the pot's rim (row 556), full above it, thinning with
# height, gone at row 205 below the herb bundles.
CENTRE = [(560, 98, 70), (520, 96, 76), (470, 104, 80), (420, 118, 76), (360, 135, 70), (300, 148, 64), (240, 156, 60),
          (200, 160, 58)]
FULL_INSIDE, NONE_BEYOND = 0.6, 1.35
HEIGHT_LEVELS = [(556, 0.0), (520, 1.0), (400, 1.0), (300, 0.8), (205, 0.0)]
# The toy horse and the jars on the shelf right of the plume stay clear: the window ends at this column (soft over
# HORSE_SOFT px) over these rows (soft over the same).
HORSE_EDGE, HORSE_SOFT, HORSE_ROWS = 186.0, 12.0, (250, 400)
# Inside that envelope the window follows the painted steam: its level is PRESENCE_FLOOR plus the rest times the
# painted steam's presence, the plate's luminance above its grey opening (PRESENCE_OPENING px, blurred PRESENCE_BLUR
# px), normalised by its PRESENCE_PERCENTILE inside the envelope and softened by PRESENCE_SOFTEN px, so the moving wisps
# are strongest where steam is painted and faint between its wisps.
PRESENCE_OPENING, PRESENCE_BLUR, PRESENCE_SOFTEN, PRESENCE_PERCENTILE, PRESENCE_FLOOR = 61, 12.0, 6.0, 95.0, 0.6
CORE_LEVEL = 0.8        # the steam colour is measured where the window is at least this
CORE_ROWS = (400, 545)  # ... between these rows (the plume's body above the pot)
BRIGHT_FRACTION = 0.1   # ... as the mean of the brightest tenth of the plate's pixels there
RATIO_LEVEL = 0.5       # the unlit ratio is measured where the window is at least this
CLIP_KEYS_PER_CYCLE = 8
REVIEW_TIMES = [0.0, 1.5, 3.0, 4.5, 6.0, 7.5, 9.0, 10.5]
REVIEW_BOX = (0, 160, 330, 600)   # concept box of the plume review

OUT = None


def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


# ========================================================================================================= window
def window(plate):
    """The plume window: the envelope, following the painted steam's presence inside it."""
    env = envelope()
    lum = plate @ np.array([0.2126, 0.7152, 0.0722])
    floor = gaussian_filter(grey_opening(lum, size=(PRESENCE_OPENING, PRESENCE_OPENING)), PRESENCE_BLUR)
    excess = gaussian_filter(np.clip(lum - floor, 0, None), PRESENCE_SOFTEN)
    presence = np.clip(excess / np.percentile(excess[env > 0.5], PRESENCE_PERCENTILE), 0, 1)
    return env * (PRESENCE_FLOOR + (1 - PRESENCE_FLOOR) * presence), env


def envelope():
    """The plume envelope over the concept frame, 0 to 1."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float64) + 0.5
    rows = np.array([c[0] for c in CENTRE], float)[::-1]
    centre = np.interp(yy, rows, np.array([c[1] for c in CENTRE], float)[::-1])
    half = np.interp(yy, rows, np.array([c[2] for c in CENTRE], float)[::-1])
    d = np.abs(xx - centre) / half
    lateral = 1 - smoothstep((d - FULL_INSIDE) / (NONE_BEYOND - FULL_INSIDE))
    height = np.zeros_like(yy)
    levels = sorted(HEIGHT_LEVELS)
    for (y0, l0), (y1, l1) in zip(levels, levels[1:]):
        inside = (yy >= y0) & (yy < y1)
        height[inside] = l0 + (l1 - l0) * smoothstep((yy[inside] - y0) / (y1 - y0))
    lo, hi = HORSE_ROWS
    rows_w = smoothstep((yy - lo + HORSE_SOFT) / HORSE_SOFT) * smoothstep((hi + HORSE_SOFT - yy) / HORSE_SOFT)
    horse = 1 - rows_w * smoothstep((xx - HORSE_EDGE + HORSE_SOFT) / HORSE_SOFT)
    return lateral * height * horse


def window_rect(env, margin=4):
    ys, xs = np.nonzero(env > 0.002)
    x0, y0 = max(0, xs.min() - margin), max(0, ys.min() - margin)
    x1, y1 = min(W, xs.max() + 1 + margin), min(H, ys.max() + 1 + margin)
    return (int(x0), int(y0), int(x1 - x0), int(y1 - y0))


def map_rect(rect):
    x, y, w, h = rect
    return [x / W, 1 - (y + h) / H, w / W, h / H]


# ========================================================================================================== tiles
def flattened_donor():
    """The donor premultiplied, its colour spread under transparent texels and its alpha's low-frequency density divided out."""
    D = load(DONOR, 'RGBA')
    a = D[..., 3] / 255.0
    rgb = D[..., :3]
    w = (a > 0.02) * a
    spread = gaussian_filter(rgb * w[..., None], (3, 3, 0)) / np.maximum(gaussian_filter(w, 3), 1e-6)[..., None]
    rgb = np.where((a > 0.02)[..., None], rgb, spread)
    density = gaussian_filter(a, DENSITY_SIGMA, mode='reflect')
    a = np.clip(a * a.mean() / np.maximum(density, DENSITY_FLOOR * a.mean()), 0, 1)
    return np.concatenate([rgb * a[..., None], a[..., None]], -1)


def wrap_both(pm, overlap=OVERLAP):
    """Cross-fades the last overlap columns into the first and then the last overlap rows into the first (premultiplied)."""
    ox, oy = overlap
    n = pm.shape[1] - ox
    t = pm[:, :n].copy()
    ramp = (np.arange(ox) + 0.5) / ox
    t[:, :ox] = pm[:, :ox] * ramp[None, :, None] + pm[:, n:n + ox] * (1 - ramp[None, :, None])
    m = t.shape[0] - oy
    u = t[:m].copy()
    ramp = (np.arange(oy) + 0.5) / oy
    u[:oy] = t[:oy] * ramp[:, None, None] + t[m:m + oy] * (1 - ramp[:, None, None])
    return u


def layer_tile(base_pm, spec):
    """A layer's premultiplied tile at concept resolution, wrapping both ways."""
    t = base_pm[:, ::-1] if spec['flip'] else base_pm
    if spec['soften'] > 0:
        t = gaussian_filter(t, (spec['soften'], spec['soften'], 0), mode='wrap')
    return BTA.resize_pm(np.ascontiguousarray(t), spec['scale'])


def coloured(tile_pm, target_linear):
    """The tile's straight colour scaled per channel in linear light so its alpha-weighted mean is the target."""
    a = tile_pm[..., 3]
    lin = to_linear(np.clip(tile_pm[..., :3] / np.maximum(a, 1e-6)[..., None], 0, 255))
    mean = (lin * a[..., None]).sum((0, 1)) / a.sum()
    gain = target_linear / np.maximum(mean, 1e-6)
    return np.clip(lin * gain, 0, 1), a, gain


def steam_colour(plate, env):
    """The painted steam's bright colour: the mean of the brightest tenth of the plate inside the plume's core."""
    yy = np.arange(H)[:, None].repeat(W, 1)
    core = (env >= CORE_LEVEL) & (yy >= CORE_ROWS[0]) & (yy < CORE_ROWS[1])
    px = plate[core]
    lum = px @ np.array([0.2126, 0.7152, 0.0722])
    bright = px[lum >= np.quantile(lum, 1 - BRIGHT_FRACTION)]
    return to_linear(bright).mean(0), bright.mean(0), int(core.sum())


def unlit_ratio(plate, unlit, env):
    m = env >= RATIO_LEVEL
    return np.median(to_linear(unlit[m]) / np.maximum(to_linear(plate[m]), 1e-5), 0), int(m.sum())


# ========================================================================================================== clips
def motion(spec, tile):
    """A layer's clip timing: period (s), whole tiles risen per period and the speeds that follow, in concept px per s."""
    th, tw = tile.shape[:2]
    period = tw / spec['drift']
    rises = max(1, int(round(period * spec['rise'] / th)))
    return period, rises, rises * th / period, tw / period


def layer_clip(spec, tile, rect):
    """uvRect x falls by one tile and uvRect y by whole tiles over the period (the wisps drift right and rise), the
    texture wrapping both ways; the broad layer also swells its colour's alpha."""
    period, rises, _, _ = motion(spec, tile)
    curves = [('m_UVRect.x', [(0.0, 0.0, -1 / period), (period, -1.0, -1 / period)]),
              ('m_UVRect.y', [(0.0, 0.0, -rises / period), (period, -float(rises), -rises / period)])]
    if spec['swell'] > 0:
        curves.append(('m_Color.a', BTA.sine_keys(spec['swell'], spec['swell_cycles'], period, spec['opacity'])))
    return curves, period


def layer_state(spec, tile, t):
    """The layer's uvRect origin (tiles) and opacity at t seconds of its clip."""
    period, rises, _, _ = motion(spec, tile)
    phase = (t % period) / period
    alpha = spec['opacity'] + spec['swell'] * math.sin(2 * math.pi * spec['swell_cycles'] * phase)
    return -phase, -rises * phase, alpha


# ======================================================================================================== compose
def sample_layer(tile_lin, tile_a, rect, uv_origin):
    """The layer's straight linear colour and alpha over the frame at a uvRect origin, as the RawImage samples its
    wrapping texture: the rect spans rect width / tile width tiles across and rect height / tile height up."""
    x, y, w, h = rect
    th, tw = tile_a.shape
    ux, uy = uv_origin
    cols = np.arange(w) + 0.5
    rows = np.arange(h) + 0.5
    u = ux + cols / tw                       # tiles, left to right
    v = uy + (h - rows) / th                 # tiles, bottom to top
    tx = (u * tw - 0.5) % tw
    ty = ((1 - v) * th - 0.5) % th
    gy, gx = np.meshgrid(ty, tx, indexing='ij')
    coords = [gy, gx]
    a = map_coordinates(tile_a, coords, order=1, mode='grid-wrap')
    pm = np.stack([map_coordinates(tile_lin[..., c] * tile_a, coords, order=1, mode='grid-wrap') for c in range(3)], -1)
    rgb, alpha = np.zeros((H, W, 3)), np.zeros((H, W))
    rgb[y:y + h, x:x + w] = pm / np.maximum(a, 1e-6)[..., None]
    alpha[y:y + h, x:x + w] = a
    return rgb, alpha


def compose(plate_lin, layers, env, rect, t, intensity_layers=None):
    """The plate with both layers drawn over it and the occluder drawing the plate through the inverted window, in
    linear light. intensity_layers replaces each layer's colour (for the unlit and bright reviews)."""
    out = plate_lin.copy()
    for name, spec in LAYERS.items():
        L = layers[name]
        ux, uy, opacity = layer_state(spec, L['tile'], t)
        lin = L['lin'] if intensity_layers is None else intensity_layers[name]
        rgb, a = sample_layer(lin, L['a'], rect, (ux, uy))
        a = a * opacity
        out = out * (1 - a[..., None]) + rgb * a[..., None]
    return plate_lin * (1 - env[..., None]) + out * env[..., None]


def srgb8(lin):
    return to_srgb(np.clip(lin, 0, 1))


def crop(img, box, scale=1.0):
    x0, y0, x1, y1 = box
    c = np.clip(img[y0:y1, x0:x1], 0, 255).astype(np.uint8)
    im = Image.fromarray(c)
    if scale != 1.0:
        im = im.resize((int(round(im.width * scale)), int(round(im.height * scale))), Image.LANCZOS)
    return np.asarray(im).astype(np.float64)


def reviews(rdir, plate, unlit, hag, layers, env, rect, report):
    rdir.mkdir(parents=True, exist_ok=True)
    plate_lin, unlit_lin = to_linear(plate), to_linear(unlit)
    hag_a = hag[..., 3:] / 255.0

    def with_her(lin_img):
        return srgb8(lin_img) * (1 - hag_a) + hag[..., :3] * hag_a

    box = REVIEW_BOX
    frames = [labelled(crop(with_her(plate_lin), box, 1.5), 'plate (no steam layers)')]
    for t in REVIEW_TIMES:
        frames.append(labelled(crop(with_her(compose(plate_lin, layers, env, rect, t)), box, 1.5), f't = {t:.1f} s'))
    sheet = column([row(frames[:5]), row(frames[5:])])
    save(rdir / 'steam-frames.png', sheet)

    # every second for a quarter minute, the plume at the concept's scale
    seq = [labelled(crop(with_her(compose(plate_lin, layers, env, rect, float(t))), box), f'{t} s', 13) for t in range(16)]
    save(rdir / 'steam-sequence-1s.png', column([row(seq[i:i + 8]) for i in range(0, 16, 8)]))

    # the window: its level tinted cyan over the plate, the occluder's rect outlined
    tint = srgb8(plate_lin) * (1 - 0.6 * env[..., None]) + np.array([0, 220, 255.]) * 0.6 * env[..., None]
    im = Image.fromarray(np.clip(tint, 0, 255).astype(np.uint8))
    x, y, w, h = rect
    ImageDraw.Draw(im).rectangle((x, y, x + w - 1, y + h - 1), outline=(255, 80, 80))
    save(rdir / 'steam-window.png', crop(np.asarray(im).astype(float), (0, 120, 420, 640), 1.5))

    # the layers alone over a dark wall colour, each tile 2 x 2 to show its wrap seams
    tiles = []
    for name, L in layers.items():
        th, tw = L['a'].shape
        lin2 = np.tile(L['lin'], (2, 2, 1))
        a2 = np.tile(L['a'], (2, 2))
        wall = to_linear(np.array([40.0, 28.0, 22.0]))
        tiles.append(labelled(srgb8(wall * (1 - a2[..., None]) + lin2 * a2[..., None]), f'{name} tile 2 x 2 ({tw} x {th})'))
    save(rdir / 'steam-tiles.png', row(tiles))

    # lit with the fire out (base plates and unlit tiles), at rest and brightened as the jolt's flare does (1.4)
    unlit_layers = {n: L['unlit_lin'] for n, L in layers.items()}
    bright_layers = {n: L['unlit_lin'] + (L['lin'] - L['unlit_lin']) * 1.4 for n, L in layers.items()}
    bright_plate = unlit_lin + np.maximum(plate_lin - unlit_lin, 0) * 1.4
    lights = [labelled(crop(with_her(compose(unlit_lin, layers, env, rect, 3.0, unlit_layers)), box, 1.5), 'fire out (light 0 = 0)'),
              labelled(crop(with_her(compose(plate_lin, layers, env, rect, 3.0)), box, 1.5), 'rest (light 0 = 1)'),
              labelled(crop(with_her(compose(bright_plate, layers, env, rect, 3.0, bright_layers)), box, 1.5), 'flare (light 0 = 1.4)')]
    save(rdir / 'steam-light-levels.png', row(lights))

    # the whole frame at the authoring width and the narrow width, at rest
    full = with_her(compose(plate_lin, layers, env, rect, 4.0))
    full_im = Image.fromarray(np.clip(full, 0, 255).astype(np.uint8)).resize((540, 960), Image.LANCZOS)
    narrow = full_im.crop((0, 0, 443, 960))
    save(rdir / 'steam-frame-540.png', np.asarray(full_im).astype(float))
    save(rdir / 'steam-frame-443.png', np.asarray(narrow).astype(float))

    # the most the steam ever changes a pixel, over a period of the wisps, sampled every 0.5 s
    most = np.zeros((H, W))
    for t in np.arange(0.0, 30.0, 0.5):
        diff = np.abs(srgb8(compose(plate_lin, layers, env, rect, float(t))) - srgb8(plate_lin)).max(-1)
        most = np.maximum(most, diff)
    visible = most * (1 - hag_a[..., 0])
    report['steam_change_levels'] = {'max_on_visible_plate': float(visible.max()), 'max_under_her': float((most * hag_a[..., 0]).max()),
                                     'p99_inside_window': float(np.percentile(most[env > 0.05], 99))}
    heat = np.clip(visible * 4, 0, 255)
    save(rdir / 'steam-change-x4.png', crop(np.stack([heat, heat * 0.6, heat * 0.2], -1), (0, 120, 420, 640), 1.5))


# ========================================================================================================= install
def write_texture(path, arr, template, used, created, repeat):
    """Writes the texture; a new one gets a copy of the template's .meta under a fresh GUID (wrapping both ways when it
    repeats); an existing .meta is kept."""
    save(path, arr)
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return
    guid = uuid.uuid4().hex
    while guid in used:
        guid = uuid.uuid4().hex
    used.add(guid)
    text = template.read_text(encoding='ascii')
    newline = '\r\n' if '\r\n' in text else '\n'
    lines = []
    for ln in text.splitlines():
        if ln.startswith('guid:'):
            ln = 'guid: ' + guid
        elif repeat and ln.strip() in ('wrapU: 1', 'wrapV: 1'):
            # the steam tiles wrap both ways as their uvRect rises and drifts
            ln = ln.replace(': 1', ': 0')
        lines.append(ln)
    meta.write_text(newline.join(lines) + newline, encoding='ascii', newline='')
    created.append(rel(meta))


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--output', help='Unused directory under Docs/work (default: a fresh sunken-larder-ambient-* directory).')
    parser.add_argument('--write-assets', action='store_true', help='Also install the steam tiles, the window mask and the clips.')
    args = parser.parse_args()
    if args.output:
        OUT = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in OUT.parents:
            raise SystemExit('--output must be under Docs/work.')
        OUT.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        OUT = Path(tempfile.mkdtemp(prefix='sunken-larder-ambient-', dir=WORK))
    BTA.A.OUTPUT = None if args.write_assets else OUT

    plate, unlit, hag = load(BACKGROUND), load(UNLIT), load(HAG, 'RGBA')
    env, shape = window(plate)
    rect = window_rect(shape)
    target_lin, target_srgb, core_px = steam_colour(plate, shape)
    ratio, ratio_px = unlit_ratio(plate, unlit, shape)
    report = {'window': {'rect_xywh': list(rect), 'map_rect': map_rect(rect), 'pixels_over_half': int((env > 0.5).sum()),
                         'on_her_over_half_px': int(((env > 0.5) & (hag[..., 3] > 5)).sum()),
                         'centre': CENTRE, 'height_levels': HEIGHT_LEVELS},
              'steam_colour_srgb': target_srgb.round(2).tolist(), 'steam_colour_core_px': core_px,
              'unlit_ratio_linear': ratio.round(4).tolist(), 'unlit_ratio_px': ratio_px, 'layers': {}}

    base = wrap_both(flattened_donor())
    layers, files = {}, []
    for name, spec in LAYERS.items():
        tile = layer_tile(base, spec)
        lin, a, gain = coloured(tile, target_lin)
        unlit_lin = lin * ratio
        th, tw = a.shape
        period, rises, rise, drift = motion(spec, tile)
        layers[name] = {'tile': tile, 'lin': lin, 'unlit_lin': unlit_lin, 'a': a}
        x, y, w, h = rect
        uv = [0.0, 0.0, w / tw, h / th]
        report['layers'][name] = dict(spec, tile_size_px=[tw, th], colour_gain_linear=gain.round(4).tolist(),
                                      period_s=period, tiles_risen_per_period=rises, rise_px_s=rise, drift_px_s=drift,
                                      uv_rect_start=uv, mean_alpha=float(a.mean()), alpha_p99=float(np.percentile(a, 99)),
                                      wrap_alpha_jump_x=float(np.abs(a[:, -1] - a[:, 0]).max()),
                                      wrap_alpha_jump_y=float(np.abs(a[-1] - a[0]).max()))
        alpha8 = (a * 255)[..., None]
        files.append((ART / f'SunkenLarderSteam{name}.png', np.concatenate([srgb8(lin), alpha8], -1), HAG, True))
        files.append((RELIGHT / f'SunkenLarderSteam{name}Unlit.png', np.concatenate([srgb8(unlit_lin), alpha8], -1), HAG, True))
    white = np.full((H, W, 3), 255.0)
    files.append((ART / 'SunkenLarderSteamMask.png', np.concatenate([white, (env * 255)[..., None]], -1),
                  ART / 'SunkenLarderHagBlinkMask.png', False))
    for path, arr, _, _ in files:
        save(OUT / 'textures' / path.name, arr)

    clips = report.setdefault('clips', {})
    for name, spec in LAYERS.items():
        curves, period = layer_clip(spec, layers[name]['tile'], rect)
        path = CONTENT / f'SunkenLarderSteam{name}Rise.anim'
        clips[path.name] = {'path': BTA.write_clip(path, curves, period), 'length_s': period, 'curves': [c for c, _ in curves]}

    # RectTransform values in the Sunken Larder Scene frame (anchored to its top-left corner, pivot top-left)
    x, y, w, h = rect
    report['placement'] = {'anchored_position': [round(x * SX, 4), round(-y * SY, 4)], 'size': [round(w * SX, 4), round(h * SY, 4)],
                           'occluder_uv_rect': [x / W, 1 - (y + h) / H, w / W, h / H], 'map_rect': map_rect(rect)}
    reviews(OUT / 'review', plate, unlit, hag, layers, env, rect, report)
    if args.write_assets:
        used, created = BTA.used_guids(), []
        for path, arr, template, repeat in files:
            write_texture(path, arr, Path(str(template) + '.meta'), used, created, repeat)
        report['installed'] = {'textures': [rel(p) for p, *_ in files], 'created_meta': created}
    (OUT / 'report.json').write_text(json.dumps(report, indent=1, default=float) + '\n', encoding='ascii')
    print(json.dumps({k: report[k] for k in ('window', 'steam_colour_srgb', 'unlit_ratio_linear', 'layers', 'placement',
                                             'steam_change_levels')}, indent=1, default=float))
    print('Wrote ' + str(OUT))


if __name__ == '__main__':
    main()
