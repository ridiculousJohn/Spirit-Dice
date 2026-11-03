"""Author the Sunken Larder's old woman as the encounter host: her lit figure, two painted eyelid overlays and one mesh.

The host is a living still like the merchant (author-merchant-presentation.py): every layer keeps the 941 x 1672
concept frame, so one deformation mesh shared by all of them moves her figure and the overlays identically and no
seam can open. Her figure is one layer (SunkenLarderHag.png): no part of her moves in front of another, so nothing
needs splitting, and her forearms, hands and the shawl's hem at the table never move. Two eyelid overlays, made as
the merchant's blink was, draw over her face: Blink (closed lids) and Downcast (lids lowered, eyes looking down).
Every layer is lit per pixel by the hearth fire (light 0 of SunkenLarderSceneLighting.asset) from its unlit base and
its painted reference, as the background is. Stages, run from the repository root:

  target    Write the overlays' imagegen edit target: the visible composite (SunkenLarderHag.png over
            SunkenLarderBackground.png) in FACE_BOX, upscaled 4x, under Docs/work/sunken-larder-host/targets.
  overlays  Register the preserved donors (Docs/Reference/Art/SunkenLarderHag{Blink,Downcast}Donor.png) to the
            concept frame by an affine fit over the face around the eyes (eyes excluded) and transfer them only inside
            soft elliptical eyelid regions, with the donor's low-frequency colour matched to her face in a ring around
            each eye. Writes each overlay as a crop (colour, mask, unlit base), review images and report.json.
  host      Write the deformation mesh SunkenLarderHostMesh.asset, the three layer materials, the portrait prefab
            SunkenLarderHost.prefab and the host SunkenLarderHost.asset (Content/Presentation/SunkenLarder), and
            report.json with the measured motion (largest displacement, the table edge's stillness, strain, folds).

  python Tools/Presentation/author-sunken-larder-host.py target
  python Tools/Presentation/author-sunken-larder-host.py overlays                 # fresh Docs/work/sunken-larder-host-*
  python Tools/Presentation/author-sunken-larder-host.py overlays --write-assets  # also writes the overlay textures
  python Tools/Presentation/author-sunken-larder-host.py host                     # fresh Docs/work/sunken-larder-host-*
  python Tools/Presentation/author-sunken-larder-host.py host --write-assets      # reauthors the host in place

Python 3.10 or later with numpy, Pillow and scipy (on this machine the venv
Docs/work/merchant-table-clear/.venv/Scripts/python.exe). --write-assets keeps existing .meta files and writes a .meta
for a file that has none. Source artwork and donors are only read, so repeated runs give the same files.
"""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import math
import tempfile
import uuid

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, map_coordinates
from scipy.optimize import minimize

SPEC = importlib.util.spec_from_file_location('spirit_authoring', Path(__file__).with_name('author-spirit-presentations.py'))
A = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(A)
ART = A.ASSETS / 'Art/SunkenLarder'
RELIGHT = ART / 'Relight'
FOLDER = A.PRESENTATION / 'SunkenLarder'
REFERENCE = A.ROOT / 'Docs/Reference/Art'
TARGETS = A.ROOT / 'Docs/work/sunken-larder-host/targets'
SOURCE = (941, 1672)

# Scripts, shaders and meta templates the written files reference.
PORTRAIT_SCRIPT = '08503f4c437f422b807ec20ae4c077c6'
IMAGE_SCRIPT = '885d80268a9b488590f58442b3c2fc79'
MASKED_SHADER = '22d03ee7f6e94daca44bf6ff9637e112'
COLOUR_META = ART / 'SunkenLarderHag.png.meta'
MASK_META = A.ASSETS / 'Art/HauntedMerchant/MerchantBlinkMask.png.meta'

# ---------------------------------------------------------------------------------------------------------------------
# Overlays. Concept pixels, top-left origin.

# The edit target: the composite in FACE_BOX, Lanczos-upscaled by TARGET_SCALE (the merchant blink's 4x of 260 px).
FACE_BOX = (370, 380, 630, 640)
TARGET_SCALE = 4
# Registration: an affine fit of high-passed luminance (HIGH_PASS px) over her face inside WINDOW, excluding the
# eye ellipses (the region the donors change) and pixels where her alpha is below 0.99.
WINDOW = (395, 415, 595, 600)
EXCLUDE = ((436, 474, 30, 19), (515, 457, 30, 19))
HIGH_PASS = 6.0
# Each overlay's eyelid regions (centre x, centre y, radius x, radius y): the mask is 1 inside FULL of the normalized
# radius and 0 beyond EDGE (smoothstep between); the donor's colour gain is measured on her face in the ring between
# RING radii, a normalized Gaussian of GAIN_SIGMA px. The overlay textures are the crop CROP, which the mask leaves 0 at
# its border.
OVERLAYS = {
    'Blink': dict(donor=REFERENCE / 'SunkenLarderHagBlinkDonor.png', eyes=((437, 476, 22, 13), (515, 461, 23, 13))),
    'Downcast': dict(donor=REFERENCE / 'SunkenLarderHagDowncastDonor.png', eyes=((437, 476, 22, 14), (515, 461, 23, 14))),
}
FULL, EDGE = .72, 1.12
RING = (1.15, 1.9)
GAIN_SIGMA = 7.0
CROP = (404, 436, 548, 494)
# The unlit base of an overlay is its colour times her own unlit-over-painted ratio, a normalized Gaussian of
# RATIO_SIGMA px in linear light over her interior (the ratio author-sunken-larder-relight.py applied to her figure).
RATIO_SIGMA = 3.0


def to_linear(c):
    c = np.asarray(c, np.float64) / 255
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= .0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - .055) * 255


def load(path, mode='RGBA'):
    return np.asarray(Image.open(path).convert(mode)).astype(np.float64)


def save(array, path, mode):
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(np.round(array), 0, 255).astype(np.uint8), mode).save(path)


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def composite():
    """Her painted figure over the plate: the visible composite at light 0 = 1."""
    plate = load(ART / 'SunkenLarderBackground.png', 'RGB')
    hag = load(ART / 'SunkenLarderHag.png')
    a = hag[..., 3:] / 255
    return hag[..., :3] * a + plate * (1 - a), hag


def stage_target(args):
    flat, _ = composite()
    x0, y0, x1, y1 = FACE_BOX
    target = Image.fromarray(np.round(flat[y0:y1, x0:x1]).astype(np.uint8), 'RGB')
    target = target.resize(((x1 - x0) * TARGET_SCALE, (y1 - y0) * TARGET_SCALE), Image.LANCZOS)
    TARGETS.mkdir(parents=True, exist_ok=True)
    path = TARGETS / 'face-target.png'
    target.save(path)
    print('Wrote ' + str(path.relative_to(A.ROOT)))


def luminance(c):
    return c @ np.array([.299, .587, .114])


def register(donor, flat, alpha):
    """Fit donor px = M (p - c) + t for concept points p over the face around the eyes; returns the fit and NCCs."""
    scale = donor.shape[1] / (FACE_BOX[2] - FACE_BOX[0])
    donor_hp = luminance(gaussian_filter(donor, (scale * .45, scale * .45, 0)))
    donor_hp = donor_hp - gaussian_filter(donor_hp, HIGH_PASS * scale)
    source_hp = luminance(flat) - gaussian_filter(luminance(flat), HIGH_PASS)
    yy, xx = np.mgrid[WINDOW[1]:WINDOW[3], WINDOW[0]:WINDOW[2]].astype(np.float64)
    keep = alpha[WINDOW[1]:WINDOW[3], WINDOW[0]:WINDOW[2]] > .99
    for cx, cy, rx, ry in EXCLUDE:
        keep &= ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 > 1
    xs, ys = xx[keep] + .5, yy[keep] + .5
    sv = source_hp[yy[keep].astype(int), xx[keep].astype(int)]
    sv = (sv - sv.mean()) / sv.std()
    c = np.array([(FACE_BOX[0] + FACE_BOX[2]) / 2, (FACE_BOX[1] + FACE_BOX[3]) / 2])

    def mapping(p, x, y):
        dx, dy = x - c[0], y - c[1]
        return p[0] * dx + p[1] * dy + p[4], p[2] * dx + p[3] * dy + p[5]

    def cost(p):
        u, v = mapping(p, xs, ys)
        d = map_coordinates(donor_hp, [v - .5, u - .5], order=1, mode='nearest')
        d = (d - d.mean()) / (d.std() + 1e-9)
        return -np.mean(d * sv)

    p0 = np.array([scale, 0, 0, scale, (c[0] - FACE_BOX[0]) * scale, (c[1] - FACE_BOX[1]) * scale])
    best = None
    for ox in np.arange(-40, 41, 8):
        for oy in np.arange(-40, 41, 8):
            for s in (.97, 1, 1.03):
                p = p0.copy()
                p[[0, 3]] *= s
                p[4] += ox
                p[5] += oy
                value = cost(p)
                if best is None or value < best[0]:
                    best = (value, p)
    fit = minimize(cost, best[1], method='Powell', options={'xtol': 1e-4, 'ftol': 1e-7, 'maxfev': 20000})
    return fit.x, c, -cost(p0), -fit.fun, mapping


def resample(donor, p, mapping):
    """The registered donor over FACE_BOX in the concept frame (anti-aliased like the merchant's blink)."""
    scale = donor.shape[1] / (FACE_BOX[2] - FACE_BOX[0])
    blurred = gaussian_filter(donor, (scale * .45, scale * .45, 0))
    gy, gx = np.mgrid[FACE_BOX[1]:FACE_BOX[3], FACE_BOX[0]:FACE_BOX[2]].astype(np.float64)
    u, v = mapping(p, gx + .5, gy + .5)
    out = np.zeros((SOURCE[1], SOURCE[0], 3))
    out[FACE_BOX[1]:FACE_BOX[3], FACE_BOX[0]:FACE_BOX[2]] = np.stack(
        [map_coordinates(blurred[..., k], [v - .5, u - .5], order=1, mode='nearest') for k in range(3)], -1)
    return out


def eyelid_mask(eyes):
    yy, xx = np.mgrid[0:SOURCE[1], 0:SOURCE[0]].astype(np.float64) + .5
    mask = np.zeros(xx.shape)
    ring = np.zeros(xx.shape, bool)
    for cx, cy, rx, ry in eyes:
        r = np.hypot((xx - cx) / rx, (yy - cy) / ry)
        mask = np.maximum(mask, 1 - smooth((r - FULL) / (EDGE - FULL)))
        ring |= (r > RING[0]) & (r < RING[1])
    return mask, ring


def unlit_ratio(hag, hag_unlit):
    """Her unlit-over-painted ratio per channel in linear light, smoothed over her interior and at most 1."""
    w = (hag[..., 3] > 250).astype(np.float64)
    weight = gaussian_filter(w, RATIO_SIGMA)
    lit = np.stack([gaussian_filter(to_linear(hag[..., k]) * w, RATIO_SIGMA) for k in range(3)], -1)
    dark = np.stack([gaussian_filter(to_linear(hag_unlit[..., k]) * w, RATIO_SIGMA) for k in range(3)], -1)
    ratio = np.where(weight[..., None] > 1e-3, dark / np.maximum(lit, 1e-6), 1)
    return np.clip(ratio, 0, 1)


def stage_overlays(args):
    out = args.out
    flat, hag = composite()
    hag_unlit = load(RELIGHT / 'SunkenLarderHagUnlit.png')
    plate_unlit = load(RELIGHT / 'SunkenLarderAmbientUnlit.png', 'RGB')
    alpha = hag[..., 3] / 255
    ratio = unlit_ratio(hag, hag_unlit)
    x0, y0, x1, y1 = CROP
    report = {'face box': FACE_BOX, 'target scale': TARGET_SCALE, 'crop': CROP, 'window': WINDOW, 'excluded': EXCLUDE,
              'mask full and edge radius': [FULL, EDGE], 'ring': RING, 'gain sigma px': GAIN_SIGMA,
              'ratio sigma px': RATIO_SIGMA,
              'her alpha in the crop': [round(float(alpha[y0:y1, x0:x1].min()), 4), round(float(alpha[y0:y1, x0:x1].max()), 4)],
              'overlays': {}}
    review = out / 'review'
    sheet = []
    for name, spec in OVERLAYS.items():
        donor_path = Path(getattr(args, 'donor_' + name.lower()) or spec['donor']).resolve()
        donor = load(donor_path, 'RGB')
        p, c, ncc0, ncc, mapping = register(donor, flat, alpha)
        registered = resample(donor, p, mapping)
        mask, ring = eyelid_mask(spec['eyes'])
        ring &= hag[..., 3] > 250
        weights = gaussian_filter(ring.astype(np.float64), GAIN_SIGMA)
        gain = np.ones(registered.shape)
        for k in range(3):
            s = gaussian_filter(flat[..., k] * ring, GAIN_SIGMA) / np.maximum(weights, 1e-6)
            d = gaussian_filter(registered[..., k] * ring, GAIN_SIGMA) / np.maximum(weights, 1e-6)
            gain[..., k] = np.where(weights > 1e-3, s / np.maximum(d, 1), 1)
        corrected = np.clip(registered * gain, 0, 255)
        crop_mask = mask[y0:y1, x0:x1]
        if crop_mask[0].max() or crop_mask[-1].max() or crop_mask[:, 0].max() or crop_mask[:, -1].max():
            raise SystemExit(name + ': the eyelid mask must vanish at the crop border.')
        colour = corrected[y0:y1, x0:x1]
        unlit = to_srgb(to_linear(colour) * ratio[y0:y1, x0:x1])
        opaque = np.full(crop_mask.shape + (1,), 255.0)
        files = {
            ART / ('SunkenLarderHag%s.png' % name): np.concatenate([colour, opaque], -1),
            ART / ('SunkenLarderHag%sMask.png' % name): np.dstack([np.full(crop_mask.shape, 255.0)] * 3 + [crop_mask * 255]),
            RELIGHT / ('SunkenLarderHag%sUnlit.png' % name): np.concatenate([unlit, opaque], -1),
        }
        for path, array in files.items():
            save(array, out / 'textures' / path.name, 'RGBA')
        changed = crop_mask > 0
        report['overlays'][name] = {
            'donor': str(donor_path.relative_to(A.ROOT)).replace('\\', '/'), 'donor size': list(donor.shape[1::-1]),
            'model': 'donor px = M (p - c) + t, p concept px (top-left, pixel centres at +0.5)', 'c': c.tolist(),
            'M': [[p[0], p[1]], [p[2], p[3]]], 't': [p[4], p[5]], 'ncc initial': ncc0, 'ncc registered': ncc,
            'eyes': spec['eyes'], 'mask nonzero pixels': int(changed.sum()), 'mask full pixels': int((crop_mask >= 1).sum()),
            'gain mean rgb': gain[y0:y1, x0:x1][changed].mean(0).tolist(),
            'gain min rgb': gain[y0:y1, x0:x1][changed].min(0).tolist(),
            'gain max rgb': gain[y0:y1, x0:x1][changed].max(0).tolist(),
            'unlit ratio in mask min/mean/max': [float(ratio[y0:y1, x0:x1][changed].min()),
                                                 float(ratio[y0:y1, x0:x1][changed].mean()),
                                                 float(ratio[y0:y1, x0:x1][changed].max())],
            'largest change at full overlay (levels)': float(np.abs(colour - flat[y0:y1, x0:x1])[changed].max()),
        }
        sheet.append((name, colour, crop_mask, unlit, registered))
        if args.write_assets:
            for path, array in files.items():
                save(array, path, 'RGBA')
                write_meta(path, MASK_META if path.name.endswith('Mask.png') else COLOUR_META)
    overlay_review(review, flat, hag_unlit, plate_unlit, sheet)
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='ascii')
    print(json.dumps(report, indent=2))
    print('Wrote the overlays to ' + str(out))


def overlay_review(review, flat, hag_unlit, plate_unlit, sheet):
    """Her face at 3x: painted and unlit, each overlay at 0, 0.5 and 1, the registered donor and the mask edges."""
    x0, y0, x1, y1 = CROP
    box = (390, 410, 600, 560)
    a = hag_unlit[..., 3:] / 255
    unlit_flat = to_srgb(to_linear(hag_unlit[..., :3]) * a + to_linear(plate_unlit) * (1 - a))

    def show(image):
        return Image.fromarray(np.clip(np.round(image[box[1]:box[3], box[0]:box[2]]), 0, 255).astype(np.uint8), 'RGB') \
            .resize(((box[2] - box[0]) * 3, (box[3] - box[1]) * 3), Image.LANCZOS)

    rows = []
    for name, colour, mask, unlit, registered in sheet:
        cells = []
        for base, over in ((flat, colour), (unlit_flat, unlit)):
            for level in (0, .5, 1):
                image = base.copy()
                m = (mask * level)[..., None]
                # Linear-light blend, as the lit layers composite in the game's linear colour space.
                image[y0:y1, x0:x1] = to_srgb(to_linear(over) * m + to_linear(base[y0:y1, x0:x1]) * (1 - m))
                cells.append(show(image))
        edges = show(registered.copy() if registered.any() else flat)
        draw = ImageDraw.Draw(edges)
        for (cx, cy, rx, ry) in OVERLAYS[name]['eyes']:
            for k, colour_ in ((EDGE, (0, 255, 255)), (FULL, (255, 0, 255))):
                draw.ellipse([((cx - rx * k) - box[0]) * 3, ((cy - ry * k) - box[1]) * 3,
                              ((cx + rx * k) - box[0]) * 3, ((cy + ry * k) - box[1]) * 3], outline=colour_)
        cells.append(edges)
        rows.append(cells)
    w, h = rows[0][0].size
    image = Image.new('RGB', (w * 4, h * 2 * len(rows)))
    for r, cells in enumerate(rows):
        for i, cell in enumerate(cells[:6]):
            image.paste(cell, ((i % 3) * w, (2 * r + i // 3) * h))
        image.paste(cells[6], (3 * w, 2 * r * h))
    review.mkdir(parents=True, exist_ok=True)
    image.save(review / 'overlays-face-3x.png')


def write_meta(path, template):
    """Keeps an existing .meta; otherwise copies the template's importer settings under a fresh GUID."""
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return
    lines = Path(template).read_text(encoding='utf-8').splitlines()
    lines = ['guid: ' + uuid.uuid4().hex if line.startswith('guid:') else line for line in lines]
    meta.write_text('\n'.join(lines) + '\n', encoding='utf-8')


# ---------------------------------------------------------------------------------------------------------------------
# Motion. Concept pixels, top-left origin; displacements are (right, up) in px at full weight.

# The merchant's grid, about 19.6 x 19.9 concept px per cell: every layer's vertices are rebuilt each frame while she
# breathes. Against the nymph's 96 x 168 it renders her smile and head shapes alike (rendered face crops differ by at most
# 26 levels, 99th percentile 4) with no folds; the grid departs from the authored Smile field by up to 1.5 px at the
# mouth corners (report.json).
COLUMNS, ROWS = 48, 84

# Head: a soft ellipse (centre, radii; full inside HEAD_FALLOFF[0] of the radius, none beyond [1]) over her head and the
# hair beside her face, faded out below the chin between NECK_ROWS, so the shoulders never follow a head shape.
HEAD = (485, 450, 160, 230)
HEAD_FALLOFF = (.8, 1.25)
NECK_ROWS = (590, 670)
# Everything at or below the table's motion rows stays still: the shapes of her body fade from full above
# TABLE_ROWS[1] to zero at TABLE_ROWS[0], well above the sleeves, forearms and hands (from row 790) and the shawl's hem
# at the table (row 900).
TABLE_ROWS = (770, 670)

# Breath: the shoulders and chest lift LIFT px; the head follows at HEAD_SHARE of it; the chest widens SPREAD px at its
# sides (CHEST ellipse).
LIFT = 4.5
HEAD_SHARE = .7
SPREAD = 1.5
CHEST = (485, 660, 230, 150)

# HeadLower: the head pitches down, the face moving LOWER_FACE px from row LOWER_ROWS[1] down and the crown
# LOWER_CROWN px; the hair above the brow stretches and the neck below the chin takes up the rest.
LOWER_CROWN, LOWER_FACE = 3.0, 7.0
LOWER_ROWS = (300, 430)
# HeadTurn: the head turns toward the hearth at image left, the front of the face (FACE ellipse, full inside
# FACE_FALLOFF[0]) moving TURN_FACE px left and the head's outline TURN_OUTLINE px.
TURN_FACE, TURN_OUTLINE = 4.5, 1.5
FACE = (482, 495, 80, 105)
FACE_FALLOFF = (.5, 1.2)

# Smile: the mouth corners (centre, radius; full inside SMILE_FALLOFF[0] of the radius) move outward and up, the
# cheeks rise a little. A negative weight relaxes the concept's faint smile.
CORNERS = (((467, 546), -1), ((521, 535), 1))
CORNER_RADIUS, CORNER_MOVE = 22.0, (1.5, 2.4)
CHEEKS = (((446, 522), -1), ((538, 512), 1))
CHEEK_RADIUS, CHEEK_MOVE = 30.0, (.6, 1.2)
SMILE_FALLOFF = (.3, 1.0)

# Settle: she leans back a little from her hunch: everything above the table rows shrinks by SETTLE_SCALE about
# SETTLE_CENTRE (her face, so the head stays put and the shoulders draw in and up) and rises SETTLE_LIFT px.
SETTLE_SCALE = .99
SETTLE_CENTRE = (485, 470)
SETTLE_LIFT = 2.0

SHAPES = ('Breath', 'HeadLower', 'HeadTurn', 'Smile', 'Settle')
BREATH, LOWER, TURN, SMILE, SETTLE = range(5)
ALPHA, DEFORMATION = 5, 6
LAYERS = ('Figure', 'Downcast', 'Blink')
FIGURE, DOWNCAST, BLINK = range(3)

# Timelines (seconds). Idle loops; Waiting and Reaction play in real time; Arrival, Victory, Defeat and Farewell
# follow the progress their caller sets, so their durations set relative timing.
IDLE_BREATHS = ((4.8, 1.0), (5.3, .84), (4.6, .95), (5.2, .9))
IDLE_BLINKS = (1.9, 6.35, 9.2, 14.8, 17.55)
# An eyelid overlay never sits between 0 and 1 longer than a blink: every change of its alpha takes OVERLAY_STEP or less
# (the merchant's blink fades in over 0.04 s and out over 0.055 s), so the painted eye and the lids never show at once.
OVERLAY_STEP = .1
WAITING = 1.5         # head lowers and turns (entry from the pose on screen over WAITING); the eyes drop at WAITING_EYES
WAITING_EYES = .3
ARRIVAL = 3.0         # she looks up: eyes open at ARRIVAL_EYES, then the head rises over ARRIVAL_LOWER, turns back over ARRIVAL_TURN
ARRIVAL_EYES = .15
ARRIVAL_LOWER = (.35, 2.2)
ARRIVAL_TURN = (.5, 2.5)
REACTION = 2.4        # the smile deepens to REACTION_SMILE over [0..1], holds, eases back by [3]
REACTION_TIMES = (0, .4, 1.3, 2.3)
REACTION_SMILE = .8
VICTORY = 3.5         # she settles back over VICTORY_SETTLE, her smile eases to VICTORY_SMILE, her eyes close for a moment
VICTORY_SETTLE = (.1, 1.8)
VICTORY_SMILE = -.5
VICTORY_EYES = (1.0, 1.9)  # a slow blink: the lids close at [0] and open at [1], each in OVERLAY_STEP
DEFEAT = 4.0          # the smile deepens to DEFEAT_SMILE and holds; a slow nod of DEFEAT_NOD
DEFEAT_SMILE_RISE = (.1, 1.3)
DEFEAT_SMILE = 1.0
DEFEAT_NOD = .45
DEFEAT_NOD_TIMES = (1.0, 2.1, 2.3, 3.6)
FAREWELL = 3.0        # the merchant's settling breath
SETTLE_TIME = .6      # an interrupted expression relaxes to rest over this time when a directed state starts


def soft(x, y, ellipse, falloff):
    cx, cy, rx, ry = ellipse
    return 1 - A.ease(math.hypot((x - cx) / rx, (y - cy) / ry), *falloff)


def head_weight(x, y):
    return soft(x, y, HEAD, HEAD_FALLOFF) * (1 - A.ease(y, *NECK_ROWS))


def body_weight(y):
    return A.ease(y, *TABLE_ROWS)


def breath_px(x, y):
    table = body_weight(y)
    if table == 0:
        return 0.0, 0.0
    lift = LIFT * table * (1 - (1 - HEAD_SHARE) * head_weight(x, y))
    spread = SPREAD * (x - CHEST[0]) / 200 * table * soft(x, y, CHEST, (.4, 1.3))
    return spread, lift


def lower_px(x, y):
    down = LOWER_CROWN + (LOWER_FACE - LOWER_CROWN) * A.ease(y, *LOWER_ROWS)
    return 0.0, -down * head_weight(x, y)


def turn_px(x, y):
    h = head_weight(x, y)
    return -(TURN_OUTLINE + (TURN_FACE - TURN_OUTLINE) * soft(x, y, FACE, FACE_FALLOFF)) * h, 0.0


def smile_px(x, y):
    dx = dy = 0.0
    for points, radius, (out, up) in ((CORNERS, CORNER_RADIUS, CORNER_MOVE), (CHEEKS, CHEEK_RADIUS, CHEEK_MOVE)):
        for (cx, cy), side in points:
            w = 1 - A.ease(math.hypot(x - cx, y - cy) / radius, *SMILE_FALLOFF)
            dx += side * out * w
            dy += up * w
    return dx, dy


def settle_px(x, y):
    t = body_weight(y)
    shrink = 1 - SETTLE_SCALE
    return -(x - SETTLE_CENTRE[0]) * shrink * t, (SETTLE_LIFT + (y - SETTLE_CENTRE[1]) * shrink) * t


FIELDS = (breath_px, lower_px, turn_px, smile_px, settle_px)


def normalized(field):
    def offset(u, v):
        dx, dy = field(u * SOURCE[0], (1 - v) * SOURCE[1])
        return dx / SOURCE[0], dy / SOURCE[1]
    return offset


def breath(t, period, amplitude):
    """One breath from rest: a measured inhale, a brief turn, a longer passive exhale, then a pause."""
    if t <= 0 or t >= period:
        return 0
    return amplitude * A.ease(t, 0, .40 * period) * (1 - A.ease(t, .44 * period, .88 * period))


def idle_breathing(t):
    start = 0
    for period, amplitude in IDLE_BREATHS:
        if t < start + period:
            return breath(t - start, period, amplitude)
        start += period
    return 0


def blink(t, times):
    # About 120 ms closed: a quick fade in, a short hold, a slightly slower fade out.
    return max((A.ease(t, at, at + .04) * (1 - A.ease(t, at + .085, at + .14)) for at in times), default=0)


def swell_and_settle(t, swell, rise, settle, fall, recover):
    """Additive breath weight: swell to `swell`, settle to `settle`, then return to rest (the merchant's cues)."""
    up, down, back = A.ease(t, *rise), A.ease(t, *fall), A.ease(t, *recover)
    return swell * up * (1 - down) + settle * down * (1 - back)


class Timelines:
    """The host's sequences, keeping each track's function so the motion can be measured from the same values."""

    def __init__(self):
        self.sequences = []
        self.tracks = {}

    def sequence(self, name, state, duration, **flags):
        seq = A.sequence(name, state, duration, **flags)
        self.sequences.append(seq)
        self.tracks[name] = []
        return seq

    def track(self, seq, layer, prop, fn, channel=0, add=False, entry=None):
        item = dict(Layer=layer, Property=prop, Channel=channel, Blend=int(add),
                    Curve=A.curve(fn, seq['Duration'], .0005 if prop == DEFORMATION else .00015))
        if entry is not None:
            item['EntryWeight'] = A.curve(entry, seq['Duration'])
        seq['Tracks'].append(item)
        self.tracks[seq['Id']].append((layer, prop, channel, add, fn))

    def deform(self, seq, channel, fn, add=False, entry=None):
        """One shape's weight on every layer, so the figure and the overlays deform identically."""
        for layer in range(len(LAYERS)):
            self.track(seq, layer, DEFORMATION, fn, channel, add, entry)


def timelines():
    tl = Timelines()
    fade = lambda seconds: (lambda t: 1 - A.ease(t, 0, seconds))
    relax = fade(SETTLE_TIME)

    idle = tl.sequence('Living still', 0, sum(period for period, _ in IDLE_BREATHS), loop=True)
    tl.deform(idle, BREATH, idle_breathing)
    tl.track(idle, BLINK, ALPHA, lambda t: blink(t, IDLE_BLINKS))

    waiting = tl.sequence('Head lowered over the pot', 1, WAITING, idle=True)
    tl.deform(waiting, LOWER, lambda t: 1, entry=fade(WAITING))
    tl.deform(waiting, TURN, lambda t: 1, entry=fade(WAITING))
    # Entering restarts the idle clock; the breath in progress blends into it.
    tl.deform(waiting, BREATH, lambda t: 0, add=True, entry=fade(.8))
    # Her eyes drop in one quick step once the head has begun to lower; lids already down stay down.
    eyes_down = lambda t: A.ease(t, WAITING_EYES, WAITING_EYES + OVERLAY_STEP)
    tl.track(waiting, DOWNCAST, ALPHA, eyes_down, entry=lambda t: 1 - eyes_down(t))
    # No blinks while her eyes are lowered; a blink in progress fades out.
    tl.track(waiting, BLINK, ALPHA, lambda t: 0, entry=fade(OVERLAY_STEP))

    arrival = tl.sequence('Looks up', 2, ARRIVAL, idle=True)
    tl.deform(arrival, LOWER, lambda t: 1 - A.ease(t, *ARRIVAL_LOWER), entry=fade(.35))
    tl.deform(arrival, TURN, lambda t: 1 - A.ease(t, *ARRIVAL_TURN), entry=fade(.35))
    tl.deform(arrival, BREATH, lambda t: swell_and_settle(t, .45, (.3, 1.3), -.12, (1.5, 2.3), (2.3, 2.95)),
              add=True, entry=fade(.8))
    # Her eyes open in one quick step before the head rises.
    tl.track(arrival, DOWNCAST, ALPHA, lambda t: 1 - A.ease(t, ARRIVAL_EYES, ARRIVAL_EYES + OVERLAY_STEP),
             entry=fade(OVERLAY_STEP))
    tl.track(arrival, BLINK, ALPHA, lambda t: 0, add=True, entry=fade(OVERLAY_STEP))

    # Additive over the idle; a reaction restarted mid-smile blends from its current pose.
    reaction = tl.sequence('Slight smile', 3, REACTION, idle=True, gesture=True)
    tl.deform(reaction, SMILE, lambda t: REACTION_SMILE * A.turn(t, *REACTION_TIMES), add=True, entry=fade(.1))

    victory = tl.sequence('Settles back', 4, VICTORY, idle=True)
    tl.deform(victory, SETTLE, lambda t: A.ease(t, *VICTORY_SETTLE), entry=relax)
    tl.deform(victory, SMILE, lambda t: VICTORY_SMILE * A.ease(t, *VICTORY_SETTLE), entry=relax)
    tl.deform(victory, LOWER, lambda t: 0, entry=relax)
    tl.deform(victory, TURN, lambda t: 0, entry=relax)
    tl.deform(victory, BREATH, lambda t: swell_and_settle(t, .4, (.1, 1.0), -.2, (1.2, 2.4), (2.6, 3.4)),
              add=True, entry=fade(.8))
    tl.track(victory, BLINK, ALPHA, lambda t: A.turn(t, VICTORY_EYES[0], VICTORY_EYES[0] + OVERLAY_STEP,
                                                     VICTORY_EYES[1], VICTORY_EYES[1] + OVERLAY_STEP),
             add=True, entry=fade(OVERLAY_STEP))

    defeat = tl.sequence('Satisfied nod', 5, DEFEAT, idle=True)
    tl.deform(defeat, SMILE, lambda t: DEFEAT_SMILE * A.ease(t, *DEFEAT_SMILE_RISE), entry=relax)
    tl.deform(defeat, LOWER, lambda t: DEFEAT_NOD * A.turn(t, *DEFEAT_NOD_TIMES), entry=relax)
    tl.deform(defeat, TURN, lambda t: 0, entry=relax)
    tl.deform(defeat, SETTLE, lambda t: 0, entry=relax)
    tl.deform(defeat, BREATH, lambda t: 0, add=True, entry=fade(.8))
    tl.track(defeat, BLINK, ALPHA, lambda t: 0, add=True, entry=fade(OVERLAY_STEP))

    farewell = tl.sequence('Farewell settle', 6, FAREWELL, idle=True)
    tl.deform(farewell, BREATH, lambda t: swell_and_settle(t, .15, (.05, .6), -.3, (.6, 1.6), (1.9, 2.9)),
              add=True, entry=fade(.6))
    for channel in (LOWER, TURN, SMILE, SETTLE):
        tl.deform(farewell, channel, lambda t: 0, entry=relax)
    tl.track(farewell, BLINK, ALPHA, lambda t: 0, add=True, entry=fade(OVERLAY_STEP))
    return tl


# ---------------------------------------------------------------------------------------------------------------------
# Unity files.

def file_id(name):
    return int(hashlib.md5(('SunkenLarderHost/' + name).encode('ascii')).hexdigest()[:15], 16)


def ensure_meta(path, body):
    """The path's GUID; a missing .meta is written (to the output folder unless writing assets)."""
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return A.guid(path)
    value = uuid.uuid5(uuid.NAMESPACE_URL, str(path.relative_to(A.ROOT)).replace('\\', '/')).hex
    destination = A.output_path(meta)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text('fileFormatVersion: 2\nguid: ' + value + '\n' + body, encoding='utf-8')
    return value


MATERIAL_META = 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
PREFAB_META = 'PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'


def texture_guid(path):
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        raise SystemExit('Missing imported artwork: ' + str(meta.relative_to(A.ROOT)))
    return A.guid(path)


def tex(guid):
    return '{fileID: 2800000, guid: %s, type: 3}' % guid if guid else '{fileID: 0}'


def write_material(path, source, mask, invert):
    text = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: %s
  m_Shader: {fileID: 4800000, guid: %s, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _LayerMask:
        m_Texture: %s
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MainTex:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _SourceTex:
        m_Texture: %s
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _ColorMask: 15
    - _InvertMask: %d
    - _Stencil: 0
    - _StencilComp: 8
    - _StencilOp: 0
    - _StencilReadMask: 255
    - _StencilWriteMask: 255
    - _UseUIAlphaClip: 0
    m_Colors:
    - _Color: {r: 1, g: 1, b: 1, a: 1}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
""" % (path.stem, MASKED_SHADER, tex(mask), tex(source), invert)
    destination = A.output_path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(text, encoding='utf-8')
    return ensure_meta(path, MATERIAL_META)


def uv_rect(crop):
    """The RawImage UV rectangle that stretches a crop texture over the full frame, keeping concept registration."""
    x0, y0, x1, y1 = crop
    w, h = x1 - x0, y1 - y0
    return -x0 / w, (y1 - SOURCE[1]) / h, SOURCE[0] / w, SOURCE[1] / h


def write_prefab(path, layers, mesh_guid):
    """Root (SpiritHostPortrait) > Portrait Layers > one SpiritPortraitImage per layer, all in the 941 x 1672 frame."""
    root_go, root_rt, portrait = file_id('root'), file_id('root/rect'), file_id('root/portrait')
    layers_go, layers_rt = file_id('layers'), file_id('layers/rect')
    ids = [(file_id(n), file_id(n + '/rect'), file_id(n + '/renderer'), file_id(n + '/image')) for n, *_ in layers]
    rt = """--- !u!224 &%d
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:%s
  m_Father: {fileID: %d}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 0, y: 0}
  m_AnchoredPosition: {x: %s, y: %s}
  m_SizeDelta: {x: 941, y: 1672}
  m_Pivot: {x: %s, y: %s}
"""
    go = """--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
%s  m_Layer: 5
  m_Name: %s
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""

    def children(items):
        return ''.join('\n  - {fileID: %d}' % i for i in items) if items else ' []'

    def components(items):
        return ''.join('  - component: {fileID: %d}\n' % i for i in items)

    parts = ['%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n']
    parts.append(go % (root_go, components([root_rt, portrait]), path.stem))
    parts.append(rt % (root_rt, root_go, children([layers_rt]), 0, 0, 0, 0, 0))
    parts.append("""--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name:
  m_EditorClassIdentifier: Dice.Presentation::Dice.Presentation.SpiritHostPortrait
  portraitRoot: {fileID: %d}
  authoredLayers:%s
""" % (portrait, root_go, PORTRAIT_SCRIPT, layers_rt, children([i[3] for i in ids])))
    parts.append(go % (layers_go, components([layers_rt]), 'Portrait Layers'))
    parts.append(rt % (layers_rt, layers_go, children([i[1] for i in ids]), root_rt, 0, 0, 0, 0))
    for (name, texture, material, colour_alpha, rect), (g, r, renderer, image) in zip(layers, ids):
        parts.append(go % (g, components([r, renderer, image]), name))
        parts.append(rt % (r, g, ' []', layers_rt, 470.5, 836, .5, .5))
        parts.append("""--- !u!222 &%d
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_CullTransparentMesh: 1
""" % (renderer, g))
        parts.append("""--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name:
  m_EditorClassIdentifier: Dice.Presentation::Dice.Presentation.SpiritPortraitImage
  m_Material: {fileID: 2100000, guid: %s, type: 2}
  m_Color: {r: 1, g: 1, b: 1, a: %s}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Texture: %s
  m_UVRect:
    serializedVersion: 2
    x: %s
    y: %s
    width: %s
    height: %s
  mesh: {fileID: 11400000, guid: %s, type: 2}
  weights:%s
""" % (image, g, IMAGE_SCRIPT, material, colour_alpha, tex(texture), *[A.scalar(float(v)) for v in rect], mesh_guid,
       ''.join('\n  - 0' for _ in SHAPES)))
    destination = A.output_path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(''.join(parts), encoding='utf-8')
    return ensure_meta(path, PREFAB_META), portrait


def lighting(base, reference):
    """Light 0, the hearth fire, by reference through the fire response, exactly as the background plate draws it."""
    return dict(
        BaseTexture=A.ref(texture_guid(base), 2800000, 3),
        Lights=[dict(LightIndex=0, Mode=0, Reference=A.ref(texture_guid(reference), 2800000, 3),
                     ResponseMap=A.ref(texture_guid(RELIGHT / 'SunkenLarderFireResponse.png'), 2800000, 3), Strength=1)],
        Depth=A.ref(None),
        DepthRange=A.v2(0, -1),
        FrameOrigin=A.Flow(x=0, y=0, z=0),
        FrameAxisX=A.Flow(x=1, y=0, z=0),
        FrameAxisY=A.Flow(x=0, y=1, z=0),
        MapRect=dict(serializedVersion=2, x=0, y=0, width=1, height=1),
        Wrap=1)


def stage_host(args):
    mesh = A.mesh(FOLDER / 'SunkenLarderHostMesh.asset', COLUMNS, ROWS,
                  [(name, normalized(field)) for name, field in zip(SHAPES, FIELDS)])
    figure = texture_guid(ART / 'SunkenLarderHag.png')
    specs = []
    for name, colour, base, mask, alpha, rect in (
            ('Figure', ART / 'SunkenLarderHag.png', RELIGHT / 'SunkenLarderHagUnlit.png', None, 1, (0, 0, 1, 1)),
            ('Downcast', ART / 'SunkenLarderHagDowncast.png', RELIGHT / 'SunkenLarderHagDowncastUnlit.png',
             ART / 'SunkenLarderHagDowncastMask.png', 0, uv_rect(CROP)),
            ('Blink', ART / 'SunkenLarderHagBlink.png', RELIGHT / 'SunkenLarderHagBlinkUnlit.png',
             ART / 'SunkenLarderHagBlinkMask.png', 0, uv_rect(CROP))):
        material = write_material(FOLDER / ('SunkenLarderHost%s.mat' % name), texture_guid(colour),
                                  texture_guid(mask) if mask else None, 0 if mask else 1)
        specs.append((name, colour, base, alpha, rect, material))
    prefab_guid, portrait = write_prefab(FOLDER / 'SunkenLarderHost.prefab',
                                         [(n, texture_guid(c), m, a, r) for n, c, b, a, r, m in specs], mesh)
    layers = []
    for name, colour, base, alpha, rect, material in specs:
        item = A.layer(name, texture_guid(colour), mesh, (SOURCE[0] / 2, SOURCE[1] / 2), SOURCE,
                       color=(1, 1, 1, alpha), weights=(0,) * len(SHAPES))
        item['ReceiveSceneLighting'] = True
        item['Smoke'] = dict(Enabled=False, MotionMask=A.ref(None), FlowNoise=A.ref(None),
                             Distortion=.035, Speed=.7, Opacity=.6, Flow=A.v2(0, .018))
        item['Lighting'] = lighting(base, colour)
        layers.append(item)
    tl = timelines()
    host = A.write_asset(FOLDER / 'SunkenLarderHost.asset', A.HOST_SCRIPT, dict(
        PortraitPrefab=A.ref(prefab_guid, portrait, 3),
        SigilPrefab=A.ref(A.guid(A.PRESENTATION / 'UI/SunkenLarderHostSigil.prefab'), 9079639759384952736, 3),
        SmokeShader=A.ref(None),
        LitLayerShader=A.ref(A.guid(A.ASSETS / 'Presentation/Shared/SceneLitImage.shader'), 4800000, 3),
        ReferenceSize=A.v2(*SOURCE), Scale=1, Offset=A.v2(0, 0), Layers=layers, Sequences=tl.sequences))
    report = {'host guid': host, 'prefab guid': prefab_guid, 'mesh guid': mesh, 'figure texture guid': figure,
              'grid': [COLUMNS, ROWS], 'shapes': list(SHAPES), 'overlay crop': CROP, 'overlay uv rect': uv_rect(CROP)}
    report.update(measure(tl))
    text = json.dumps(report, indent=2)
    (args.out / 'report.json').write_text(text + '\n', encoding='ascii')
    print(text)


# ---------------------------------------------------------------------------------------------------------------------
# Measurements of the rendered grid over her pixels.

def vertex_offsets():
    """Per shape, (rows + 1, columns + 1, 2) vertex displacements in px (right, up); row 0 is the frame's bottom."""
    out = np.zeros((len(FIELDS), ROWS + 1, COLUMNS + 1, 2))
    for s, field in enumerate(FIELDS):
        for row in range(ROWS + 1):
            for column in range(COLUMNS + 1):
                out[s, row, column] = field(column / COLUMNS * SOURCE[0], (1 - row / ROWS) * SOURCE[1])
    return out


def pixel_field(vertices, weights):
    """Displacement (right, up) of every pixel centre under the weighted shapes, bilinear between vertices."""
    grid = np.tensordot(np.asarray(weights, np.float64), vertices, axes=1)
    ys, xs = np.mgrid[0:SOURCE[1], 0:SOURCE[0]].astype(np.float64) + .5
    gx = xs / SOURCE[0] * COLUMNS
    gy = (1 - ys / SOURCE[1]) * ROWS
    return np.stack([map_coordinates(grid[..., k], [gy, gx], order=1, mode='nearest') for k in range(2)], -1)


def pose_stats(field, alpha, edge_rows):
    magnitude = np.hypot(field[..., 0], field[..., 1])
    visible = alpha >= .02 * 255
    m = np.where(visible, magnitude, 0)
    y, x = np.unravel_index(np.argmax(m), m.shape)
    ddx_x = np.diff(field[..., 0], axis=1)[:-1]
    ddx_y = np.diff(field[..., 0], axis=0)[:, :-1]
    ddy_x = np.diff(field[..., 1], axis=1)[:-1]
    ddy_y = np.diff(field[..., 1], axis=0)[:, :-1]
    # Top-left image coordinates: X = x + dx, Y = y - dy_up.
    jxx, jxy, jyx, jyy = 1 + ddx_x, ddx_y, -ddy_x, 1 - ddy_y
    inside = visible[:-1, :-1]
    strain = np.max(np.abs(np.stack([jxx - 1, jxy, jyx, jyy - 1]))[:, inside]) if inside.any() else 0
    folds = int(np.sum((jxx * jyy - jxy * jyx <= 0) & inside))
    low = m[edge_rows:]
    ly, lx = np.unravel_index(np.argmax(low), low.shape)
    return {'largest px': round(float(m[y, x]), 3), 'at (x, y)': [int(x), int(y)],
            'largest at or below row %d px' % edge_rows: round(float(low[ly, lx]), 4),
            'there (x, y)': [int(lx), int(ly + edge_rows)],
            'largest strain': round(float(strain), 4), 'folded pixels': folds}


def sampled_poses(tl):
    """Weights per shape over every state's timeline: idle at its own time (directed states sample it at progress x
    duration, as the portrait does; Waiting and Reaction at the same time), plus the state's tracks."""
    poses = []
    idle = dict((channel, fn) for layer, prop, channel, add, fn in tl.tracks['Living still']
                if layer == FIGURE and prop == DEFORMATION)
    for seq in tl.sequences:
        duration = seq['Duration']
        for i in range(int(duration * 20) + 1):
            t = duration * i / (duration * 20)
            weights = [idle[c](t) if c in idle else 0 for c in range(len(SHAPES))]
            if seq['State'] != 0:
                for layer, prop, channel, add, fn in tl.tracks[seq['Id']]:
                    if layer == FIGURE and prop == DEFORMATION:
                        weights[channel] = weights[channel] + fn(t) if add else fn(t)
            poses.append((seq['Id'], round(t, 3), weights))
    return poses


def measure(tl):
    hag = load(ART / 'SunkenLarderHag.png')
    alpha = hag[..., 3]
    vertices = vertex_offsets()
    edge_rows = 780
    result = {'shapes at full weight': {}, 'states': {}}
    for s, name in enumerate(SHAPES):
        for w in (1, -1) if name == 'Smile' else (1,):
            weights = [0] * len(SHAPES)
            weights[s] = w
            result['shapes at full weight']['%s %+g' % (name, w)] = pose_stats(pixel_field(vertices, weights), alpha, edge_rows)
    worst = {}
    for seq_id, t, weights in sampled_poses(tl):
        key = seq_id
        magnitude_bound = sum(abs(w) for w in weights)
        if key not in worst or magnitude_bound > worst[key][0]:
            worst[key] = (magnitude_bound, t, weights)
    overall = None
    for seq_id, (_, t, weights) in worst.items():
        stats = pose_stats(pixel_field(vertices, weights), alpha, edge_rows)
        stats['time s'] = t
        stats['weights'] = dict(zip(SHAPES, [round(w, 4) for w in weights]))
        result['states'][seq_id] = stats
        if overall is None or stats['largest px'] > overall[1]['largest px']:
            overall = (seq_id, stats)
    result['largest displacement anywhere on her'] = {'state': overall[0], **overall[1]}
    result['motion margin of the plate (full reconstruction within 22 px of her)'] = 22
    result['grid against the authored field over her face %s, px' % (FIDELITY_BOX,)] = fidelity(vertices, alpha)
    result['longest overlay alpha between 0.02 and 0.98, s'] = overlay_steps(tl)
    return result


# The face, where the grid must follow the authored head and smile shapes closely.
FIDELITY_BOX = (380, 380, 620, 620)


def fidelity(vertices, alpha):
    """Per shape, the largest difference between the rendered grid's displacement and the authored field on her pixels."""
    x0, y0, x1, y1 = FIDELITY_BOX
    out = {}
    for s, (name, field) in enumerate(zip(SHAPES, FIELDS)):
        weights = [0] * len(SHAPES)
        weights[s] = 1
        grid = pixel_field(vertices, weights)
        worst = 0.0
        for y in range(y0, y1):
            for x in range(x0, x1):
                if alpha[y, x] < .02 * 255:
                    continue
                dx, dy = field(x + .5, y + .5)
                worst = max(worst, math.hypot(grid[y, x, 0] - dx, grid[y, x, 1] - dy))
        out[name] = round(worst, 3)
    return out


def overlay_steps(tl):
    """Per sequence and overlay, the longest stretch its alpha track spends between 0.02 and 0.98 (entered from rest)."""
    out = {}
    for seq in tl.sequences:
        for layer, prop, channel, add, fn in tl.tracks[seq['Id']]:
            if prop != ALPHA or layer == FIGURE:
                continue
            longest = run = 0.0
            step = .001
            for i in range(int(seq['Duration'] / step) + 1):
                run = run + step if .02 < fn(i * step) < .98 else 0.0
                longest = max(longest, run)
            out['%s, %s' % (seq['Id'], LAYERS[layer])] = round(longest, 3)
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = parser.add_subparsers(dest='stage', required=True)
    sub.add_parser('target', help='Write the overlays\' edit target.')
    overlays = sub.add_parser('overlays', help='Build the eyelid overlays from the donors.')
    overlays.add_argument('--donor-blink', help='A candidate closed-eyelid donor in place of the preserved one.')
    overlays.add_argument('--donor-downcast', help='A candidate downcast donor in place of the preserved one.')
    host = sub.add_parser('host', help='Write the mesh, materials, prefab and host asset.')
    for p in (overlays, host):
        p.add_argument('--write-assets', action='store_true', help='Also write the production files in place.')
        p.add_argument('--output', type=Path, help='A new directory beneath Docs/work.')
    args = parser.parse_args()
    if args.stage == 'target':
        stage_target(args)
        return
    scratch = (A.ROOT / 'Docs/work').resolve()
    if args.output:
        out = args.output.resolve()
        if not out.is_relative_to(scratch) or out == scratch:
            parser.error('--output must be a new directory beneath Docs/work.')
        out.mkdir(parents=True, exist_ok=False)
    else:
        out = Path(tempfile.mkdtemp(prefix='sunken-larder-host-', dir=scratch))
    args.out = out
    if args.stage == 'overlays':
        if args.write_assets and (args.donor_blink or args.donor_downcast):
            parser.error('--write-assets installs only from the preserved donors.')
        stage_overlays(args)
    else:
        # Without --write-assets the Unity files go to the output folder; the report always does.
        if not args.write_assets:
            A.OUTPUT = out
        stage_host(args)
        print('Authored the Sunken Larder host in ' + str(A.OUTPUT or A.ROOT) + '; report in ' + str(out))


if __name__ == '__main__':
    main()
