"""Author the Sunken Larder arrival picture's chimney plume: the painted smoke moving by its own pixels, as a picture scene flow layer.

Python 3.10 or later with numpy, Pillow and scipy (on this machine Docs/work/merchant-table-clear/.venv/Scripts/python.exe).
Run from the repository root:

    python Tools/Presentation/author-sunken-larder-arrival-plume.py                 # fresh Docs/work/sunken-larder-arrival-plume-* folder
    python Tools/Presentation/author-sunken-larder-arrival-plume.py --write-assets  # also installs the window and the flow

It only reads the accepted arrival picture (Art/SunkenLarder/SunkenLarderArrival.png, never changed, the picture scene's
plate), so reruns give the same files; see "Arrival motion" in Docs/Reference/Art/SunkenLarderLayers.md. The drift layers
of the same picture (its haze and low fog) are built by author-sunken-larder-arrival-motion.py, whose image helpers this
script imports.

The plume is a flow layer (PictureFlowLayer): nothing is drawn over the painting. Inside the layer's rectangle the shader
redraws the picture pulled along a flow field, in two phases half a period apart that cross-fade by a triangle weight, and
blends it over the plate by the window's alpha. This script measures the plume and writes:

- The window (alpha): the painted plume's presence (its luminance above the grey opening of the dark around it), spread a
  few px past the smoke, inside a band around the plume's centre line, faded out at the line's end, zero on the chimney's
  cap and rising from zero at the chimney's mouth, so the chimney never moves.
- The flow (RG = direction * relative speed * 0.5 + 0.5, x right, y up; B = a low-frequency noise for the phase): along
  the measured centre line, rising straight out of the mouth and turning onto the line, slower towards the plume's sides
  and near the mouth, with mild low-frequency noise in its angle and speed so the wisps break up.

The report gives the flow layer's values for SunkenLarderArrivalPicture.asset (rectangle in picture units, origin bottom
left, speed, phase period and noise strength), the measured centre line, and the zero-displacement check. The review
images simulate the shader's maths over the picture.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import math
import tempfile

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt, gaussian_filter, grey_opening, map_coordinates, maximum_filter

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / 'Tools/Presentation'


def _load_module(name, file):
    spec = importlib.util.spec_from_file_location(name, TOOLS / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ARM = _load_module('author_sunken_larder_arrival_motion', 'author-sunken-larder-arrival-motion.py')
W, H, ART, WORK, PICTURE = ARM.W, ARM.H, ARM.ART, ARM.WORK, ARM.PICTURE
load, save, rel, labelled, row, column = ARM.load, ARM.save, ARM.rel, ARM.labelled, ARM.row, ARM.column
to_linear, to_srgb, smoothstep, polygon_mask = ARM.to_linear, ARM.to_srgb, ARM.smoothstep, ARM.polygon_mask

WINDOW_PATH = ART / 'SunkenLarderArrivalPlumeWindow.png'
FLOW_PATH = ART / 'SunkenLarderArrivalPlumeFlow.png'

# ------------------------------------------------------------------------------------------------------------- plume
# The painted plume's presence: luminance above its grey opening (PRESENCE_OPENING px, blurred PRESENCE_OPENING_BLUR px),
# softened PRESENCE_SOFTEN px. The plume is a thin curl, so the opening is narrower than the cauldron steam's.
PRESENCE_OPENING, PRESENCE_OPENING_BLUR, PRESENCE_SOFTEN = 41, 10.0, 3.0
# The centre line is measured, not drawn: in each column from MEASURE_COLUMNS, the presence-squared-weighted mean row
# within GUIDE_REACH rows of GUIDE, a rough line by eye only telling the measure where to look (it keeps the measure off the
# lit branches above and below), then smoothed CENTRE_SMOOTH px along x. It starts at the chimney's mouth, MOUTH (concept
# px, top-left origin), the top of the chimney's cap.
GUIDE = [(236, 246), (262, 228), (300, 214), (340, 200), (380, 182), (420, 166), (460, 162), (488, 168)]
GUIDE_REACH = 30
MEASURE_COLUMNS = range(244, 485, 2)
CENTRE_SMOOTH = 12.0
MOUTH = (233.0, 248.0)
# The chimney's cap and flue (concept px); the mouth is the cap's top.
CAP = [(206, 268), (231, 245), (237, 246), (262, 262), (262, 320), (206, 320)]

# ------------------------------------------------------------------------------------------------------------ window
# Presence from PRESENCE_LEVELS mapped to 0 to 1 (smoothstep), spread SPREAD px past the smoke (a maximum filter, then a
# blur of SPREAD_SOFTEN px), so the window is full on the smoke and fades to zero just beyond it. Inside a band around the
# centre line, full to BAND_FULL px from it and none past BAND_EDGE; faded out over END_FADE px past the line's end and
# over START_FADE px below the mouth; zero on the cap and up to CAP_STILL px from it, full from CAP_CLEAR px, so the
# chimney never moves and none of it is pulled into the smoke.
PRESENCE_LEVELS = (7.0, 22.0)
SPREAD, SPREAD_SOFTEN = 4, 3.0
BAND_FULL, BAND_EDGE = 26.0, 40.0
END_FADE = 24.0
START_FADE = 6.0
CAP_STILL, CAP_CLEAR = 1.0, 14.0
RECT_MARGIN = 2
# The plume's region (concept px, x0, y0, x1, y1); the window and flow are zero outside it.
PLUME_BOX = (150, 60, 600, 360)

# -------------------------------------------------------------------------------------------------------------- flow
# Out of the mouth the flow rises straight up, turning onto the centre line's direction over RISE px along it. Relative
# speed is 1 on the line, falling to SIDE_SPEED at BAND_EDGE from it (the sides lag, so wisps shear and curl), and rises
# from CAP_SPEED at the cap to full CAP_SPEED_UP px from it, so wherever the window is above CAP_PULL_WINDOW no phase pulls
# a pixel as far as its distance from the cap, and the cap's colour does not enter the smoke. Noise: two low-frequency fields (Gaussian of white noise, sigma NOISE_SCALE px, seeded NOISE_SEED, normalised to
# unit deviation) turn the direction by up to NOISE_ANGLE degrees (one deviation) and scale the speed by 1 plus or minus
# NOISE_SPEED; a third (sigma PHASE_NOISE_SCALE px), mapped to 0 to 1, is the phase noise in B.
RISE = 20.0
SIDE_SPEED = 0.7
CAP_SPEED, CAP_SPEED_UP = 0.1, 50.0
CAP_PULL_WINDOW = 0.2
NOISE_SEED = 7
NOISE_SCALE, NOISE_ANGLE, NOISE_SPEED = 14.0, 18.0, 0.15
PHASE_NOISE_SCALE = 24.0

# The layer's values. SPEED is concept px per second at relative speed 1: a slow drifting plume, a little slower than the
# table's cauldron steam rises (14.8 and 22.1 px/s), so the smoke leaves the chimney calmly into still night air.
# PHASE_PERIOD: each phase pulls the plate at most SPEED * PHASE_PERIOD = 36 px, about one painted wisp, before it fades
# out, so no phase stretches the plume beyond its own curl; shorter periods make the cross-fade pulse visibly. The two
# phases trade places every half period, so the drawn motion repeats every PHASE_PERIOD / 2 seconds.
# NOISE_STRENGTH: half a period of phase spread across the plume, so its parts do not fade in unison.
SPEED = 12.0
PHASE_PERIOD = 3.0
NOISE_STRENGTH = 0.5

# ------------------------------------------------------------------------------------------------------------ review
REVIEW_BOX = (196, 112, 516, 292)   # the plume region, concept px (x0, y0, x1, y1)
REVIEW_SCALE = 2
REVIEW_TIMES = [0.5 * i for i in range(8)]
ARROW_STEP = 10
FRAME_540 = (540, 960)
FRAME_540_TIME = 1.0

LUMA = np.array([0.2126, 0.7152, 0.0722])
OUT = None


# ------------------------------------------------------------------------------------------------------------ measure
def presence(plate):
    lum = plate @ LUMA
    floor = gaussian_filter(grey_opening(lum, size=(PRESENCE_OPENING, PRESENCE_OPENING)), PRESENCE_OPENING_BLUR)
    return gaussian_filter(np.clip(lum - floor, 0, None), PRESENCE_SOFTEN)


def centre_line(excess):
    """The measured centre line as a dense polyline (concept px, about 1 px apart) from the mouth, and the column
    measures."""
    gx, gy = zip(*GUIDE)
    xs, ys = [], []
    for x in MEASURE_COLUMNS:
        g = np.interp(x, gx, gy)
        rows = np.arange(int(round(g - GUIDE_REACH)), int(round(g + GUIDE_REACH)) + 1)
        w = excess[rows, x] ** 2
        xs.append(float(x))
        ys.append(float((w * rows).sum() / w.sum()))
    step = MEASURE_COLUMNS.step
    smooth = gaussian_filter(np.array(ys), CENTRE_SMOOTH / step, mode='nearest')
    points = np.concatenate([[MOUTH], np.stack([xs, smooth], -1)])
    # resample about 1 px apart along its length
    seg = np.hypot(*np.diff(points, axis=0).T)
    s = np.concatenate([[0], np.cumsum(seg)])
    t = np.arange(0, s[-1], 1.0)
    dense = np.stack([np.interp(t, s, points[:, 0]), np.interp(t, s, points[:, 1])], -1)
    return dense, list(zip(xs, ys)), smooth


def nearest_on_line(line, box):
    """For every pixel of the box (x0, y0, x1, y1): the index of the nearest centre-line point and the distance to it."""
    x0, y0, x1, y1 = box
    yy, xx = np.mgrid[y0:y1, x0:x1] + 0.5
    best = np.full(yy.shape, np.inf)
    index = np.zeros(yy.shape, int)
    for i, (px, py) in enumerate(line):
        d = (xx - px) ** 2 + (yy - py) ** 2
        closer = d < best
        best[closer] = d[closer]
        index[closer] = i
    return index, np.sqrt(best), xx, yy


def tangents(line):
    d = np.gradient(line, axis=0)
    d = gaussian_filter(d, (3.0, 0), mode='nearest')
    return d / np.linalg.norm(d, axis=1, keepdims=True)


def build(plate):
    """The window (0 to 1) and flow (direction times relative speed, x right and y up; phase noise 0 to 1) over the full
    picture, the rectangle, the centre line and the measures."""
    excess = presence(plate)
    line, measured, smooth = centre_line(excess)
    bx0, by0, bx1, by1 = PLUME_BOX
    index, dist, xx, yy = nearest_on_line(line, PLUME_BOX)
    excess = excess[by0:by1, bx0:bx1]
    along = index.astype(float)              # px along the line, about 1 px per point
    length = float(len(line) - 1)
    tang = tangents(line)
    # past the line's end, along keeps growing with the projection onto the last tangent
    end = line[-1]
    beyond = np.clip((xx - end[0]) * tang[-1, 0] + (yy - end[1]) * tang[-1, 1], 0, None) * (index == len(line) - 1)

    lo, hi = PRESENCE_LEVELS
    p = smoothstep((excess - lo) / (hi - lo))
    p = gaussian_filter(maximum_filter(p, size=2 * SPREAD + 1), SPREAD_SOFTEN)
    band = 1 - smoothstep((dist - BAND_FULL) / (BAND_EDGE - BAND_FULL))
    end_fade = 1 - smoothstep(beyond / END_FADE)
    before = np.clip(yy - MOUTH[1], 0, None) * (index == 0)
    start_fade = 1 - smoothstep(before / START_FADE)
    d_cap = distance_transform_edt(polygon_mask(CAP, 0)[by0:by1, bx0:bx1] < 0.5)
    cap = smoothstep((d_cap - CAP_STILL) / (CAP_CLEAR - CAP_STILL))
    window = np.zeros((H, W))
    window[by0:by1, bx0:bx1] = np.clip(p * band * end_fade * start_fade * cap, 0, 1)

    # direction: up out of the mouth, turning onto the line's tangent over RISE px along it (y up)
    tx, ty = tang[index, 0], -tang[index, 1]
    turn = smoothstep(along / RISE)
    dx, dy = tx * turn, ty * turn + (1 - turn)
    rng = np.random.default_rng(NOISE_SEED)

    def field(sigma):
        n = gaussian_filter(rng.standard_normal((by1 - by0, bx1 - bx0)), sigma, mode='reflect')
        return n / n.std()
    angle = np.radians(NOISE_ANGLE) * field(NOISE_SCALE)
    speed_noise = 1 + NOISE_SPEED * np.clip(field(NOISE_SCALE), -1, 1)
    phase = np.zeros((H, W))
    phase[by0:by1, bx0:bx1] = smoothstep((field(PHASE_NOISE_SCALE) + 2.0) / 4.0)  # 0 to 1, past 2 deviations clamped
    ca, sa = np.cos(angle), np.sin(angle)
    dx, dy = dx * ca - dy * sa, dx * sa + dy * ca
    norm = np.maximum(np.hypot(dx, dy), 1e-6)
    side = 1 - (1 - SIDE_SPEED) * smoothstep(dist / BAND_EDGE)
    source = CAP_SPEED + (1 - CAP_SPEED) * smoothstep(d_cap / CAP_SPEED_UP)
    rel_speed = np.clip(side * source * speed_noise, 0, 1)
    flow = np.zeros((H, W, 2))
    flow[by0:by1, bx0:bx1] = np.stack([dx / norm * rel_speed, dy / norm * rel_speed], -1)

    ys, xs = np.nonzero(window > 0.002)
    x0, y0 = max(0, xs.min() - RECT_MARGIN), max(0, ys.min() - RECT_MARGIN)
    x1, y1 = min(W, xs.max() + 1 + RECT_MARGIN), min(H, ys.max() + 1 + RECT_MARGIN)
    rect = (int(x0), int(y0), int(x1 - x0), int(y1 - y0))
    measures = {'measured_columns': [[x, round(y, 1)] for x, y in measured],
                'centre_line_every_20px': [[round(float(a), 1), round(float(b), 1)] for a, b in line[::20]],
                'centre_line_length_px': round(length, 1),
                # a phase's longest pull over the distance to the cap, where the window is above CAP_PULL_WINDOW; below 1
                # none reaches it
                'cap_pull_over_distance_max': float((SPEED * PHASE_PERIOD * rel_speed / np.maximum(d_cap, 0.5))[
                    window[by0:by1, bx0:bx1] > CAP_PULL_WINDOW].max())}
    return window, flow, phase, rect, line, measures


# -------------------------------------------------------------------------------------------------------------- encode
def encode(window, flow, phase, rect):
    """The installed textures over the rectangle: the window as white with alpha, the flow as RGB."""
    x, y, w, h = rect
    win = np.rint(window[y:y + h, x:x + w] * 255)
    window_rgba = np.concatenate([np.full((h, w, 3), 255.0), win[..., None]], -1)
    rg = np.rint((flow[y:y + h, x:x + w] * 0.5 + 0.5) * 255)
    b = np.rint(phase[y:y + h, x:x + w] * 255)
    flow_rgb = np.concatenate([rg, b[..., None]], -1)
    return window_rgba, flow_rgb


def layer_values(rect):
    x, y, w, h = rect
    return {'rect': [x / W, 1 - (y + h) / H, w / W, h / H], 'speed': SPEED, 'phasePeriod': PHASE_PERIOD,
            'noiseStrength': NOISE_STRENGTH}


# ------------------------------------------------------------------------------------------------------------ simulate
def bilinear(img, cx, cy):
    """Samples an image (rows top-down) at concept-texel coordinates of its own (x right, y down; pixel centres at
    .5), bilinear and clamped, as the GPU samples a clamped texture."""
    coords = [cy - 0.5, cx - 0.5]
    if img.ndim == 2:
        return map_coordinates(img, coords, order=1, mode='nearest')
    return np.stack([map_coordinates(img[..., c], coords, order=1, mode='nearest') for c in range(img.shape[-1])], -1)


class Layer:
    """The flow layer as the shader draws it, from the installed (8-bit) textures."""

    def __init__(self, plate_lin, window_rgba, flow_rgb, rect, speed=SPEED):
        self.plate = plate_lin
        self.alpha = window_rgba[..., 3] / 255.0
        self.flow = flow_rgb[..., :2] / 255.0 * 2 - 1
        self.noise = flow_rgb[..., 2] / 255.0
        self.rect = rect
        self.reach = speed * PHASE_PERIOD

    def draw(self, cx, cy, t):
        """The frame's linear colour at concept points (cx, cy) at t seconds: the background, quantized to 8 bits as the
        frame stores it, with the layer blended over it by the window."""
        background = to_linear(np.rint(to_srgb(bilinear(self.plate, cx, cy))))
        x, y, w, h = self.rect
        lx, ly = cx - x, cy - y
        inside = (lx >= 0) & (lx < w) & (ly >= 0) & (ly < h)
        a = bilinear(self.alpha, lx, ly) * inside
        flow = bilinear(self.flow, lx, ly)
        noise = bilinear(self.noise, lx, ly)
        phase_a = np.mod(t / PHASE_PERIOD + noise * NOISE_STRENGTH, 1.0)
        phase_b = np.mod(phase_a + 0.5, 1.0)
        weight_a = 1 - np.abs(1 - 2 * phase_a)
        # displacement in concept px; flow y is up, rows go down
        pulled = []
        for ph in (phase_a, phase_b):
            d = self.reach * ph
            pulled.append(bilinear(self.plate, cx - flow[..., 0] * d, cy + flow[..., 1] * d))
        flowed = pulled[1] + (pulled[0] - pulled[1]) * weight_a[..., None]
        return background * (1 - a[..., None]) + flowed * a[..., None]


def grid(box, scale):
    """Concept coordinates of the pixel centres of the box drawn at the scale."""
    x0, y0, x1, y1 = box
    n_x, n_y = int(round((x1 - x0) * scale)), int(round((y1 - y0) * scale))
    cy, cx = np.mgrid[0:n_y, 0:n_x] + 0.5
    return x0 + cx / scale, y0 + cy / scale


def frame_540_grid():
    """Concept coordinates of a 540 x 960 frame showing the picture's cover crop, as the journey screen draws it."""
    fw, fh = FRAME_540
    aspect = fw / fh
    if W / H > aspect:
        cw = H * aspect
        x0, scale = (W - cw) / 2, fh / H
        cy, cx = np.mgrid[0:fh, 0:fw] + 0.5
        return x0 + cx / scale, cy / scale
    ch = W / aspect
    y0, scale = (H - ch) / 2, fw / W
    cy, cx = np.mgrid[0:fh, 0:fw] + 0.5
    return cx / scale, y0 + cy / scale


def srgb8(lin):
    return np.rint(to_srgb(np.clip(lin, 0, 1)))


# -------------------------------------------------------------------------------------------------------------- review
def reviews(rdir, plate, layer, still, window, flow, rect, line, report):
    rdir.mkdir(parents=True, exist_ok=True)
    cx, cy = grid(REVIEW_BOX, REVIEW_SCALE)
    frames = [labelled(np.rint(to_srgb(bilinear(layer.plate, cx, cy))), 'picture (no flow layer), 2x')]
    shots = {}
    for t in REVIEW_TIMES:
        shots[t] = srgb8(layer.draw(cx, cy, t))
        frames.append(labelled(shots[t], f't = {t:.1f} s, 2x'))
    save(rdir / 'plume-frames.png', column([row(frames[i:i + 3]) for i in range(0, len(frames), 3)]))
    steps = [np.abs(shots[b] - shots[a]).max(-1) for a, b in zip(REVIEW_TIMES, REVIEW_TIMES[1:])]
    report['half_second_change_levels'] = {'max': float(np.max(steps)), 'p99': float(np.percentile(np.max(steps, 0), 99))}

    # the window tinted over the picture, the rectangle and the centre line outlined
    x0, y0, x1, y1 = REVIEW_BOX
    tint = plate.copy()
    tint = tint * (1 - 0.6 * window[..., None]) + np.array([255, 120, 0.]) * 0.6 * window[..., None]
    im = Image.fromarray(np.clip(np.rint(tint), 0, 255).astype(np.uint8)).crop(REVIEW_BOX)
    im = im.resize((im.width * REVIEW_SCALE, im.height * REVIEW_SCALE), Image.NEAREST)
    d = ImageDraw.Draw(im)
    rx, ry, rw, rh = rect
    s = REVIEW_SCALE
    d.rectangle(((rx - x0) * s, (ry - y0) * s, (rx + rw - x0) * s - 1, (ry + rh - y0) * s - 1), outline=(0, 220, 255))
    d.line([((px - x0) * s, (py - y0) * s) for px, py in line[::4]], fill=(255, 255, 255), width=1)
    mx, my = MOUTH
    d.ellipse(((mx - x0) * s - 3, (my - y0) * s - 3, (mx - x0) * s + 3, (my - y0) * s + 3), outline=(255, 0, 0))
    save(rdir / 'plume-window.png', labelled(np.asarray(im).astype(float),
                                             'window (orange), rectangle (cyan), centre line (white), mouth (red), 2x'))

    # the flow as arrows over the picture where the window shows, length = relative speed
    im = Image.fromarray(plate.astype(np.uint8)).crop(REVIEW_BOX)
    im = im.resize((im.width * s, im.height * s), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for py in range(y0 + ARROW_STEP // 2, y1, ARROW_STEP):
        for px in range(x0 + ARROW_STEP // 2, x1, ARROW_STEP):
            if window[py, px] < 0.05:
                continue
            fx, fy = flow[py, px]
            ax, ay = (px - x0 + 0.5) * s, (py - y0 + 0.5) * s
            bx, by = ax + fx * ARROW_STEP * s * 0.9, ay - fy * ARROW_STEP * s * 0.9
            c = (int(80 + 175 * window[py, px]), 230, 80)
            d.line([(ax, ay), (bx, by)], fill=c, width=1)
            ang = math.atan2(by - ay, bx - ax)
            for side in (2.6, -2.6):
                d.line([(bx, by), (bx + 5 * math.cos(ang + side), by + 5 * math.sin(ang + side))], fill=c, width=1)
    save(rdir / 'plume-flow.png', labelled(np.asarray(im).astype(float),
                                           'flow (arrow length = relative speed, brighter = stronger window), 2x'))

    # one whole-picture frame at 540 x 960
    fx, fy = frame_540_grid()
    save(rdir / 'frame-540.png', labelled(srgb8(layer.draw(fx, fy, FRAME_540_TIME)), f'540 x 960, t = {FRAME_540_TIME:.1f} s, the flow layer alone (no drift layers)'))

    # zero displacement: the layer with no speed against the background alone, at 2x over the plume and at 540 x 960
    zero = {}
    for name, (gx, gy) in {'plume_2x': (cx, cy), 'frame_540': (fx, fy)}.items():
        plate_only = srgb8(to_linear(np.rint(to_srgb(bilinear(still.plate, gx, gy)))))
        drawn = srgb8(still.draw(gx, gy, 1.7))
        diff = np.abs(drawn - plate_only)
        zero[name] = {'max_level_difference': float(diff.max()), 'pixels_differing': int((diff.max(-1) > 0).sum()),
                      'pixels': int(diff.shape[0] * diff.shape[1])}
    report['zero_displacement'] = zero


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--output', help='Unused directory under Docs/work (default: a fresh sunken-larder-arrival-plume-* directory).')
    parser.add_argument('--write-assets', action='store_true', help='Also install the window and the flow.')
    args = parser.parse_args()
    if args.output:
        OUT = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in OUT.parents:
            raise SystemExit('--output must be under Docs/work.')
        OUT.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        OUT = Path(tempfile.mkdtemp(prefix='sunken-larder-arrival-plume-', dir=WORK))

    plate = load(PICTURE)
    window, flow, phase, rect, line, measures = build(plate)
    window_rgba, flow_rgb = encode(window, flow, phase, rect)
    plate_lin = to_linear(plate)
    layer = Layer(plate_lin, window_rgba, flow_rgb, rect)
    still = Layer(plate_lin, window_rgba, flow_rgb, rect, speed=0.0)
    x, y, w, h = rect
    report = {'picture': rel(PICTURE), 'layer': dict(layer_values(rect), window=rel(WINDOW_PATH), flow=rel(FLOW_PATH),
                                                     rect_xywh_px=list(rect), reach_px=SPEED * PHASE_PERIOD),
              'measures': measures,
              'window': {'pixels_over_half': int((window > 0.5).sum()), 'max': float(window.max()),
                         'at_mouth': float(window[int(MOUTH[1]), int(MOUTH[0])]),
                         'on_cap_max': float(window[polygon_mask(CAP, 0) > 0.5].max())},
              'flow': {'relative_speed_max': float(np.hypot(*flow[y:y + h, x:x + w].transpose(2, 0, 1)).max())}}
    save(OUT / 'textures' / WINDOW_PATH.name, window_rgba)
    save(OUT / 'textures' / FLOW_PATH.name, flow_rgb)
    reviews(OUT / 'review', plate, layer, still, window, flow, rect, line, report)
    if args.write_assets:
        # Existing .meta files are kept; new textures get their import settings in Unity (the window like
        # SunkenLarderArrivalFogFarWindow.png, the flow linear, clamped, without mipmaps or compression).
        save(WINDOW_PATH, window_rgba)
        save(FLOW_PATH, flow_rgb)
        report['installed'] = [rel(WINDOW_PATH), rel(FLOW_PATH)]
    (OUT / 'report.json').write_text(json.dumps(report, indent=1, default=float) + '\n', encoding='ascii')
    print(json.dumps(report, indent=1, default=float))
    print('Wrote ' + str(OUT))


if __name__ == '__main__':
    main()
