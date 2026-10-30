"""Author the Bound Tree's ambient layers: hanging moss that sways, mist that drifts and creatures that come out of hiding.

Python 3.10 or later with numpy, Pillow and scipy (on this machine Docs/work/merchant-table-clear/.venv/Scripts/python.exe).
Run from the repository root:

    python Tools/Presentation/author-bound-tree-ambient.py                 # fresh Docs/work/bound-tree-ambient-* folder
    python Tools/Presentation/author-bound-tree-ambient.py --write-assets  # also installs textures, meshes and paths

It reads the concept, the installed Bound Tree plates and layers, and the kept donors in Docs/Reference/Art (see
Docs/Reference/Art/BoundTreeLayers.md, "Ambient layers"), so reruns give the same files. The method is the merchant's and
the nymph's, imported rather than copied: registration, ratio maps and harmonic fill from author-merchant-relight.py,
colour matching and bleeding from cut-merchant-props.py, the relight receiver weight from author-bound-tree-relight.py
and the deformation mesh writer from author-spirit-presentations.py.

Moss: each side's removal donor is registered to the light 0 plate on the box with the moss excluded and transferred
inside the moss region (full weight within FULL_PX of the strands, none at DECLARED_PX), so every other plate pixel stays
byte-identical; the base and light 1 plates follow from the cleaned light 0 plate through the plates' own linear gain
field, harmonically filled inside the region. The extraction donor, registered over the removal donor, gives the strands'
matte and colour, low-frequency matched to the plate; their base and light 1 versions carry the plates' own linear
ratio over the painted moss. Strands touching the spirit's face mask stay painted on the plate and do not sway. Each
strand sways horizontally by its tip sway times ((y - top) / length) ** 1.5; strands are spread over three sway
channels, and each side's deformation mesh holds one shape per channel (the moss's own nearest-strand field on a
GRID_PX grid), driven by a looping clip like CanopyFlutter.anim.

Mist: two transparent bands made to wrap by a premultiplied cross-fade of their ends, scaled to the frame; the mask
(where mist shows) is 1 minus the relight's receiver weight within MIST_ROWS, without specks and zero on the nymph and
the hiding places. The scene draws the bands unmasked and keeps the tree in front with an occluder: the background's
plates drawn above them through the inverted mask.

Creatures: each is painted into its scene by an in-scene donor (fixing its painted scale), extracted by a transparent
donor registered to it, colour-matched to it and resampled from the donor's resolution to its length; a creature that
would not read against the dark bark gets added moonlight. They move rigidly: a RawImage turned about
its centre, moved by an ambient Path motion (AmbientPathMotionAsset) through waypoints, facing its travel or keeping
its rest rotation. Hiding places are occluder polygons: the background's plates drawn through their mask above the
creatures, whose edge at the opening is where a creature appears; every pixel of a creature at its first and last
waypoint is inside its own hiding place. The paths are checked in 2 px steps: nothing visible on the playing surface,
the nymph, the face mask, the mist or another creature's hiding place. Every creature is built and placed; only those
marked in_group take turns in the ambient activity's creature group (placement's in_group).
"""
from pathlib import Path
import argparse
import importlib.util
import json
import math
import tempfile
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import (gaussian_filter, map_coordinates, distance_transform_edt, binary_erosion, binary_dilation,
                           label)
from scipy.optimize import minimize

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / 'Tools/Presentation'
ASSETS = ROOT / 'Dice-Unity/Assets/Dice'
ART = ASSETS / 'Art/BoundTree'
RELIGHT = ART / 'Relight'
CONTENT = ASSETS / 'Content/Presentation/BoundTree'
REF = ROOT / 'Docs/Reference/Art'
WORK = ROOT / 'Docs/work'
W, H = 941, 1672
# The encounter's Bound Tree Scene frame is 540 x 960 units over the concept's 941 x 1672 pixels.
FRAME = (540.0, 960.0)
SX, SY = FRAME[0] / W, FRAME[1] / H


def _load_module(name, file):
    spec = importlib.util.spec_from_file_location(name, TOOLS / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


MR = _load_module('author_merchant_relight', 'author-merchant-relight.py')
CMP = _load_module('cut_merchant_props', 'cut-merchant-props.py')
BT = _load_module('author_bound_tree_relight', 'author-bound-tree-relight.py')
A = _load_module('author_spirit_presentations', 'author-spirit-presentations.py')
to_linear, to_srgb = MR.to_linear, MR.to_srgb

CONCEPT = ART / 'BoundTreeOriginal.png'
BACKGROUND = ART / 'BoundTreeBackground.png'
FACE_MASK = ART / 'BoundTreeFaceMask.png'
NYMPH = ART / 'BoundTreeNymph.png'
# The background's plates (BoundTreeSceneLighting.asset before the moss is removed): base, light 0 and light 1 references.
PLATES = {'unlit': RELIGHT / 'BoundTreeFacelessPlateUnlit.png', 'glow': RELIGHT / 'BoundTreeFacelessPlateGlow.png',
          'glowlit': RELIGHT / 'BoundTreeFacelessPlateGlowLit.png'}
VERSION = {'unlit': 'Unlit', 'glow': 'Glow', 'glowlit': 'GlowLit'}

# ----------------------------------------------------------------------------------------------------------- moss
# Edit targets are Lanczos 1024 x 1536 upscales of these concept boxes (Docs/Reference/Art/BoundTreeMoss*Target.png).
MOSS = {
    'Left': {'box': (0, 40, 340, 550), 'extract': 'BoundTreeMossLeftExtractionDonor.png',
             'remove': 'BoundTreeMossLeftRemovalDonor.png'},
    'Right': {'box': (601, 0, 941, 510), 'extract': 'BoundTreeMossRightExtractionDonor.png',
              'remove': 'BoundTreeMossRightRemovalDonor.png'},
}
FULL_PX = 10.0        # removal at full weight within this distance of the moss: the largest sway (8 px) plus 2
DECLARED_PX = 16.0    # removal falls to zero here
ROUGH_DILATE = 14     # registration of the removal donor excludes the nominal moss dilated by this much
CONCEPT_MOSS_DIFF = 22.0   # plate pixels this far (luminance levels) from the matched clean donor count as moss too
CONCEPT_MOSS_NEAR = 30     # ... only within this distance of the extraction's moss
SWAY_FRACTION = 0.03  # tip sway, fraction of the strand length
SWAY_MIN, SWAY_MAX = 2.0, 8.0
SWAY_EXPONENT = 1.5   # displacement grows as ((y - top) / length) ** exponent
CHANNELS = 3          # strands are spread over this many sway channels, left to right
MIN_STRAND_PX = 120   # smaller components join their nearest strand
FACE_KEEP_PX = 4      # strands within this distance of the face mask (plus its 2 px guard) stay on the plate
SWAY_REACH = 14       # the sway field reaches pixels this far from a strand, so a strand's edge moves with it
GRID_PX = 4           # deformation mesh cell size in concept pixels

# ----------------------------------------------------------------------------------------------------------- mist
MIST_OVERLAP = 256    # donor columns cross-faded at the wrap
MIST = {
    # scale: frame px per donor px; centre_y: band centre row; opacity: the layer colour's alpha; speed: frame px per s
    'Far': {'donor': 'BoundTreeMistFarDonor.png', 'scale': 0.38, 'centre_y': 648, 'opacity': 0.35, 'speed': 4.0},
    'Near': {'donor': 'BoundTreeMistNearDonor.png', 'scale': 0.52, 'centre_y': 760, 'opacity': 0.45, 'speed': 7.0},
}
MIST_MIN_COMPONENT_PX = 600
MIST_ROWS = (470, 1060)   # mist never shows above or below these rows (soft 20 px edge)

# ------------------------------------------------------------------------------------------------------ creatures
PLAYING_SURFACE = [(0, 1092), (60, 1060), (130, 1035), (220, 1012), (320, 1000), (450, 996), (560, 998), (640, 1008),
                   (720, 1030), (790, 1062), (860, 1100), (941, 1145), (941, 1672), (0, 1672)]
ALPHA_LEVELS = (2.0, 250.0)
# Moonlight added to a creature so it reads against the dark bark and roots: linear-light gain toward the moon's colour.
MOONLIGHT = 0.35
MOON = ((804, 184), 60)   # moon disc centre and an inner radius, concept px
PATH_STEP = 6.0           # spacing of the waypoints that follow a curve, concept px (turns of a few degrees per leg)
SWEEP_STEP = 2.0          # check spacing along a path, concept px


def line(*points):
    return [tuple(map(float, p)) for p in points]


def arc(centre, radius, start, end, step=PATH_STEP):
    """Points on a circle from angle start to end (degrees, image axes: 0 right, 90 down), excluding the start."""
    length = math.radians(abs(end - start)) * radius
    count = max(1, int(round(length / step)))
    return [(centre[0] + radius * math.cos(math.radians(a)), centre[1] + radius * math.sin(math.radians(a)))
            for a in np.linspace(start, end, count + 1)[1:]]


# Each creature: in-scene donor box and donors, length along its body (concept px), the direction its head points in
# the sprite, and its course. Positions are the sprite's centre in concept px; the last point is its rest pose.
# `pause` maps a point index to seconds; `speed` is concept px per second. Hiding places are polygons in concept px,
# their edge at the opening following the painted lip. `moonlight` is the gain of the moonlight added to the creature
# (0 for none); `in_group` puts it in the ambient activity's creature group, so it takes turns.
CREATURES = {
    'Snake': {
        'box': (440, 760, 720, 1180), 'scene': 'BoundTreeSnakeSceneDonor.png', 'extract': 'BoundTreeSnakeExtractionDonor.png',
        'length': 170.0, 'axis': 'x', 'head': 'max', 'moonlight': MOONLIGHT,
        # At phone size the HUD's target panel covers the gap and the slide, so the snake takes no turns until it has a
        # hiding hollow in view.
        'in_group': False,
        # The head and neck slide straight out of the dark gap between the root columns, down to the right along the
        # big root, wait and slide straight back; the rest of the body stays in the gap.
        'mouth': (577.0, 914.0), 'direction': (0.84, 0.54), 'out': 84.0, 'in': 12.0, 'wait': 3.0, 'speed': 24.0,
        'keep_px': 100.0,
        'hide': [(590, 880), (598, 896), (594, 912), (584, 926), (572, 940), (556, 946), (500, 920), (456, 900),
                 (452, 862), (478, 830), (520, 828), (560, 846)],
    },
    'Beetle': {
        'box': (300, 560, 500, 860), 'scene': 'BoundTreeBeetleSceneDonor.png', 'extract': 'BoundTreeBeetleExtractionDonor.png',
        'length': 100.0, 'axis': 'y', 'head': 'min', 'speed': 20.0, 'moonlight': MOONLIGHT, 'in_group': True,
        # Facing down in the dark crevice, a turn out to the left onto the bark, a clockwise turn up the bark, a wait
        # facing up, back into the crevice above where it came out and a turn to face down again. The body is longer than
        # the crevice is wide, so it lies lengthwise in the crevice at both ends and turns there.
        'course': lambda: line((494, 752), (494, 758)) + arc((462, 758), 32, 0, 90) + line((420, 790))
        + arc((420, 762), 28, 90, 180) + arc((420, 762), 28, 180, 270) + line((466, 734)) + arc((466, 766), 32, -90, 0),
        'pause_at': 'leftmost', 'pause': 1.2,
        'hide': [(452, 700), (470, 692), (520, 692), (540, 706), (540, 836), (470, 838), (452, 828), (450, 780), (451, 750)],
    },
    'Centipede': {
        'box': (540, 560, 740, 860), 'scene': 'BoundTreeCentipedeSceneDonor.png', 'extract': 'BoundTreeCentipedeExtractionDonor.png',
        'length': 110.0, 'axis': 'y', 'head': 'min', 'speed': 26.0, 'moonlight': 0.0, 'in_group': True,
        # Up out of the crack from behind the knotted roots, a turn to the left over the bark and back down behind
        # the roots to the left of where it came out.
        'course': lambda: line((622, 806), (620, 704)) + arc((592, 704), 28, 0, -180) + line((564, 806)),
        'pause_at': None, 'pause': 0.0,
        'hide': [(528, 692), (548, 698), (566, 706), (584, 716), (602, 728), (622, 736), (642, 744), (664, 750),
                 (668, 870), (528, 870)],
    },
}

INSTALL_ASSETS = False
OUT = None


# =============================================================================================================== io
def load(path, mode='RGB'):
    return np.asarray(Image.open(path).convert(mode)).astype(np.float64)


def save(path, arr):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    a = np.clip(np.rint(arr), 0, 255).astype(np.uint8)
    mode = 'L' if a.ndim == 2 else ('RGBA' if a.shape[-1] == 4 else 'RGB')
    Image.fromarray(a, mode).save(path)
    return path


def rel(p):
    return Path(p).resolve().relative_to(ROOT).as_posix()


def font(size=14):
    for name in ('arial.ttf', 'DejaVuSans.ttf'):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def labelled(img, text, size=15):
    im = Image.fromarray(np.clip(np.rint(img), 0, 255).astype(np.uint8)).convert('RGB')
    out = Image.new('RGB', (im.width, im.height + size + 8), (20, 20, 20))
    out.paste(im, (0, size + 8))
    ImageDraw.Draw(out).text((4, 3), text, fill=(235, 235, 235), font=font(size))
    return np.asarray(out).astype(np.float64)


def row(images, gap=6, bg=20):
    h = max(i.shape[0] for i in images)
    out = np.full((h, sum(i.shape[1] for i in images) + gap * (len(images) - 1), 3), float(bg))
    x = 0
    for i in images:
        out[:i.shape[0], x:x + i.shape[1]] = i[..., :3]
        x += i.shape[1] + gap
    return out


def column(images, gap=6, bg=20):
    w = max(i.shape[1] for i in images)
    out = np.full((sum(i.shape[0] for i in images) + gap * (len(images) - 1), w, 3), float(bg))
    y = 0
    for i in images:
        out[y:y + i.shape[0], :i.shape[1]] = i[..., :3]
        y += i.shape[0] + gap
    return out


def crop(img, box, scale=1.0):
    x0, y0, x1, y1 = [int(v) for v in box]
    c = img[y0:y1, x0:x1]
    if scale == 1:
        return c
    im = Image.fromarray(np.clip(np.rint(c[..., :3]), 0, 255).astype(np.uint8))
    return np.asarray(im.resize((int((x1 - x0) * scale), int((y1 - y0) * scale)), Image.LANCZOS)).astype(np.float64)


def checker(h, w, cell=8):
    yy, xx = np.mgrid[0:h, 0:w]
    return np.repeat(np.where(((yy // cell) + (xx // cell)) % 2 == 0, 90.0, 140.0)[..., None], 3, -1)


def over(rgb, rgba):
    a = rgba[..., 3:4] / 255.0
    return rgba[..., :3] * a + rgb * (1 - a)


def sample(img, u, v, order=1):
    if img.ndim == 2:
        return map_coordinates(img, [v, u], order=order, mode='constant', cval=0.0)
    return np.stack([map_coordinates(img[..., c], [v, u], order=order, mode='constant', cval=0.0)
                     for c in range(img.shape[-1])], -1)


def box_mask(box, inset=0):
    m = np.zeros((H, W), bool)
    x0, y0, x1, y1 = box
    m[max(0, y0 + inset):min(H, y1 - inset), max(0, x0 + inset):min(W, x1 - inset)] = True
    return m


def alpha_levels(a, lo=2.0, hi=250.0):
    return np.clip((a - lo) / (hi - lo), 0, 1)


def poly_mask(poly):
    im = Image.new('L', (W * 4, H * 4), 0)
    ImageDraw.Draw(im).polygon([(x * 4, y * 4) for x, y in poly], fill=255)
    return np.asarray(im.resize((W, H), Image.BOX)).astype(np.float64) / 255.0


def lum(rgb):
    return rgb @ np.array([0.299, 0.587, 0.114])


def luma_linear(lin):
    return lin @ np.array([0.2126, 0.7152, 0.0722])


def nominal_map(box, donor_shape):
    """Donor pixel coordinates of every concept pixel under the edit target's own Lanczos upscale."""
    x0, y0, x1, y1 = box
    sx, sy = donor_shape[1] / (x1 - x0), donor_shape[0] / (y1 - y0)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float64)
    return (xx + 0.5 - x0) * sx - 0.5, (yy + 0.5 - y0) * sy - 0.5


# ========================================================================================================= register
def register(donor, src, region, box):
    """author-merchant-relight.register (affine NCC of locally contrast-normalised high-passed luminance, then a smooth
    residual from tile correlation peaks); when no tile qualifies for the residual the affine fit is used alone."""
    try:
        return MR.register(donor, src, np.where(region, 255.0, 0.0), box)
    except IndexError:
        pass
    x0, y0, x1, y1 = box
    scale = (donor.shape[1] / (x1 - x0), donor.shape[0] / (y1 - y0))
    s_struct = MR.structure(MR.luminance(src), 0, MR.REG_HIGHPASS)
    d_struct = MR.structure(MR.luminance(donor), 0.45 * np.mean(scale), MR.REG_HIGHPASS * np.mean(scale))
    inner = binary_erosion(region, iterations=6)
    bx = np.zeros_like(inner)
    bx[y0 + 8:y1 - 8, x0 + 8:x1 - 8] = True
    ys, xs = np.nonzero(inner & bx)
    ys, xs = ys[::3].astype(np.float64), xs[::3].astype(np.float64)
    sv = s_struct[ys.astype(int), xs.astype(int)]
    sv = (sv - sv.mean()) / sv.std()
    c = np.array([(x0 + x1) / 2, (y0 + y1) / 2])

    def mapping(p, X, Y):
        m00, m01, m10, m11, tx, ty = p
        dx, dy = X + 0.5 - c[0], Y + 0.5 - c[1]
        return m00 * dx + m01 * dy + tx - 0.5, m10 * dx + m11 * dy + ty - 0.5

    def ncc(p):
        u, v = mapping(p, xs, ys)
        dv = map_coordinates(d_struct, [v, u], order=1, mode='nearest')
        dv = (dv - dv.mean()) / (dv.std() + 1e-9)
        return float(np.mean(sv * dv))
    p0 = np.array([scale[0], 0, 0, scale[1], (c[0] - x0) * scale[0], (c[1] - y0) * scale[1]])
    best, best_score = p0, -2
    for ox in np.arange(-24, 25, 4):
        for oy in np.arange(-24, 25, 4):
            p = p0 + np.array([0, 0, 0, 0, ox, oy])
            s = ncc(p)
            if s > best_score:
                best, best_score = p, s
    res = minimize(lambda p: -ncc(p), best, method='Powell', options={'xtol': 1e-4, 'ftol': 1e-6, 'maxfev': 6000})
    yy, xx = np.mgrid[0:region.shape[0], 0:region.shape[1]].astype(np.float64)
    du, dv = mapping(res.x, xx, yy)
    return du, dv, {'donor_size': [int(donor.shape[1]), int(donor.shape[0])],
                    'affine_m00_m01_m10_m11_tx_ty': [float(v) for v in res.x], 'affine_center_native': c.tolist(),
                    'ncc_affine': -float(res.fun), 'local_tiles': 0, 'local_tiles_used': 0,
                    'note': 'no tile qualified for the local residual; affine only'}


# ============================================================================================================== moss
def unpremultiplied_spread(rgba, sigma=3.0, floor=0.05):
    a = rgba[..., 3] / 255.0
    rgb = rgba[..., :3]
    w = (a > floor).astype(float) * a
    spread = gaussian_filter(rgb * w[..., None], (sigma, sigma, 0)) / np.maximum(gaussian_filter(w, sigma), 1e-6)[..., None]
    return np.where((a > floor)[..., None], rgb, spread)


def moss_side(name, spec, P, face_block, report):
    box = spec['box']
    E = load(REF / spec['extract'], 'RGBA')
    R = load(REF / spec['remove'], 'RGB')
    src = P['glow']
    inbox = box_mask(box, 4)
    nu, nv = nominal_map(box, E.shape)
    a0 = sample(E[..., 3], nu, nv) * inbox
    rough = binary_dilation(a0 > 20, iterations=ROUGH_DILATE)
    # removal donor: fitted on the box with the moss excluded
    du, dv, rep_r = register(R, src, box_mask(box, 8) & ~rough, box)
    rep_r['donor'] = rel(REF / spec['remove'])
    Rreg = sample(R, du, dv)
    # extraction donor over the removal donor in donor space, fitted on the moss
    Ea = E[..., 3:4] / 255.0
    ER = E[..., :3] * Ea + R * (1 - Ea)
    eu, ev, rep_e = register(ER, src, binary_dilation(a0 > 128, iterations=8) & box_mask(box, 8), box)
    rep_e['donor'] = rel(REF / spec['extract'])
    pm = E.copy()
    pm[..., :3] *= Ea
    s = sample(pm, eu, ev) * inbox[..., None]
    a_raw = s[..., 3]
    rgb = s[..., :3] / np.maximum(a_raw / 255.0, 1e-6)[..., None]
    alpha = alpha_levels(a_raw)
    colour = unpremultiplied_spread(np.concatenate([np.clip(rgb, 0, 255), (alpha * 255)[..., None]], -1))
    sil = alpha >= 0.02
    # colour matched to the light 0 plate on the visible moss (the nymph's rule)
    inner = binary_erosion(alpha >= 0.5, iterations=1)
    m24 = CMP.lowfreq_match(colour, src, inner, sigmas=(24,), detail=False)
    visible = inner & (np.abs(m24 - src).max(-1) < 40)
    colour = np.clip(CMP.lowfreq_match(colour, src, visible, sigmas=(24, 10, 4), detail=False), 0, 255)
    # strands: connected components, small ones joined to the nearest strand
    lab, n = label(binary_dilation(sil, iterations=2))
    lab = lab * sil
    sizes = np.bincount(lab.ravel(), minlength=n + 1)
    big = [i for i in range(1, n + 1) if sizes[i] >= MIN_STRAND_PX]
    if big:
        keep = np.isin(lab, big)
        _, (iy, ix) = distance_transform_edt(~keep, return_indices=True)
        lab = np.where(sil, lab[iy, ix], 0)
    # strands touching the face mask stay painted: the face layer would keep showing them
    near_face = binary_dilation(face_block, iterations=FACE_KEEP_PX)
    kept_on_plate = sorted({int(i) for i in np.unique(lab[near_face & sil]) if i})
    big = [i for i in big if i not in kept_on_plate]
    static = np.isin(lab, kept_on_plate) & sil
    static_alpha = np.where(static, alpha, 0.0)
    alpha = np.where(static, 0.0, alpha)
    sil = sil & ~static
    lab = np.where(static, 0, lab)
    visible = visible & ~static
    strands = []
    for k, i in enumerate(sorted(big, key=lambda i: np.nonzero(lab == i)[1].mean())):
        ys, xs = np.nonzero(lab == i)
        top, bottom = int(ys.min()), int(ys.max())
        length = bottom - top + 1
        strands.append({'id': k + 1, 'label': int(i), 'channel': k % CHANNELS, 'top_y': top, 'bottom_y': bottom,
                        'x_range': [int(xs.min()), int(xs.max())], 'attach_x': float(xs[ys <= top + 3].mean()),
                        'length_px': length, 'tip_sway_px': round(float(np.clip(SWAY_FRACTION * length, SWAY_MIN, SWAY_MAX)), 2),
                        'pixels': int(len(ys))})
    # plate moss the extraction missed: far from the matched clean donor, near the extraction's moss
    d_sil = distance_transform_edt(~sil)
    Rm = CMP.lowfreq_match(Rreg, src, box_mask(box, 8) & (d_sil > DECLARED_PX + 4), sigmas=(4, 10, 24), detail=True)
    concept_moss = (np.abs(lum(src) - lum(Rm)) > CONCEPT_MOSS_DIFF) & (d_sil < CONCEPT_MOSS_NEAR) & inbox
    concept_moss = binary_erosion(binary_dilation(concept_moss, iterations=1), iterations=1)
    concept_moss &= ~binary_dilation(static, iterations=6)
    covered = sil | concept_moss
    dist = distance_transform_edt(~covered)
    w = np.clip((DECLARED_PX - dist) / (DECLARED_PX - FULL_PX), 0, 1) * box_mask(box, 2)
    w[face_block] = 0
    report[name] = {
        'box_xyxy': list(box), 'registration_removal': rep_r, 'registration_extraction': rep_e,
        'moss_pixels': int(sil.sum()), 'plate_moss_added_pixels': int((concept_moss & ~sil).sum()),
        'region_pixels': int((w > 0).sum()), 'covered_pixels_inside_face_mask_not_removed': int((covered & face_block).sum()),
        'declared_band_pixels_outside_box': int((binary_dilation(covered, iterations=int(DECLARED_PX)) & ~box_mask(box, 2)).sum()),
        'strands_left_on_plate_near_face': len(kept_on_plate), 'strand_pixels_left_on_plate': int(static.sum()),
        'visible_match_pixels': int(visible.sum()), 'strands': strands}
    return {'alpha': alpha, 'colour': colour, 'w': w, 'Rm': Rm, 'lab': lab, 'strands': strands, 'visible': visible,
            'box': box, 'static_alpha': static_alpha}


def sway_fields(side):
    """Per-channel horizontal displacement (px, right positive) at weight 1 over the frame: every pixel within
    SWAY_REACH of a strand takes its nearest strand's displacement, in that strand's channel only."""
    lab = side['lab']
    yy = np.arange(H)[:, None].repeat(W, 1).astype(float)
    disp = np.zeros((H, W))
    chan = np.full((H, W), -1)
    for st in side['strands']:
        m = lab == st['label']
        t = np.clip((yy - st['top_y']) / max(1, st['length_px']), 0, 1) ** SWAY_EXPONENT
        disp[m] = st['tip_sway_px'] * t[m]
        chan[m] = st['channel']
    dist, (iy, ix) = distance_transform_edt(lab == 0, return_indices=True)
    near = dist <= SWAY_REACH
    disp, chan = np.where(near, disp[iy, ix], 0.0), np.where(near, chan[iy, ix], -1)
    return [np.where(chan == k, disp, 0.0) for k in range(CHANNELS)]


def moss_mesh(side, rect):
    """Grid and per-vertex offsets (concept px, right positive) of each channel over the layer's rect."""
    x0, y0, w, h = rect
    cols, rows = max(1, int(round(w / GRID_PX))), max(1, int(round(h / GRID_PX)))
    fields = sway_fields(side)
    # vertex (column, row) sits at u = column / cols, v = row / rows (bottom-left), concept point (x0 + u w, y0 + (1 - v) h)
    vx = x0 + np.arange(cols + 1) / cols * w
    vy = y0 + (1 - np.arange(rows + 1) / rows) * h
    gx, gy = np.meshgrid(vx, vy)
    shapes = [map_coordinates(f, [gy.ravel() - 0.5, gx.ravel() - 0.5], order=1, mode='nearest').reshape(gy.shape)
              for f in fields]
    return cols, rows, shapes


def mesh_displacement(cols, rows, shape, rect, xs, ys):
    """Displacement the rendered grid gives concept points (bilinear between the cell's vertices)."""
    x0, y0, w, h = rect
    gx = np.clip((xs - x0) / w * cols, 0, cols - 1e-6)
    gy = np.clip((1 - (ys - y0) / h) * rows, 0, rows - 1e-6)
    return map_coordinates(shape, [gy, gx], order=1, mode='nearest')


def build_moss(P, report):
    face = load(FACE_MASK, 'RGBA')[..., 3] > 0
    face_block = binary_dilation(face, iterations=2)
    rep = report.setdefault('moss', {})
    sides = {k: moss_side(k, v, P, face_block, rep) for k, v in MOSS.items()}
    # cleaned light 0 plate, then base and light 1 through the plates' gain field
    w_all = np.zeros((H, W))
    clean = {'glow': P['glow'].copy()}
    for s in sides.values():
        ww = s['w'][..., None]
        clean['glow'] = clean['glow'] * (1 - ww) + s['Rm'] * ww
        w_all = np.maximum(w_all, s['w'])
    region = w_all > 0
    lin_glow = to_linear(P['glow'])
    lin_clean = to_linear(clean['glow'])
    for k in ('unlit', 'glowlit'):
        g = np.clip(to_linear(P[k]) / np.maximum(lin_glow, 1e-5), 0.05, 20)
        g = MR.membrane_fill(np.where(region[..., None], 1.0, g), region)
        c = P[k].copy()
        c[region] = to_srgb(lin_clean * g)[region]
        clean[k] = c
    plates, identical = {}, {}
    for k in VERSION:
        c = np.where(region[..., None], np.clip(np.rint(clean[k]), 0, 255), P[k])
        plates[k] = c
        identical[k] = bool(np.array_equal(c[~region], P[k][~region]))
    rep['plates'] = {'region_pixels': int(region.sum()), 'byte_identical_outside_region': identical}
    layers = {}
    for name, s in sides.items():
        alpha, vis = s['alpha'], s['visible']
        lin_c = to_linear(s['colour'])
        versions = {'glow': lin_c}
        for k in ('unlit', 'glowlit'):
            num, _ = CMP.nblur(to_linear(P[k]), vis.astype(float), 3.0)
            ref, _ = CMP.nblur(lin_glow, vis.astype(float), 3.0)
            versions[k] = lin_c * CMP.extrapolate(np.clip(num / np.maximum(ref, 1e-6), 0.05, 20), vis, (3, 12, 40))
        versions['glow'] = np.maximum(versions['glow'], versions['unlit'])
        versions['glowlit'] = np.maximum(versions['glowlit'], versions['unlit'])
        ys, xs = np.nonzero(alpha > 0)
        x0, y0 = max(0, xs.min() - 3), max(0, ys.min() - 3)
        x1, y1 = min(W, xs.max() + 4), min(H, ys.max() + 4)
        rect = (int(x0), int(y0), int(x1 - x0), int(y1 - y0))
        full = {k: np.concatenate([to_srgb(lin), alpha[..., None] * 255], -1) for k, lin in versions.items()}
        cols, rows, shapes = moss_mesh(s, rect)
        # at-rest composite error against the plate on the light 0 version
        d = np.abs(over(plates['glow'], full['glow']) - P['glow']).max(-1)[s['w'] > 0]
        strand_px = np.nonzero(s['alpha'] > 0.02)
        largest = 0.0
        for shape in shapes:
            largest = max(largest, float(np.abs(mesh_displacement(cols, rows, shape, rect, strand_px[1] + 0.5,
                                                                  strand_px[0] + 0.5)).max()))
        rep[name].update({
            'crop_rect_xywh': list(rect), 'map_rect': map_rect(rect),
            'rest_error_levels': {'mean': float(d.mean()), 'p95': float(np.percentile(d, 95)), 'max': float(d.max()),
                                  'pixels_over_24': int((d > 24).sum()), 'region_pixels': int(d.size)},
            'mesh': {'grid': [cols, rows], 'largest_vertex_displacement_px': [float(np.abs(sh).max()) for sh in shapes],
                     'largest_displacement_on_strand_pixels_px': largest,
                     'full_weight_removal_px': FULL_PX}})
        layers[name] = {'full': full, 'rect': rect, 'mesh': (cols, rows, shapes), 'side': s}
    static = np.maximum.reduce([s['static_alpha'] for s in sides.values()])
    return plates, layers, static


# ============================================================================================================== mist
def tile_band(path):
    D = load(path, 'RGBA')
    a = alpha_levels(D[..., 3])
    rgb = D[..., :3]
    w = (a > 0.02) * a
    spread = gaussian_filter(rgb * w[..., None], (3, 3, 0)) / np.maximum(gaussian_filter(w, 3), 1e-6)[..., None]
    rgb = np.where((a > 0.02)[..., None], rgb, spread)
    pm = np.concatenate([rgb * a[..., None], a[..., None]], -1)
    n = D.shape[1] - MIST_OVERLAP
    t = pm[:, :n].copy()
    ramp = (np.arange(MIST_OVERLAP) + 0.5) / MIST_OVERLAP
    t[:, :MIST_OVERLAP] = pm[:, :MIST_OVERLAP] * ramp[None, :, None] + pm[:, n:n + MIST_OVERLAP] * (1 - ramp[None, :, None])
    rows = np.nonzero(t[..., 3].max(1) > 0.004)[0]
    return t[max(0, rows.min() - 4):rows.max() + 5]


def resize_pm(pm, scale):
    h, w = pm.shape[:2]
    nw, nh = int(round(w * scale)), int(round(h * scale))
    ch = [np.asarray(Image.fromarray(pm[..., c].astype(np.float32), 'F').resize((nw, nh), Image.LANCZOS)) for c in range(4)]
    out = np.stack(ch, -1).astype(np.float64)
    out[..., 3] = np.clip(out[..., 3], 0, 1)
    out[..., :3] = np.clip(out[..., :3], 0, 255)
    return out


def straight(pm):
    a = pm[..., 3]
    return np.concatenate([np.clip(pm[..., :3] / np.maximum(a, 1e-6)[..., None], 0, 255), (a * 255)[..., None]], -1)


def place_band(tile_pm, centre_y, offset_x, opacity):
    """Premultiplied RGB and alpha over the frame of a wrapping band, its tile origin at offset_x."""
    th, tw = tile_pm.shape[:2]
    rgb, a = np.zeros((H, W, 3)), np.zeros((H, W))
    y0 = int(round(centre_y - th / 2))
    cols = (np.arange(W) - offset_x) % tw
    ci = np.floor(cols).astype(int)
    fr = (cols - ci)[None, :, None]
    strip = tile_pm[:, ci] * (1 - fr) + tile_pm[:, (ci + 1) % tw] * fr
    ys = slice(max(0, y0), min(H, y0 + th))
    sy = slice(ys.start - y0, ys.stop - y0)
    rgb[ys] = strip[sy, :, :3] * opacity
    a[ys] = strip[sy, :, 3] * opacity
    return rgb, a


def build_mist(P, hides, report):
    rep = report.setdefault('mist', {'bands': {}})
    tiles = {}
    for name, b in MIST.items():
        t = tile_band(REF / b['donor'])
        tf = resize_pm(t, b['scale'])
        th, tw = tf.shape[:2]
        tiles[name] = tf
        y0 = b['centre_y'] - th / 2
        rep['bands'][name] = dict(b, tile_size_px=[tw, th], rect_xywh=[0, y0, W, th],
                                  wrap_alpha_jump=float(np.abs(tf[:, -1, 3] - tf[:, 0, 3]).max()),
                                  largest_inner_alpha_step=float(np.abs(np.diff(tf[..., 3], axis=1)).max()),
                                  uv_width=W / tw, drift_period_s=tw / b['speed'])
    rec = BT.receivers(P['unlit'])
    yy = np.arange(H)[:, None].repeat(W, 1).astype(float)
    rows = np.clip((yy - MIST_ROWS[0]) / 20, 0, 1) * np.clip((MIST_ROWS[1] - yy) / 20, 0, 1)
    nymph = load(NYMPH, 'RGBA')[..., 3] / 255.0
    mask = np.clip(1 - rec, 0, 1) * rows * (1 - np.clip(gaussian_filter(nymph, 1.0) * 1.5, 0, 1))
    # drop specks (dark bark whose blue-to-red ratio reads as distant swamp), grown back 3 px for their soft edge
    lab, n = label(mask > 0.5)
    sizes = np.bincount(lab.ravel())
    keep = np.isin(lab, [i for i in range(1, n + 1) if sizes[i] >= MIST_MIN_COMPONENT_PX])
    mask = mask * gaussian_filter(binary_dilation(keep, iterations=3).astype(float), 1.0)
    # no mist inside the hiding places: their occluder draws the plate without mist above the creatures
    mask = mask * (1 - np.clip(gaussian_filter(binary_dilation(hides > 0, iterations=2).astype(float), 1.0) * 1.5, 0, 1))
    lu, lg, lgl = to_linear(P['unlit']), to_linear(P['glow']), to_linear(P['glowlit'])
    m = mask > 0.5
    rep['mask'] = {'rows': list(MIST_ROWS), 'pixels_over_half': int(m.sum()),
                   'light1_over_base_in_mask_luma_p99': float(np.percentile(luma_linear((lgl + 1e-4) / (lu + 1e-4))[m], 99)),
                   'light0_over_base_in_mask_luma_p99': float(np.percentile(luma_linear((lg + 1e-4) / (lu + 1e-4))[m], 99))}
    return tiles, mask


# ========================================================================================================= creatures
def prealign(X, S, step=4):
    """Similarity pre-alignment of an extraction X (RGBA) to its in-scene donor S by masked normalised
    cross-correlation of high-passed luminance over a sweep of scales; returns X resampled into S's grid."""
    from scipy.signal import fftconvolve
    lx = MR.structure(MR.luminance(X[..., :3]), 1.0, 6.0)
    ls = MR.structure(MR.luminance(S), 1.0, 6.0)
    Ss = np.asarray(Image.fromarray(ls.astype(np.float32), 'F').resize((S.shape[1] // step, S.shape[0] // step), Image.BOX)).astype(np.float64)
    m = binary_erosion(X[..., 3] > 250, iterations=3).astype(np.float32)
    ay, ax = np.nonzero(m)
    x0, x1, y0, y1 = ax.min(), ax.max() + 1, ay.min(), ay.max() + 1
    best = (-2, None)
    for sc in np.exp(np.linspace(np.log(0.7), np.log(2.8), 48)):     # X px per S px
        f = 1.0 / (sc * step)
        w, h = max(4, int(round((x1 - x0) * f))), max(4, int(round((y1 - y0) * f)))
        t = np.asarray(Image.fromarray(lx[y0:y1, x0:x1].astype(np.float32), 'F').resize((w, h), Image.BOX)).astype(np.float64)
        k = (np.asarray(Image.fromarray(m[y0:y1, x0:x1], 'F').resize((w, h), Image.BOX)).astype(np.float64) > 0.5).astype(np.float64)
        if k.sum() < 30 or w >= Ss.shape[1] or h >= Ss.shape[0]:
            continue
        tm = (t - (t * k).sum() / k.sum()) * k
        tm /= np.sqrt((tm * tm).sum()) + 1e-9
        num = fftconvolve(Ss, tm[::-1, ::-1], mode='valid')
        e1 = fftconvolve(Ss, k[::-1, ::-1], mode='valid')
        e2 = fftconvolve(Ss * Ss, k[::-1, ::-1], mode='valid')
        score = num / np.sqrt(np.maximum(e2 - e1 * e1 / k.sum(), 1e-9))
        iy, ix = np.unravel_index(np.argmax(score), score.shape)
        if score[iy, ix] > best[0]:
            best = (float(score[iy, ix]), (sc, ix * step, iy * step))
    sc, ox, oy = best[1]
    gy, gx = np.mgrid[0:S.shape[0], 0:S.shape[1]].astype(np.float64)
    return sample(X, x0 + (gx - ox) * sc, y0 + (gy - oy) * sc), {
        'scale_extraction_px_per_scene_px': float(sc), 'offset_scene_px': [float(ox), float(oy)],
        'extraction_crop_origin': [int(x0), int(y0)], 'ncc_prealign': best[0]}


def build_sprite(name, spec, concept, report):
    """The creature at its length in concept px: straight RGBA (colour bled under transparent texels)."""
    box = spec['box']
    S = load(REF / spec['scene'], 'RGB')
    X = load(REF / spec['extract'], 'RGBA')
    scale = S.shape[1] / (box[2] - box[0])
    # 1. in-scene donor to the concept, creature excluded: it fixes the painted scale
    nu, nv = nominal_map(box, S.shape)
    rough = sample(X[..., 3], nu, nv) > 10
    du, dv, rep_s = register(S, concept, box_mask(box, 8) & ~binary_dilation(rough, iterations=14), box)
    rep_s['donor'] = rel(REF / spec['scene'])
    M = np.array(rep_s['affine_m00_m01_m10_m11_tx_ty'][:4]).reshape(2, 2)
    frame_per_scene_px = 1.0 / np.sqrt(abs(np.linalg.det(M)))
    # 2. extraction to the in-scene donor on the creature's interior
    X, pre = prealign(X, S)
    Xa = X[..., 3] / 255.0
    XS = X[..., :3] * Xa[..., None] + S * (1 - Xa[..., None])
    xu, xv, rep_x = register(XS, S, X[..., 3] > 250, (0, 0, S.shape[1], S.shape[0]))
    rep_x['donor'] = rel(REF / spec['extract'])
    rep_x['pre_alignment'] = pre
    pm = X.copy()
    pm[..., :3] *= Xa[..., None]
    Xr = sample(pm, xu, xv)
    a_raw = Xr[..., 3]
    rgb = Xr[..., :3] / np.maximum(a_raw / 255.0, 1e-6)[..., None]
    alpha = alpha_levels(a_raw, *ALPHA_LEVELS)
    w = (alpha > 0.05) * alpha
    spread = gaussian_filter(rgb * w[..., None], (3 * scale, 3 * scale, 0)) / np.maximum(gaussian_filter(w, 3 * scale), 1e-6)[..., None]
    colour = np.where((alpha > 0.05)[..., None], rgb, spread)
    inner = binary_erosion(alpha >= 0.5, iterations=int(round(scale)))
    m24 = CMP.lowfreq_match(colour, S, inner, sigmas=(24 * scale,), detail=False)
    visible = inner & (np.abs(m24 - S).max(-1) < 40)
    colour = np.clip(CMP.lowfreq_match(colour, S, visible, sigmas=tuple(s * scale for s in (24, 10, 4)), detail=False), 0, 255)
    match_err = np.abs(colour - S).max(-1)[visible]
    # 3. resampled (premultiplied Lanczos) from the donor's resolution to the creature's length
    ys, xs = np.nonzero(alpha > 0.02)
    x0, x1, y0, y1 = xs.min() - 4, xs.max() + 5, ys.min() - 4, ys.max() + 5
    painted = ((xs.max() - xs.min()) if spec['axis'] == 'x' else (ys.max() - ys.min())) * frame_per_scene_px
    f = frame_per_scene_px * spec['length'] / painted     # sprite px per in-scene donor px
    crop_pm = np.concatenate([colour * alpha[..., None], alpha[..., None] * 255], -1)[y0:y1, x0:x1]
    sw, sh = max(1, int(round((x1 - x0) * f))), max(1, int(round((y1 - y0) * f)))
    spm = np.clip(np.stack([np.asarray(Image.fromarray(crop_pm[..., c].astype(np.float32), 'F').resize((sw, sh), Image.LANCZOS))
                            for c in range(4)], -1), 0, 255)
    sa = spm[..., 3] / 255.0
    srgb = spm[..., :3] / np.maximum(sa, 1e-6)[..., None]
    known = sa > 0.02
    srgb = CMP.bleed(np.where(known[..., None], srgb, 0), known, iters=6)
    sprite = np.concatenate([np.clip(srgb, 0, 255), (np.clip(sa, 0, 1) * 255)[..., None]], -1)
    sprite[sa <= 0.004, 3] = 0
    report[name] = {'registration_scene_to_concept': rep_s, 'registration_extraction_to_scene': rep_x,
                    'frame_px_per_scene_px': float(frame_per_scene_px), 'painted_length_px': float(painted),
                    'donor_px_per_sprite_px': float(1 / f),
                    'length_px': spec['length'], 'scale_from_painted': spec['length'] / painted,
                    'colour_match_error_levels': {'p50': float(np.median(match_err)), 'p95': float(np.percentile(match_err, 95))}}
    return sprite


def head_and_axis(sprite, spec, reach=60.0):
    """The head tip (sprite px) and the unit direction of the last `reach` px of the body toward it."""
    a = sprite[..., 3] / 255.0 > 0.5
    if spec['axis'] == 'x':
        cols = np.nonzero(a.any(0))[0]
        cy = np.array([np.nonzero(a[:, c])[0].mean() for c in cols])
        pts = np.stack([cols + 0.5, gaussian_filter(cy, 3, mode='nearest') + 0.5], -1)
    else:
        rws = np.nonzero(a.any(1))[0]
        cx = np.array([np.nonzero(a[r])[0].mean() for r in rws])
        pts = np.stack([gaussian_filter(cx, 3, mode='nearest') + 0.5, rws + 0.5], -1)
    if spec['head'] == 'min':
        pts = pts[::-1]
    tip = pts[-1]
    s = np.r_[0, np.cumsum(np.hypot(*np.diff(pts, axis=0).T))]
    back = pts[np.searchsorted(s, s[-1] - reach)]
    d = tip - back
    return tip, d / np.linalg.norm(d)


def up(v):
    """Image vector (y down) to the frame's (y up)."""
    return np.array([v[0], -v[1]])


def rotate_up(v, degrees):
    c, s = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
    return np.array([c * v[0] - s * v[1], s * v[0] + c * v[1]])


def to_frame(p):
    """Concept px (top-left origin) to the frame's anchored position from its top-left corner (units, y up)."""
    return np.array([p[0] * SX, -p[1] * SY])


def course(name, spec, sprite):
    """Rest pose (centre, rotation) and waypoints (concept px centres with pauses) of the creature's path."""
    h, w = sprite.shape[:2]
    centre = np.array([w / 2.0, h / 2.0])
    if name == 'Snake':
        tip, axis = head_and_axis(sprite, spec)
        d = np.array(spec['direction'], float)
        d /= np.linalg.norm(d)
        # Turn the sprite so its neck runs along the slide; the head tip then sits at the mouth plus the offset.
        rotation = math.degrees(math.atan2(*up(d)[::-1]) - math.atan2(*up(axis)[::-1]))
        tip_from_centre = rotate_up(up(tip - centre), rotation)
        mouth = np.array(spec['mouth'], float)

        def centre_at(head):
            return head - np.array([tip_from_centre[0], -tip_from_centre[1]])
        rest = centre_at(mouth - spec['in'] * d)
        out = centre_at(mouth + spec['out'] * d)
        points = [tuple(rest), tuple(out), tuple(rest)]
        pauses = [0.0, spec['wait'], 0.0]
        return {'rest': rest, 'rotation': rotation, 'points': points, 'pauses': pauses, 'face_travel': False,
                'facing_offset': 0.0, 'speed': spec['speed']}
    pts = spec['course']()
    pauses = [0.0] * len(pts)
    if spec['pause_at'] == 'leftmost':
        pauses[int(np.argmin([p[0] for p in pts]))] = spec['pause']
    # art drawn facing up (heading 90 degrees) takes a facing offset of -90
    return {'rest': np.array(pts[-1]), 'rotation': 0.0, 'points': pts, 'pauses': pauses, 'face_travel': True,
            'facing_offset': -90.0, 'speed': spec['speed']}


def waypoints_frame(path):
    """Waypoint offsets in frame units from the rest anchored position, as the Path motion asset holds them."""
    rest = to_frame(path['rest'])
    return [(to_frame(p) - rest, pause) for p, pause in zip(path['points'], path['pauses'])]


def evaluate(path, elapsed):
    """AmbientPathMotion.Evaluate in frame units: (centre in concept px, rotation in degrees) at elapsed seconds."""
    wps = waypoints_frame(path)
    speed = path['speed'] * SX
    heading = None
    for i in range(len(wps) - 1):
        if np.linalg.norm(wps[i + 1][0] - wps[i][0]) > 0:
            dvec = wps[i + 1][0] - wps[i][0]
            heading = math.degrees(math.atan2(dvec[1], dvec[0]))
            break
    position = wps[-1][0]
    for i, (p, pause) in enumerate(wps):
        if elapsed < pause:
            position = p
            break
        elapsed -= pause
        if i + 1 == len(wps):
            break
        q = wps[i + 1][0]
        length = float(np.linalg.norm(q - p))
        if length > 0:
            heading = math.degrees(math.atan2(q[1] - p[1], q[0] - p[0]))
        travel = length / speed
        if elapsed < travel:
            position = p + (q - p) * (elapsed / travel)
            break
        elapsed -= travel
    angle = path['rotation'] + (heading + path['facing_offset'] if path['face_travel'] else 0.0)
    frame = to_frame(path['rest']) + position
    return np.array([frame[0] / SX, -frame[1] / SY]), angle


def duration(path):
    wps = waypoints_frame(path)
    speed = path['speed'] * SX
    return sum(p for _, p in wps) + sum(float(np.linalg.norm(wps[i + 1][0] - wps[i][0])) / speed for i in range(len(wps) - 1))


def render(sprite, centre, angle):
    """Premultiplied RGB, alpha and the box (x0, y0) of the sprite turned by angle (degrees, counter-clockwise on screen)
    about its centre placed at `centre` (concept px)."""
    h, w = sprite.shape[:2]
    r = 0.5 * math.hypot(w, h) + 2
    x0, y0 = int(math.floor(centre[0] - r)), int(math.floor(centre[1] - r))
    x1, y1 = int(math.ceil(centre[0] + r)), int(math.ceil(centre[1] + r))
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(float) + 0.5
    dx, dy = xx - centre[0], -(yy - centre[1])
    c, s = math.cos(math.radians(-angle)), math.sin(math.radians(-angle))
    lx, ly = c * dx - s * dy, s * dx + c * dy
    u, v = lx + w / 2.0 - 0.5, -ly + h / 2.0 - 0.5
    pm = sprite.copy()
    pm[..., 3] /= 255.0
    pm[..., :3] *= pm[..., 3:4]
    out = sample(pm, u, v)
    return out[..., :3], out[..., 3], (x0, y0)


def paste(shape, values, origin):
    """Full-frame array holding `values` at origin (x0, y0), clipped to the frame."""
    full = np.zeros((H, W) + values.shape[2:])
    x0, y0 = origin
    h, w = values.shape[:2]
    fx0, fy0, fx1, fy1 = max(0, x0), max(0, y0), min(W, x0 + w), min(H, y0 + h)
    if fx1 > fx0 and fy1 > fy0:
        full[fy0:fy1, fx0:fx1] = values[fy0 - y0:fy1 - y0, fx0 - x0:fx1 - x0]
    return full


def add_moonlight(sprite, P, gain):
    """Adds moonlight to a creature: its linear colour times 1 + gain * the moon's colour (luma 1)."""
    (mx, my), mr = MOON
    yy, xx = np.mgrid[0:H, 0:W]
    disc = np.hypot(xx - mx, yy - my) < mr
    moon = to_linear(P['unlit'])[disc].mean(0)
    moon = moon / luma_linear(moon)
    lin = to_linear(sprite[..., :3]) * (1 + gain * moon)
    return np.concatenate([np.clip(to_srgb(lin), 0, 255), sprite[..., 3:]], -1), moon


def build_creatures(P, report):
    concept = load(CONCEPT)
    rep = report.setdefault('creatures', {'playing_surface_polygon': PLAYING_SURFACE})
    lgl, lu, lg = to_linear(P['glowlit']), to_linear(P['unlit']), to_linear(P['glow'])
    rec = BT.receivers(P['unlit'])
    # ratios of locally averaged linear light on receivers, so dark cracks do not divide by zero
    gain = CMP.nblur(lgl, rec, 6.0)[0] / np.maximum(CMP.nblur(lu, rec, 6.0)[0], 1e-4)          # light 1 ref / base
    gain0 = CMP.nblur(lg, rec, 6.0)[0] / np.maximum(CMP.nblur(lu, rec, 6.0)[0], 1e-4)          # light 0 ref / base
    out = {}
    for name, spec in CREATURES.items():
        sprite = build_sprite(name, spec, concept, rep)
        if spec['moonlight'] > 0:
            sprite, moon = add_moonlight(sprite, P, spec['moonlight'])
            rep[name]['moonlight'] = {'gain': spec['moonlight'], 'moon_linear_rgb_luma_1': moon.tolist()}
        rep[name]['in_group'] = spec['in_group']
        if name == 'Snake':
            # Only the front of the body ever leaves the gap; the rest is cropped so the hiding place stays small.
            tip, axis = head_and_axis(sprite, spec)
            hh, ww = sprite.shape[:2]
            yy, xx = np.mgrid[0:hh, 0:ww].astype(float) + 0.5
            behind = (tip[0] - xx) * axis[0] + (tip[1] - yy) * axis[1]
            sprite[..., 3] *= np.clip((spec['keep_px'] - behind) / 4.0, 0, 1)
            ys, xs = np.nonzero(sprite[..., 3] > 0)
            sprite = sprite[max(0, ys.min() - 2):ys.max() + 3, max(0, xs.min() - 2):xs.max() + 3]
        path = course(name, spec, sprite)
        out[name] = {'sprite': sprite, 'path': path, 'hide': poly_mask(spec['hide'])}
        rep[name]['sprite_size'] = [int(sprite.shape[1]), int(sprite.shape[0])]
    return out, gain, gain0


def check_creatures(creatures, gain, gain0, mist_mask, report):
    rep = report['creatures']
    surface = poly_mask(PLAYING_SURFACE) > 0.5
    nymph = load(NYMPH, 'RGBA')[..., 3] / 255.0
    face = load(FACE_MASK, 'RGBA')[..., 3] / 255.0
    hides = {n: c['hide'] for n, c in creatures.items()}
    for name, c in creatures.items():
        sprite, path, hide = c['sprite'], c['path'], c['hide']
        others = np.maximum.reduce([h for n, h in hides.items() if n != name])
        T = duration(path)
        speed = path['speed'] * SX
        times = np.arange(0.0, T + 1e-9, SWEEP_STEP * SX / speed)
        worst = {'surface': 0, 'nymph': 0, 'face': 0, 'mist': 0, 'other_hiding_place': 0}
        visible_alpha, centres, gains = [], [], []
        for t in times:
            centre, angle = evaluate(path, t)
            _, a, (x0, y0) = render(sprite, centre, angle)
            a = paste(None, a, (x0, y0))
            vis = a * (1 - hide)
            v = vis > 0.1
            worst['surface'] = max(worst['surface'], int((v & surface).sum()))
            worst['nymph'] = max(worst['nymph'], int((v & (nymph > 0.1)).sum()))
            worst['face'] = max(worst['face'], int((v & (face > 0)).sum()))
            worst['mist'] = max(worst['mist'], int((v & (mist_mask > 0.1)).sum()))
            worst['other_hiding_place'] = max(worst['other_hiding_place'], int(((vis * others) > 0.1).sum()))
            visible_alpha.append(float(vis.sum()))
            if vis.sum() > 1:
                cy, cx = int(round(centre[1])), int(round(centre[0]))
                gains.append((gain[cy, cx], gain0[cy, cx]))
            centres.append(centre)
        visible_alpha = np.array(visible_alpha)
        full = visible_alpha.max()
        # capture moments: half emerged, the middle of the run, half hidden again
        out_idx = int(np.argmax(visible_alpha >= 0.5 * full))
        in_idx = len(visible_alpha) - 1 - int(np.argmax(visible_alpha[::-1] >= 0.5 * full))
        g = np.array(gains)
        g1, g0 = g[:, 0], g[:, 1]
        c['times'] = {'emerging': float(times[out_idx]), 'middle': float(T / 2), 'hiding': float(times[in_idx])}
        c['flare_gain'] = g1.mean(0)
        c['lit'] = bool(luma_linear(g1).mean() > 1.02)
        hide_b = hide > 0.5
        rep[name].update({
            'rest_centre_px': [float(v) for v in path['rest']], 'rest_rotation_degrees': float(path['rotation']),
            'waypoints_frame_units': [[round(float(p[0]), 4), round(float(p[1]), 4), pause] for p, pause in waypoints_frame(path)],
            'speed_frame_units_per_s': path['speed'] * SX, 'face_travel': path['face_travel'],
            'facing_offset': path['facing_offset'], 'run_seconds': T, 'capture_times_s': c['times'],
            'visible_alpha_at_start': float(visible_alpha[0]), 'visible_alpha_at_end': float(visible_alpha[-1]),
            'largest_visible_alpha': float(full),
            'max_visible_px': worst,
            'light1_over_base_along_visible_path_luma': {'min': float(luma_linear(g1).min()), 'mean': float(luma_linear(g1).mean()),
                                                         'max': float(luma_linear(g1).max())},
            'light0_over_base_along_visible_path_luma_max': float(luma_linear(g0).max()),
            'lit_by_light_1': c['lit'], 'flare_gain_mean_rgb': g1.mean(0).tolist(),
            'hiding_place_px': {'on_nymph': int((hide_b & (nymph > 0)).sum()), 'on_face_mask': int((hide_b & (face > 0)).sum()),
                                'in_mist_mask': int((hide_b & (mist_mask > 0.05)).sum()), 'on_playing_surface': int((hide_b & surface).sum())},
            'hiding_polygon': CREATURES[name]['hide']})


# ======================================================================================================= placement
def map_rect(rect):
    x, y, w, h = rect
    return [x / W, 1 - (y + h) / H, w / W, h / H]


def placement(report, layers, creatures, tiles, mist_mask, hides_all):
    """RectTransform values in the Bound Tree Scene frame (anchored to its top-left corner) for every layer."""
    def rect_values(x, y, w, h, pivot=(0.0, 1.0)):
        return {'anchored_position': [round((x + pivot[0] * w) * SX, 4), round(-(y + (1 - pivot[1]) * h) * SY, 4)],
                'size': [round(w * SX, 4), round(h * SY, 4)], 'pivot': list(pivot)}
    out = {}
    for name, l in layers.items():
        x, y, w, h = l['rect']
        out['Moss' + name] = dict(rect_values(x, y, w, h), map_rect=map_rect(l['rect']))
    for name, b in report['mist']['bands'].items():
        x, y, w, h = b['rect_xywh']
        out['Mist' + name] = dict(rect_values(x, y, w, h), uv_rect=[0, 0, b['uv_width'], 1])

    def full_frame_crop(m, margin=4):
        ys, xs = np.nonzero(m > 0)
        x0, y0, x1, y1 = max(0, xs.min() - margin), max(0, ys.min() - margin), min(W, xs.max() + 1 + margin), min(H, ys.max() + 1 + margin)
        return (int(x0), int(y0), int(x1 - x0), int(y1 - y0))
    # the mist occluder covers the bands' rows; the hiding places' occluder their bounding box
    top = min(b['rect_xywh'][1] for b in report['mist']['bands'].values())
    bottom = max(b['rect_xywh'][1] + b['rect_xywh'][3] for b in report['mist']['bands'].values())
    mrect = (0, int(math.floor(top)), W, int(math.ceil(bottom)) - int(math.floor(top)))
    hrect = full_frame_crop(hides_all)
    for key, r in (('MistOccluder', mrect), ('HidingPlaces', hrect)):
        x, y, w, h = r
        out[key] = dict(rect_values(x, y, w, h), uv_rect=[x / W, 1 - (y + h) / H, w / W, h / H], map_rect=map_rect(r))
    for name, c in creatures.items():
        s = c['sprite']
        h, w = s.shape[:2]
        rest = c['path']['rest']
        # creatures turn about their centre; the response map is sampled at the middle of their visible path
        centre, _ = evaluate(c['path'], c['times']['middle'])
        out[name] = {'anchored_position': [round(float(rest[0] * SX), 4), round(float(-rest[1] * SY), 4)],
                     'size': [round(w * SX, 4), round(h * SY, 4)], 'pivot': [0.5, 0.5],
                     'rotation_z': round(float(c['path']['rotation']), 4),
                     'map_rect': map_rect((centre[0] - w / 2, centre[1] - h / 2, w, h)), 'lit_by_light_1': c['lit'],
                     'in_group': CREATURES[name]['in_group']}
    report['placement'] = out


# ========================================================================================================= reviews
def reviews(rdir, P, plates, layers, static, tiles, mist_mask, creatures, report):
    concept = load(CONCEPT)
    nymph = load(NYMPH, 'RGBA')
    bg = load(BACKGROUND)
    removed = np.where(np.any(plates['glow'] != P['glow'], -1)[..., None], plates['glow'], bg)
    hides_all = np.maximum.reduce([c['hide'] for c in creatures.values()])

    def moss_rgba(name, weights=(0.0, 0.0, 0.0)):
        l = layers[name]
        x, y, w, h = l['rect']
        rgba = l['full']['glow']
        if not any(weights):
            return rgba
        cols, rows, shapes = l['mesh']
        yy, xx = np.mgrid[0:H, 0:W].astype(float) + 0.5
        d = sum(wt * mesh_displacement(cols, rows, sh, l['rect'], xx, yy) for wt, sh in zip(weights, shapes))
        # inverse warp: a pixel shows the layer where its displacement came from (small, smooth fields)
        pm = rgba.copy()
        pm[..., 3] /= 255.0
        pm[..., :3] *= pm[..., 3:4]
        s = sample(pm, xx - 0.5 - d, yy - 0.5)
        a = s[..., 3]
        return np.concatenate([s[..., :3] / np.maximum(a, 1e-6)[..., None], a[..., None] * 255], -1)

    def scene(mist_frac=0.0, moss_weights=None, creature=None):
        img = removed.copy()
        for name, b in MIST.items():
            prgb, pa = place_band(tiles[name], b['centre_y'], mist_frac * tiles[name].shape[1], b['opacity'])
            img = prgb + img * (1 - pa[..., None])
        img = img * mist_mask[..., None] + removed * (1 - mist_mask[..., None])     # occluder: plate through ~mask
        if creature is not None:
            sprite, centre, angle = creature
            prgb, pa, o = render(sprite, centre, angle)
            img = paste(None, prgb, o) + img * (1 - paste(None, pa, o)[..., None])
            img = img * (1 - hides_all[..., None]) + removed * hides_all[..., None]   # hiding places' occluder
        for name in layers:
            img = over(img, moss_rgba(name, (moss_weights or {}).get(name, (0, 0, 0))))
        return over(img, nymph)

    (rdir / 'moss').mkdir(parents=True, exist_ok=True)
    (rdir / 'mist').mkdir(parents=True, exist_ok=True)
    (rdir / 'creatures').mkdir(parents=True, exist_ok=True)
    rest = scene()
    save(rdir / 'scene-at-rest.png', row([labelled(concept, 'concept'), labelled(rest, 'moss and mist at rest')]))
    for name, l in layers.items():
        box = MOSS[name]['box']
        tiles_ = [labelled(crop(concept, box), 'concept'), labelled(crop(over(removed, nymph), box), 'moss removed'),
                  labelled(crop(over(over(removed, l['full']['glow']), nymph), box), 'moss at rest'),
                  labelled(crop(np.clip(np.abs(over(over(removed, l['full']['glow']), nymph) - over(bg, nymph)) * 4, 0, 255), box), 'rest vs plate x4')]
        save(rdir / 'moss' / f'moss-{name.lower()}-rest.png', row(tiles_))
        poses = [('channels +1 -1 +1', (1, -1, 1)), ('channels -1 +1 -1', (-1, 1, -1)), ('all +1', (1, 1, 1)), ('all -1', (-1, -1, -1))]
        save(rdir / 'moss' / f'moss-{name.lower()}-sway.png',
             row([labelled(crop(scene(moss_weights={name: wts}), box), lab) for lab, wts in poses]))
        bx = (box[0] + 60, box[1] + 60, box[0] + 260, box[1] + 330) if name == 'Left' else (box[0] + 80, box[1] + 30, box[0] + 280, box[1] + 300)
        save(rdir / 'moss' / f'moss-{name.lower()}-sway-detail-2x.png',
             row([labelled(crop(scene(moss_weights={name: wts}), bx, 2), lab + ', 2x') for lab, wts in [('rest', (0, 0, 0))] + poses[:2]]))
        chk = checker(H, W, 6)
        save(rdir / 'moss' / f'moss-{name.lower()}-layer.png',
             row([labelled(crop(over(chk, l['full']['glow']), box), 'layer on checker'),
                  labelled(crop(np.repeat(l['full']['glow'][..., 3:], 3, -1), box), 'matte')]))
    half = lambda im: crop(im, (0, 0, W, H), 0.5)
    save(rdir / 'mist' / 'mist-drift.png', row([labelled(half(over(bg, nymph)), 'no mist'), labelled(half(rest), 'mist, offset 0'),
                                                 labelled(half(scene(1 / 3)), 'drifted 1/3 tile'), labelled(half(scene(2 / 3)), 'drifted 2/3 tile')]))
    for nm, bx in {'left': (0, 520, 330, 880), 'right': (600, 560, 941, 1060)}.items():
        save(rdir / 'mist' / f'mist-{nm}-water-2x.png', row([labelled(crop(over(bg, nymph), bx, 2), 'no mist'),
                                                              labelled(crop(rest, bx, 2), 'mist at rest'),
                                                              labelled(crop(scene(0.5), bx, 2), 'drifted 1/2 tile')]))
    strips = []
    for k, t in tiles.items():
        two = np.concatenate([t, t], 1)
        img = two[..., :3] + 25.0 * (1 - two[..., 3:4])
        img = np.concatenate([img, np.zeros((6, img.shape[1], 3))], 0)
        img[-6:, t.shape[1] - 1:t.shape[1] + 1] = (255, 0, 0)
        strips.append(labelled(img, f'mist {k.lower()}: two tiles side by side, wrap seam at the red mark'))
    save(rdir / 'mist' / 'mist-wrap-seam.png', column(strips))
    save(rdir / 'mist' / 'mist-mask.png', row([labelled(half(np.repeat(mist_mask[..., None] * 255, 3, -1)), 'mist mask (white: mist shows)'),
                                               labelled(half(np.where(((mist_mask > 0.5) & ~binary_erosion(mist_mask > 0.5))[..., None], (0, 255, 255), over(bg, nymph))), 'mask edge')]))
    # creatures: three moments of each run over the scene, the paths and hiding places, the sprites
    ov = Image.fromarray(np.clip(np.rint(rest), 0, 255).astype(np.uint8))
    dr = ImageDraw.Draw(ov)
    dr.line([tuple(p) for p in PLAYING_SURFACE[:-2]], fill=(255, 60, 60), width=2)
    colours = {'Snake': (80, 255, 120), 'Beetle': (255, 220, 60), 'Centipede': (90, 200, 255)}
    for name, c in creatures.items():
        sprite, path, hide = c['sprite'], c['path'], c['hide']
        ring = np.argwhere((hide > 0.5) & ~binary_erosion(hide > 0.5))
        for y, x in ring:
            dr.point((x, y), fill=(255, 80, 255))
        dr.line([tuple(p) for p in path['points']], fill=colours[name], width=1)
        frames = []
        stops = [('rest (hidden)', 0.0)] + list(c['times'].items()) + [('end (hidden)', duration(path))]
        boxes = []
        for _, t in stops:
            centre, angle = evaluate(path, t)
            boxes.append(centre)
        bc = np.array(boxes)
        r = max(sprite.shape) * 0.7 + 20
        tb = (max(0, bc[:, 0].min() - r), max(0, bc[:, 1].min() - r), min(W, bc[:, 0].max() + r), min(H, bc[:, 1].max() + r))
        z = max(1.0, min(3.0, 360.0 / max(tb[2] - tb[0], tb[3] - tb[1])))
        for lab, t in stops:
            centre, angle = evaluate(path, t)
            frames.append(labelled(crop(scene(creature=(sprite, centre, angle)), tb, z), f'{name.lower()}: {lab} {t:.1f} s ({z:.1f}x)'))
        save(rdir / 'creatures' / f'{name.lower()}-stops.png', row(frames))
        # the same moments with the occluder removed and the hiding place outlined
        frames = []
        for lab, t in stops[1:4]:
            centre, angle = evaluate(path, t)
            prgb, pa, o = render(sprite, centre, angle)
            img = paste(None, prgb, o) + rest * (1 - paste(None, pa, o)[..., None])
            img = np.where(((hide > 0.5) & ~binary_erosion(hide > 0.5))[..., None], (255, 80, 255), img)
            frames.append(labelled(crop(img, tb, z), f'{lab}, unoccluded'))
        save(rdir / 'creatures' / f'{name.lower()}-unoccluded.png', row(frames))
        chk = checker(sprite.shape[0], sprite.shape[1], 4)
        sz = max(1, int(round(240 / max(sprite.shape[:2]))))
        whole = (0, 0, sprite.shape[1], sprite.shape[0])
        save(rdir / 'creatures' / f'{name.lower()}-sprite.png',
             row([labelled(crop(over(chk, sprite), whole, sz), 'on checker'),
                  labelled(crop(over(np.full_like(chk, 110.0), sprite), whole, sz), 'on grey'),
                  labelled(crop(np.repeat(sprite[..., 3:], 3, -1), whole, sz), 'matte')]))
    ov.save(rdir / 'creatures' / 'paths-overview.png')
    ov.crop((340, 520, 760, 1060)).resize((840, 1080), Image.LANCZOS).save(rdir / 'creatures' / 'paths-overview-2x.png')


# ========================================================================================================= install
TEMPLATES = {
    'plate_unlit': RELIGHT / 'BoundTreeFacelessPlateUnlit.png.meta',
    'plate_glow': RELIGHT / 'BoundTreeFacelessPlateGlow.png.meta',
    'plate_glowlit': RELIGHT / 'BoundTreeFacelessPlateGlowLit.png.meta',
    'rgba': ART / 'BoundTreeNymph.png.meta',
    'mask': ART / 'BoundTreeFaceMask.png.meta',
}


def used_guids():
    used = set()
    for meta in (ROOT / 'Dice-Unity/Assets').rglob('*.meta'):
        for ln in meta.read_text(encoding='utf-8', errors='ignore').splitlines()[:3]:
            if ln.startswith('guid:'):
                used.add(ln.split(':', 1)[1].strip())
    return used


def write_texture(path, arr, template, used, created, wrap_u_repeat=False):
    save(path, arr)
    if not np.array_equal(load(path, 'RGBA' if arr.shape[-1] == 4 else 'RGB'), np.clip(np.rint(arr), 0, 255)):
        raise SystemExit(f'{path} does not read back equal to the build output.')
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return
    guid = uuid.uuid4().hex
    while guid in used:
        guid = uuid.uuid4().hex
    used.add(guid)
    lines = []
    for ln in TEMPLATES[template].read_text(encoding='ascii').splitlines():
        if ln.startswith('guid:'):
            ln = 'guid: ' + guid
        elif wrap_u_repeat and ln.strip() == 'wrapU: 1':
            # the mist tiles wrap horizontally as their uvRect drifts
            ln = ln.replace('wrapU: 1', 'wrapU: 0')
        lines.append(ln)
    meta.write_text('\n'.join(lines) + '\n', encoding='ascii', newline='\n')
    created.append(rel(meta))


def outputs(plates, layers, tiles, mist_mask, creatures):
    """Every installed texture: (path, array, meta template, wraps horizontally)."""
    files = []
    for k, suffix in VERSION.items():
        files.append((RELIGHT / f'BoundTreeFacelessPlate{suffix}MossRemoved.png', plates[k], 'plate_' + k, False))
    for name, l in layers.items():
        x, y, w, h = l['rect']
        for k, suffix in VERSION.items():
            files.append((RELIGHT / f'BoundTreeMoss{name}{suffix}.png', l['full'][k][y:y + h, x:x + w], 'rgba', False))
    for name, t in tiles.items():
        files.append((ART / f'BoundTreeMist{name}.png', straight(t), 'rgba', True))
    white = np.full((H, W, 3), 255.0)
    files.append((ART / 'BoundTreeMistMask.png', np.concatenate([white, (mist_mask * 255)[..., None]], -1), 'mask', False))
    return files


def creature_textures(creatures, P):
    files = []
    for name, c in creatures.items():
        sprite = c['sprite']
        lin = to_linear(sprite[..., :3])
        if c['lit']:
            files.append((RELIGHT / f'BoundTree{name}Unlit.png', sprite, 'rgba', False))
            lit = np.concatenate([to_srgb(np.maximum(lin * c['flare_gain'], lin)), sprite[..., 3:]], -1)
            files.append((RELIGHT / f'BoundTree{name}GlowLit.png', lit, 'rgba', False))
        else:
            files.append((ART / f'BoundTree{name}.png', sprite, 'rgba', False))
    return files


def write_meshes_and_paths(layers, creatures, report):
    """Moss deformation meshes and the creatures' Path motion assets (written under OUT unless installing)."""
    written = {}
    for name, l in layers.items():
        x, y, w, h = l['rect']
        cols, rows, shapes = l['mesh']

        def field(shape):
            def offset(u, v):
                c, r = int(round(u * cols)), int(round(v * rows))
                return shape[r, c] / w, 0.0
            return offset
        mesh = A.mesh(CONTENT / f'BoundTreeMoss{name}Mesh.asset', cols, rows,
                      [(f'Sway{k}', field(sh)) for k, sh in enumerate(shapes)])
        written[f'BoundTreeMoss{name}Mesh.asset'] = mesh
    script = A.guid(ASSETS / 'Presentation/AmbientPathMotionAsset.cs')
    for name, c in creatures.items():
        p = c['path']
        data = {'waypoints': [{'Position': A.v2(round(float(q[0]), 4), round(float(q[1]), 4)), 'Pause': float(pause)}
                              for q, pause in waypoints_frame(p)],
                'speed': round(p['speed'] * SX, 4), 'faceTravel': p['face_travel'], 'facingOffset': float(p['facing_offset'])}
        written[f'BoundTree{name}Path.asset'] = A.write_asset(CONTENT / f'BoundTree{name}Path.asset', script, data)
    report['content_assets'] = written


# ============================================================================================================ main
def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--output', help='Unused directory under Docs/work (default: a fresh bound-tree-ambient-* directory).')
    parser.add_argument('--write-assets', action='store_true',
                        help='Also install the textures (keeping existing .meta files), the moss meshes and the creature paths.')
    args = parser.parse_args()
    if args.output:
        OUT = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in OUT.parents:
            raise SystemExit('--output must be under Docs/work.')
        OUT.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        OUT = Path(tempfile.mkdtemp(prefix='bound-tree-ambient-', dir=WORK))
    A.OUTPUT = None if args.write_assets else OUT
    P = {k: load(p) for k, p in PLATES.items()}
    report = {}
    plates, layers, static = build_moss(P, report)
    creatures, gain, gain0 = build_creatures(P, report)
    hides_all = np.maximum.reduce([c['hide'] for c in creatures.values()])
    # the painted trunk-edge strand stays on the plate; it occludes creatures that pass behind it
    occluder = np.maximum(hides_all, np.clip(static * 1.0, 0, 1))
    tiles, mist_mask = build_mist(P, hides_all, report)
    report['mist']['mask']['occluder_px_in_mist_mask'] = int(((occluder > 0.05) & (mist_mask > 0.05)).sum())
    check_creatures(creatures, gain, gain0, mist_mask, report)
    placement(report, layers, creatures, tiles, mist_mask, occluder)
    files = outputs(plates, layers, tiles, mist_mask, creatures) + creature_textures(creatures, P)
    white = np.full((H, W, 3), 255.0)
    files.append((ART / 'BoundTreeHidingPlacesMask.png', np.concatenate([white, (occluder * 255)[..., None]], -1), 'mask', False))
    for path, arr, _, _ in files:
        save(OUT / 'textures' / path.name, arr)
    write_meshes_and_paths(layers, creatures, report)
    reviews(OUT / 'review', P, plates, layers, static, tiles, mist_mask, creatures, report)
    if args.write_assets:
        used, created = used_guids(), []
        for path, arr, template, wrap in files:
            write_texture(path, arr, template, used, created, wrap)
        report['installed'] = {'textures': [rel(p) for p, *_ in files], 'created_meta': created}
    (OUT / 'report.json').write_text(json.dumps(report, indent=1, default=float) + '\n', encoding='ascii')
    summary = {'moss': {k: {kk: v[kk] for kk in ('rest_error_levels', 'mesh', 'crop_rect_xywh')} for k, v in report['moss'].items() if k in MOSS},
               'plates': report['moss']['plates'],
               'creatures': {k: {kk: v[kk] for kk in ('sprite_size', 'max_visible_px', 'visible_alpha_at_start', 'visible_alpha_at_end',
                                                       'hiding_place_px', 'run_seconds', 'capture_times_s', 'lit_by_light_1',
                                                       'light1_over_base_along_visible_path_luma', 'in_group')}
                             for k, v in report['creatures'].items() if k in CREATURES}}
    print(json.dumps(summary, indent=1, default=float))
    print('Wrote ' + str(OUT))


if __name__ == '__main__':
    main()
