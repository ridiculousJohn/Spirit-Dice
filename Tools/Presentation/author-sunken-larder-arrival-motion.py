"""Author the Sunken Larder arrival picture's motion: its painted haze and low fog drifting, as picture scene drift layers.

Python 3.10 or later with numpy, Pillow and scipy (on this machine Docs/work/merchant-table-clear/.venv/Scripts/python.exe).
Run from the repository root:

    python Tools/Presentation/author-sunken-larder-arrival-motion.py                 # fresh Docs/work/sunken-larder-arrival-motion-* folder
    python Tools/Presentation/author-sunken-larder-arrival-motion.py --write-assets  # also installs the tiles and windows

It reads the accepted arrival picture (Art/SunkenLarder/SunkenLarderArrival.png, never changed, the picture scene's plate)
and kept generated donors in Docs/Reference/Art, so reruns give the same files; see "Arrival motion" in
Docs/Reference/Art/SunkenLarderLayers.md.
The method is the table's cauldron steam's (author-sunken-larder-ambient.py, whose wrap, colour and install helpers it
imports): a transparent generated donor made into tiles that wrap both ways, a broad, softened layer and a finer wisps
layer drifting over the plate at calm speeds, the broad one a little slower, seen only through a window that covers the
whole painted haze with a floor (0.6 plus 0.4 times the painted presence), so every part of it moves and it reads as the
painting moving. Here the window is the layers' own mask (a picture scene drift layer draws its texture through its
window), so no occluder redraws the plate.

Layers (LAYERS, in draw order):
- FogFar and FogFarWisps: the haze behind the trees and the hut, drifting slowly right. Their tiles are the Bound Tree's
  far mist donor's dense band, its brightness turned into density (its low-frequency level divided out, so no denser band
  pulses past), the broad one mirrored and softened, coloured to the painted haze.
- FogNear: the low fog over the water and around the stilts, from the near mist donor's band as one uniform-coloured
  density tile, drifting right faster than the haze, with a slow opacity swell.

Windows: the haze's envelope covers the whole painted haze (its presence blurred broadly, inside its rows and off the
hut); inside it the window is the floor plus the rest times the painted presence (luminance above its grey opening), as
the cauldron steam's. The low fog's window is its envelope (rows, kept off the hut's walls and faint over the walkway)
times the painted fog's bluish brightness, with a floor of 0.3. The report gives each layer's drift layer values in
picture units (rectangle with origin bottom left, tile size, velocity, opacity, swell and its period) for the picture
scene asset, the installed texture paths, and how much of the haze's window stays still from one second to the next.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import math
import tempfile

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, grey_opening, map_coordinates

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / 'Tools/Presentation'
ASSETS = ROOT / 'Dice-Unity/Assets/Dice'
ART = ASSETS / 'Art/SunkenLarder'
REF = ROOT / 'Docs/Reference/Art'
WORK = ROOT / 'Docs/work'
W, H = 941, 1672
PICTURE = ART / 'SunkenLarderArrival.png'


def _load_module(name, file):
    spec = importlib.util.spec_from_file_location(name, TOOLS / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


SLA = _load_module('author_sunken_larder_ambient', 'author-sunken-larder-ambient.py')
BTA = SLA.BTA
to_linear, to_srgb = BTA.to_linear, BTA.to_srgb
load, save, rel, labelled, row, column = BTA.load, BTA.save, BTA.rel, BTA.labelled, BTA.row, BTA.column

# ------------------------------------------------------------------------------------------------------------ tiles
# kind: 'mist' (a mist donor's dense band, rows in donor px, its brightness over black turned into density: divided by its
# Gaussian average at density_sigma donor px and mapped from the stretch levels to alpha 0 to 1, the donor's own colour
# kept) or 'density' (the same density as alpha alone, in one uniform colour). flip: mirrored left to right, so two layers
# never repeat one pattern; soften: sigma (donor px) of a blur, for a broader, softer layer; scale: concept px per donor
# px; overlap: donor columns and rows cross-faded at the wrap. Drift: speed in concept px per second along angle (degrees
# above rightward); opacity at rest; swell: the opacity's swell either way as a fraction of it, swell_period seconds a
# cycle. window names the window it shows through.
MIST_FAR = {'donor': 'BoundTreeMistFarDonor.png', 'rows': (450, 680), 'density_sigma': 140.0, 'stretch': (0.85, 1.8),
            'overlap': (256, 48)}
LAYERS = {
    'FogFar': dict(MIST_FAR, kind='mist', flip=True, soften=6.0, scale=0.75, speed=7.0, angle=0.0, opacity=0.7,
                   swell=0.08, swell_period=83.0, window='far'),
    'FogFarWisps': dict(MIST_FAR, kind='mist', flip=False, soften=0.0, scale=0.45, speed=11.0, angle=0.0, opacity=0.9,
                        swell=0.0, swell_period=83.0, window='far'),
    'FogNear': {'donor': 'BoundTreeMistNearDonor.png', 'kind': 'density', 'rows': (450, 690),
                'flip': False, 'soften': 0.0, 'scale': 0.85, 'density_sigma': 160.0, 'stretch': (0.85, 1.3),
                'overlap': (256, 60), 'speed': 33.0, 'angle': 0.0, 'opacity': 0.75, 'swell': 0.15, 'swell_period': 27.0,
                'window': 'near'},
}
# The window texture of each window, named after its first layer.
WINDOW_NAMES = {'far': 'FogFar', 'near': 'FogNear'}

# ---------------------------------------------------------------------------------------------------------- windows
# The hut, its chimney and porch stand in front of the far haze: the haze's envelope is zero inside this polygon
# (concept px, edge softened HUT_SOFTEN px). The low fog passes in front of the stilts but not the walls above
# HUT_WALL_ROW.
HUT = [(12, 535), (98, 336), (206, 330), (210, 252), (252, 250), (268, 318), (292, 318), (420, 368), (560, 444),
       (660, 508), (754, 552), (748, 592), (716, 602), (704, 700), (692, 790), (600, 800), (280, 802), (60, 770), (12, 712)]
HUT_SOFTEN = 4.0
HUT_WALL_ROW = 745
# The walkway's near planks and posts, where the low fog passes at WALKWAY_LEVEL of its window.
WALKWAY = [(452, 770), (600, 760), (760, 800), (840, 880), (860, 1010), (640, 1080), (420, 1090), (300, 1000), (440, 900)]
WALKWAY_LEVEL = 0.45
# Rows over which an envelope rises from zero to full, stays full, and falls back to zero.
ROWS = {'far': (300, 360, 700, 790), 'near': (690, 745, 930, 1020)}
# Painted fog: luminance (blurred PRESENCE_BLUR px) between the envelope's PRESENCE_LOW and PRESENCE_HIGH percentiles
# mapped to 0 to 1 (smoothstep), times bluish, B - R between BLUISH levels (smoothstep). The low fog's window is its
# envelope times FLOOR plus the rest times it; the haze's envelope is it blurred HAZE_REACH px, mapped from HAZE_LEVELS to
# 0 to 1 (smoothstep), so it covers all the painted haze with soft edges and leaves the dark tree masses.
PRESENCE_BLUR = 3.0
PRESENCE_LOW, PRESENCE_HIGH = {'far': 25.0, 'near': 20.0}, {'far': 90.0, 'near': 90.0}
BLUISH = (4.0, 16.0)
FLOOR = {'near': 0.3}
HAZE_REACH = 14.0
HAZE_LEVELS = (0.12, 0.4)
# The cauldron steam's window inside the haze's envelope: STEAM_FLOOR plus the rest times the painted
# presence, the luminance above its grey opening (PRESENCE_OPENING px, blurred PRESENCE_OPENING_BLUR px), softened
# PRESENCE_SOFTEN px and normalised by its PRESENCE_PERCENTILE inside the envelope.
STEAM_FLOOR = 0.6
PRESENCE_OPENING, PRESENCE_OPENING_BLUR, PRESENCE_SOFTEN, PRESENCE_PERCENTILE = 61, 12.0, 6.0, 95.0
# Colour, as the cauldron steam's: each haze tile's alpha-weighted mean colour is, in linear light, the mean of the
# brightest BRIGHT_FRACTION (the haze, broad and even, its brighter third) of its window's picture pixels where the window
# is at least CORE_LEVEL, so it matches the painted haze. The low fog's uniform colour is the mean of its brightest
# NEAR_BRIGHT_FRACTION there, times NEAR_GAIN.
BRIGHT_FRACTION, CORE_LEVEL = 0.3, 0.8
NEAR_BRIGHT_FRACTION, NEAR_CORE_LEVEL, NEAR_GAIN = 0.4, 0.5, 1.2
RECT_MARGIN = 2        # concept px kept around a window's nonzero pixels in its rectangle
REVIEW_TIMES = [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0]
REVIEW_BOXES = {'far': (520, 300, 941, 600), 'near': (0, 680, 941, 1040)}
VISIBLE_ROWS = (0, 592)   # rows the journey narrative panel leaves visible above it at phone size
STILL_LEVEL = 1.0         # a window pixel counts as still when no one-second step changes it by this many levels

OUT = None
LUMA = np.array([0.2126, 0.7152, 0.0722])


def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


def polygon_mask(points, soften):
    im = Image.new('L', (W, H), 0)
    ImageDraw.Draw(im).polygon(points, fill=255)
    m = np.asarray(im).astype(np.float64) / 255
    return gaussian_filter(m, soften) if soften > 0 else m


def ramp_rows(rows):
    """Envelope by row: rises from 0 at rows[0] to 1 at rows[1], falls from 1 at rows[2] to 0 at rows[3]."""
    y = np.arange(H) + 0.5
    r = smoothstep((y - rows[0]) / (rows[1] - rows[0])) * (1 - smoothstep((y - rows[2]) / (rows[3] - rows[2])))
    return np.repeat(r[:, None], W, 1)


def fog_presence(plate, env, kind):
    """The painted fog's bluish brightness, 0 to 1, against the envelope's own levels."""
    lum = gaussian_filter(plate @ LUMA, PRESENCE_BLUR)
    inside = env > 0.5
    lo, hi = np.percentile(lum[inside], PRESENCE_LOW[kind]), np.percentile(lum[inside], PRESENCE_HIGH[kind])
    bluish = gaussian_filter(plate[..., 2] - plate[..., 0], PRESENCE_BLUR)
    p = smoothstep((lum - lo) / (hi - lo)) * smoothstep((bluish - BLUISH[0]) / (BLUISH[1] - BLUISH[0]))
    return p, (float(lo), float(hi))


def steam_window(plate, env):
    """The cauldron steam's window inside an envelope: the floor plus the rest times the painted presence."""
    lum = plate @ LUMA
    floor = gaussian_filter(grey_opening(lum, size=(PRESENCE_OPENING, PRESENCE_OPENING)), PRESENCE_OPENING_BLUR)
    excess = gaussian_filter(np.clip(lum - floor, 0, None), PRESENCE_SOFTEN)
    presence = np.clip(excess / np.percentile(excess[env > 0.5], PRESENCE_PERCENTILE), 0, 1)
    return env * (STEAM_FLOOR + (1 - STEAM_FLOOR) * presence)


def windows(plate):
    """Each window over the full picture, 0 to 1, with the painted haze its window covers and the presence levels."""
    hut = polygon_mask(HUT, HUT_SOFTEN)
    y = np.arange(H)[:, None] + 0.5
    walls = hut * smoothstep((HUT_WALL_ROW + 10 - y) / 20.0)
    walkway = polygon_mask(WALKWAY, 8.0)
    levels = {}
    far_rows = ramp_rows(ROWS['far']) * (1 - hut)
    haze, levels['far'] = fog_presence(plate, far_rows, 'far')
    far_env = far_rows * smoothstep((gaussian_filter(haze, HAZE_REACH) - HAZE_LEVELS[0]) / (HAZE_LEVELS[1] - HAZE_LEVELS[0]))
    near_env = ramp_rows(ROWS['near']) * (1 - walls) * (1 - (1 - WALKWAY_LEVEL) * walkway)
    near, levels['near'] = fog_presence(plate, near_env, 'near')
    wins = {'far': steam_window(plate, far_env), 'near': near_env * (FLOOR['near'] + (1 - FLOOR['near']) * near)}
    return wins, far_rows * haze, levels


def window_rect(win, margin=RECT_MARGIN):
    ys, xs = np.nonzero(win > 0.002)
    x0, y0 = max(0, xs.min() - margin), max(0, ys.min() - margin)
    x1, y1 = min(W, xs.max() + 1 + margin), min(H, ys.max() + 1 + margin)
    return (int(x0), int(y0), int(x1 - x0), int(y1 - y0))


# ------------------------------------------------------------------------------------------------------------- tiles
def mist_density(spec):
    """A mist donor's dense band as density (0 to 1) and its straight colour, before wrapping."""
    D = load(REF / spec['donor'], 'RGBA')
    r0, r1 = spec['rows']
    a = D[r0:r1, :, 3] / 255.0
    rgb = D[r0:r1, :, :3]
    d = (rgb @ LUMA) * a
    level = gaussian_filter(d, spec['density_sigma'], mode='reflect')
    flat = d / np.maximum(level, 0.25 * d.mean())
    lo, hi = spec['stretch']
    return smoothstep((flat - lo) / (hi - lo)), rgb


def base_pm(spec):
    """The layer's donor as a premultiplied texture (colour 0 to 255 times alpha, alpha) wrapping both ways."""
    density, rgb = mist_density(spec)
    if spec['kind'] == 'density':
        rgb = np.zeros_like(rgb)
    return SLA.wrap_both(np.concatenate([rgb * density[..., None], density[..., None]], -1), spec['overlap'])


def layer_tile(spec):
    """A layer's premultiplied tile at concept resolution, wrapping both ways."""
    pm = base_pm(spec)
    if spec['flip']:
        pm = np.ascontiguousarray(pm[:, ::-1])
    if spec['soften'] > 0:
        pm = gaussian_filter(pm, (spec['soften'], spec['soften'], 0), mode='wrap')
    pm[..., 3] = np.clip(pm[..., 3], 0, 1)
    return BTA.resize_pm(np.ascontiguousarray(pm), spec['scale'])


def bright_colour(plate, win, fraction, core_level, gain=1.0):
    """The mean colour, in linear light, of the brightest fraction of the picture where the window is at least core_level."""
    core = win >= core_level
    px = plate[core]
    lum = px @ LUMA
    bright = px[lum >= np.quantile(lum, 1 - fraction)]
    return np.clip(to_linear(bright).mean(0) * gain, 0, 1), bright.mean(0), int(core.sum())


def coloured_tile(spec, tile_pm, plate, win):
    """The tile's straight linear colour and alpha, coloured as its kind is, with the painted sRGB colour and core pixel
    count."""
    a = tile_pm[..., 3]
    if spec['kind'] == 'density':
        colour, painted, core_px = bright_colour(plate, win, NEAR_BRIGHT_FRACTION, NEAR_CORE_LEVEL, NEAR_GAIN)
        return np.broadcast_to(colour, a.shape + (3,)).copy(), a, painted, core_px
    colour, painted, core_px = bright_colour(plate, win, BRIGHT_FRACTION, CORE_LEVEL)
    lin, a, _ = SLA.coloured(tile_pm, colour)
    return lin, a, painted, core_px


# ---------------------------------------------------------------------------------------------------------- compose
def velocity(spec):
    angle = math.radians(spec['angle'])
    return spec['speed'] * math.cos(angle), spec['speed'] * math.sin(angle)


def layer_values(spec, tile, rect):
    """The drift layer's values in picture units: rect (origin bottom left), tile size, velocity (up positive)."""
    x, y, w, h = rect
    th, tw = tile.shape
    vx, vy = velocity(spec)
    return {'rect': [x / W, 1 - (y + h) / H, w / W, h / H], 'tileSize': [tw / W, th / H], 'velocity': [vx / W, vy / H],
            'opacity': spec['opacity'], 'swell': spec['swell'], 'swellPeriod': spec['swell_period']}


def sample(layer, t):
    """The layer's straight linear colour and alpha over the frame at t seconds, as the drift layer draws it: the tile at
    (point - velocity * t) / tile size, wrapping, through the window, at the swelling opacity (PictureDriftLayerImage)."""
    spec, a_tile, win, rect = layer['spec'], layer['alpha'], layer['window'], layer['rect']
    x, y, w, h = rect
    th, tw = a_tile.shape
    vx, vy = velocity(spec)
    cols = np.arange(x, x + w) + 0.5
    rows = np.arange(y, y + h) + 0.5
    u = (cols - vx * t) / tw                    # tiles, left to right
    v = (H - rows - vy * t) / th                # tiles, bottom to top
    tx = (u % 1.0) * tw - 0.5
    ty = (1 - (v % 1.0)) * th - 0.5
    gy, gx = np.meshgrid(ty, tx, indexing='ij')
    coords = [gy, gx]
    a = map_coordinates(a_tile, coords, order=1, mode='grid-wrap')
    pm = np.stack([map_coordinates(layer['lin'][..., c] * a_tile, coords, order=1, mode='grid-wrap') for c in range(3)], -1)
    opacity = min(1.0, spec['opacity'] * (1 + spec['swell'] * math.sin(2 * math.pi * t / spec['swell_period'])))
    rgb, alpha = np.zeros((H, W, 3)), np.zeros((H, W))
    rgb[y:y + h, x:x + w] = pm / np.maximum(a, 1e-6)[..., None]
    alpha[y:y + h, x:x + w] = a * win[y:y + h, x:x + w] * opacity
    return rgb, alpha


def compose(plate_lin, layers, t):
    out = plate_lin.copy()
    for L in layers.values():
        rgb, a = sample(L, t)
        out = out * (1 - a[..., None]) + rgb * a[..., None]
    return out


def srgb8(lin):
    return to_srgb(np.clip(lin, 0, 1))


def crop(img, box, scale=1.0):
    x0, y0, x1, y1 = box
    im = Image.fromarray(np.clip(np.rint(img[y0:y1, x0:x1]), 0, 255).astype(np.uint8))
    if scale != 1.0:
        im = im.resize((int(round(im.width * scale)), int(round(im.height * scale))), Image.LANCZOS)
    return np.asarray(im).astype(np.float64)


def reviews(rdir, plate, layers, wins, report):
    """Review images of the layers over the picture, the picture scene's plate, beside the picture."""
    rdir.mkdir(parents=True, exist_ok=True)
    plate_lin = to_linear(plate)
    frames_at = {t: srgb8(compose(plate_lin, layers, t)) for t in REVIEW_TIMES}
    for kind, box in REVIEW_BOXES.items():
        frames = [labelled(crop(plate, box, 1.5), 'picture (no drift layers)')]
        for t in REVIEW_TIMES:
            frames.append(labelled(crop(frames_at[t], box, 1.5), f't = {t:.1f} s'))
        save(rdir / f'{kind}-frames.png', column([row(frames[i:i + 3]) for i in range(0, len(frames), 3)]))
    # how much of the haze's window changes from one second to the next: the least change of each pixel over the steps,
    # and the share of the window (at least half) that some step leaves still
    steps = [np.abs(frames_at[b] - frames_at[a]).max(-1) for a, b in zip(REVIEW_TIMES, REVIEW_TIMES[1:])]
    most = np.max(steps, 0)
    report['one_second_steps'] = {}
    inside = wins['far'] >= 0.5
    report['one_second_steps']['far'] = {
        'window_px': int(inside.sum()),
        'still_share': float((most[inside] < STILL_LEVEL).mean()),
        'median_change': float(np.median(np.mean(steps, 0)[inside])),
        'p95_change': float(np.percentile(np.mean(steps, 0)[inside], 95))}
    still = np.zeros((H, W, 3))
    still[..., 0] = np.where(inside, np.where(most < STILL_LEVEL, 255, 0), 0)
    still[..., 1] = np.clip(np.mean(steps, 0) * 8, 0, 255) * inside
    save(rdir / 'one-second-change.png', crop(np.maximum(still, plate * 0.35), (0, 280, 941, 820)))
    # the windows tinted over the picture, each rectangle outlined
    tint = plate.copy()
    colours = {'far': np.array([0, 220, 255.]), 'near': np.array([255, 200, 0.])}
    for kind, win in wins.items():
        tint = tint * (1 - 0.6 * win[..., None]) + colours[kind] * 0.6 * win[..., None]
    im = Image.fromarray(np.clip(tint, 0, 255).astype(np.uint8))
    for name, L in layers.items():
        x, y, w, h = L['rect']
        ImageDraw.Draw(im).rectangle((x, y, x + w - 1, y + h - 1), outline=tuple(int(c) for c in colours[L['spec']['window']]))
    save(rdir / 'windows.png', np.asarray(im).astype(float))
    # each tile 2 x 2 in its colour over a dark ground, for its wrap seams
    tiles = []
    for name, L in layers.items():
        a2 = np.tile(L['alpha'], (2, 2))[..., None]
        lin2 = np.tile(L['lin'], (2, 2, 1))
        ground = to_linear(np.array([12.0, 16.0, 22.0]))
        th, tw = L['alpha'].shape
        tiles.append(labelled(srgb8(ground * (1 - a2) + lin2 * a2), f'{name} tile 2 x 2 ({tw} x {th})'))
    save(rdir / 'tiles.png', column(tiles))
    # the whole frame at phone size, two seconds apart, and the most each pixel changes over half a minute
    seq = []
    for t in (0.0, 2.0, 4.0, 6.0):
        full = Image.fromarray(np.clip(np.rint(frames_at[t]), 0, 255).astype(np.uint8))
        seq.append(labelled(np.asarray(full.resize((540, 960), Image.LANCZOS)).astype(float), f'{t:.0f} s, 540 x 960'))
    save(rdir / 'frames-540.png', row(seq))
    most = np.zeros((H, W))
    for t in np.arange(0.0, 30.0, 0.5):
        most = np.maximum(most, np.abs(srgb8(compose(plate_lin, layers, float(t))) - plate).max(-1))
    report['change_levels'] = {kind: {'max': float(most[wins[kind] > 0.05].max()),
                                      'p99': float(np.percentile(most[wins[kind] > 0.05], 99))} for kind in wins}
    v0, v1 = VISIBLE_ROWS
    report['change_levels']['visible_above_narrative'] = float(most[v0:v1].max())
    heat = np.clip(most * 4, 0, 255)
    save(rdir / 'change-x4.png', np.stack([heat, heat * 0.6, heat * 0.2], -1))


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--output', help='Unused directory under Docs/work (default: a fresh sunken-larder-arrival-motion-* directory).')
    parser.add_argument('--write-assets', action='store_true', help='Also install the tiles and the windows.')
    args = parser.parse_args()
    if args.output:
        OUT = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in OUT.parents:
            raise SystemExit('--output must be under Docs/work.')
        OUT.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        OUT = Path(tempfile.mkdtemp(prefix='sunken-larder-arrival-motion-', dir=WORK))

    plate = load(PICTURE)
    wins, painted_haze, levels = windows(plate)
    report = {'picture': rel(PICTURE), 'presence_levels': levels, 'layers': {}}
    # how much of the painted haze its window covers at a level of at least half
    haze = painted_haze > 0.5
    report['painted_haze_px'] = int(haze.sum())
    report['painted_haze_covered_share'] = float((wins['far'][haze] >= 0.5).mean())
    layers, tiles = {}, {name: layer_tile(spec) for name, spec in LAYERS.items()}
    files = []
    for kind, name in WINDOW_NAMES.items():
        win = wins[kind]
        x, y, w, h = window_rect(win)
        window_rgba = np.concatenate([np.full((h, w, 3), 255.0), (win[y:y + h, x:x + w] * 255)[..., None]], -1)
        files.append((ART / f'SunkenLarderArrival{name}Window.png', window_rgba, ART / 'SunkenLarderSteamMask.png', False))
    for name, spec in LAYERS.items():
        win = wins[spec['window']]
        rect = window_rect(win)
        lin, a, painted_srgb, core_px = coloured_tile(spec, tiles[name], plate, win)
        layers[name] = {'spec': spec, 'alpha': a, 'lin': lin, 'window': win, 'rect': rect}
        tile_rgba = np.concatenate([srgb8(lin), (a * 255)[..., None]], -1)
        tile_path = ART / f'SunkenLarderArrival{name}.png'
        window_path = ART / f'SunkenLarderArrival{WINDOW_NAMES[spec["window"]]}Window.png'
        files.append((tile_path, tile_rgba, ART / 'SunkenLarderSteamWisps.png', True))
        th, tw = a.shape
        report['layers'][name] = dict(layer_values(spec, a, rect), texture=rel(tile_path), window=rel(window_path),
                                      rect_xywh_px=list(rect), tile_px=[tw, th], velocity_px_s=list(velocity(spec)),
                                      speed_px_s=spec['speed'], painted_colour_srgb=painted_srgb.round(1).tolist(),
                                      colour_core_px=core_px, tile_mean_alpha=float(a.mean()),
                                      window_pixels_over_half=int((win > 0.5).sum()),
                                      wrap_alpha_jump_x=float(np.abs(a[:, -1] - a[:, 0]).max()),
                                      wrap_alpha_jump_y=float(np.abs(a[-1] - a[0]).max()))
    for path, arr, _, _ in files:
        save(OUT / 'textures' / path.name, arr)
    reviews(OUT / 'review', plate, layers, wins, report)
    if args.write_assets:
        used, created = BTA.used_guids(), []
        for path, arr, template, repeat in files:
            SLA.write_texture(path, arr, Path(str(template) + '.meta'), used, created, repeat)
        report['installed'] = {'textures': [rel(p) for p, *_ in files], 'created_meta': created}
    (OUT / 'report.json').write_text(json.dumps(report, indent=1, default=float) + '\n', encoding='ascii')
    print(json.dumps(report, indent=1, default=float))
    print('Wrote ' + str(OUT))


if __name__ == '__main__':
    main()
