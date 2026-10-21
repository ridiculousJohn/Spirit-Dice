"""Author the haunted merchant's relighting sources from registered imagegen lighting donors.

Python 3.10 or later with numpy, Pillow and scipy. Stages:

  targets  Write the imagegen edit targets under Docs/work/merchant-relight/targets: the merchant
           composited over the lighting plates (merchant frame) and, once MerchantHostUnlit.png
           exists, the unlit merchant over the lantern plate and the canopy cloth over the unlit
           scene (background frame).
  build    Register the preserved donors (Docs/Reference/Art/HauntedMerchantRelight*Donor.png),
           transfer illumination inside the merchant alpha and the dilated canopy mask only, derive
           the per-layer textures through linear ratio maps, author the depth and lantern-response
           data maps, and write the review contact sheet. Installed textures go to
           Dice-Unity/Assets/Dice/Art/HauntedMerchant/Relight (a .meta with a fresh GUID is written
           only when none exists); measurements go to Docs/work/merchant-relight/relight-build.json.
  cup-targets
           Write the hanging cup's edit targets under Docs/work/merchant-relight/cup/targets: the
           painted HangingCup.png over the unlit scene at its rest rect and, once
           HangingCupUnlit.png exists, the unlit cup over the lantern-lit scene (cup frame).
  cup      Register the preserved cup donors (Docs/Reference/Art/HauntedMerchantRelightCup*Donor.png)
           in the cup frame and write HangingCupUnlit.png and HangingCupLanternLit.png to the
           Relight folder; measurements go to Docs/work/merchant-relight/cup/cup-build.json. Only
           the cup textures are written.

Run from the repository root, for example:
  python Tools/Presentation/author-merchant-relight.py build --replace --save-registered
  python Tools/Presentation/author-merchant-relight.py cup --replace --save-registered

Existing files are never replaced unless --replace is given; source artwork is only read.
"""
from pathlib import Path
import argparse
import json
import sys

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, map_coordinates, distance_transform_edt, binary_erosion
from scipy.optimize import minimize

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Dice-Unity/Assets/Dice/Art/HauntedMerchant'
OUT = ART / 'Relight'
WORK = ROOT / 'Docs/work/merchant-relight'
SIZE = (941, 1672)

# Merchant source frame to background frame, continuous pixel coordinates (top-left origin).
MAP_SCALE = (0.9136, 0.94)
MAP_OFFSET = (21.0, 28.8)

# Edit-target crop in the merchant frame (x0, y0, x1, y1) and its upscale for imagegen.
TARGET_BOX = (0, 240, 760, 1100)
TARGET_SCALE = 2

# Blink crop box in the merchant frame (MerchantBlinkEyes.png / MerchantBlinkMask.png).
BLINK_BOX = (264, 576, 400, 634)


def load(name, mode='RGBA'):
    return np.asarray(Image.open(ART / name).convert(mode)).astype(np.float64)


def to_linear(c):
    c = np.clip(c / 255.0, 0, 1)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_srgb(lin):
    lin = np.clip(lin, 0, 1)
    c = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - 0.055)
    return c * 255.0


def save_png(path, array, mode, replace):
    path = Path(path)
    if path.exists() and not replace:
        raise SystemExit(f'Refusing to replace existing {path}; pass --replace for working outputs.')
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(np.rint(array), 0, 255).astype(np.uint8), mode).save(path)
    return path


def plate_in_merchant_frame(plate):
    """Resample a background-frame RGB plate onto the merchant's native frame (bilinear)."""
    h, w = SIZE[1], SIZE[0]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float64)
    bx = MAP_SCALE[0] * (xx + 0.5) + MAP_OFFSET[0] - 0.5
    by = MAP_SCALE[1] * (yy + 0.5) + MAP_OFFSET[1] - 0.5
    return np.stack([map_coordinates(plate[..., c], [by, bx], order=1, mode='nearest') for c in range(plate.shape[2])], -1)


def over(rgba, plate_rgb):
    a = rgba[..., 3:4] / 255.0
    return rgba[..., :3] * a + plate_rgb * (1 - a)


def write_target(host, plate_name, path, replace):
    x0, y0, x1, y1 = TARGET_BOX
    size = ((x1 - x0) * TARGET_SCALE, (y1 - y0) * TARGET_SCALE)
    bg = plate_in_merchant_frame(load(plate_name, 'RGB'))
    comp = over(host, bg)[y0:y1, x0:x1]
    img = Image.fromarray(np.clip(np.rint(comp), 0, 255).astype(np.uint8), 'RGB').resize(size, Image.LANCZOS)
    if path.exists() and not replace:
        raise SystemExit(f'Refusing to replace existing {path}')
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
    print(path)


def stage_targets(args):
    """Edit targets: the current merchant over the unlit plate (unlit donor) and over the lantern
    plate (lantern donor derived from the current art), plus, once MerchantHostUnlit.png exists,
    the unlit merchant over the lantern plate (lantern donor derived from the unlit result)."""
    host = load('MerchantHost.png')
    write_target(host, 'MerchantAmbientUnlit.png', WORK / 'targets' / 'target-unlit.png', args.replace)
    write_target(host, 'MerchantLanternOnly.png', WORK / 'targets' / 'target-lantern.png', args.replace)
    unlit = (Path(args.out) if args.out else OUT) / 'MerchantHostUnlit.png'
    if unlit.exists():
        rgba = np.asarray(Image.open(unlit).convert('RGBA')).astype(np.float64)
        write_target(rgba, 'MerchantLanternOnly.png', WORK / 'targets' / 'target-lantern-from-unlit.png', args.replace)
        # Canopy: the painted cloth (MerchantOriginal.png through the overhang mask) over the unlit
        # plate and the unlit merchant, in the background frame.
        comp = canopy_composite(load('MerchantAmbientUnlit.png', 'RGB'), rgba)
        x0, y0, x1, y1 = CANOPY_BOX
        img = Image.fromarray(np.clip(np.rint(comp[y0:y1, x0:x1]), 0, 255).astype(np.uint8), 'RGB')
        img = img.resize(((x1 - x0) * TARGET_SCALE, (y1 - y0) * TARGET_SCALE), Image.LANCZOS)
        path = WORK / 'targets' / 'target-canopy-unlit.png'
        if path.exists() and not args.replace:
            raise SystemExit(f'Refusing to replace existing {path}')
        img.save(path)
        print(path)


# Canopy cloth: MerchantOriginal.png RGB sampled through CanopyOriginalOverhangMask.png (background
# frame). CANOPY_BOX is the edit-target crop in that frame; CANOPY_DILATE pads the transfer region
# beyond the mask so bilinear sampling at the cut edge reads relit color.
CANOPY_BOX = (0, 0, 720, 520)
CANOPY_DILATE = 4
CANOPY_FIT_SIGMA = 2.0


def canopy_mask():
    return load('CanopyOriginalOverhangMask.png')[..., 3]


def canopy_composite(base_rgb, merchant_rgba=None):
    comp = base_rgb.copy()
    if merchant_rgba is not None:
        comp = over_plate(merchant_rgba, comp)
    cloth = np.concatenate([load('MerchantOriginal.png', 'RGB'), canopy_mask()[..., None]], -1)
    return over(cloth, comp)


# ---------------------------------------------------------------------------------------------
# Registration of a generated donor (a regenerated TARGET_BOX crop) to the native merchant frame.
# Model: donor_px = M (p - c) + t + local(p), p a native merchant pixel center, M a 2x2 matrix,
# local(p) a smooth residual displacement interpolated from per-tile cross-correlation peaks.
# The fit maximises normalised cross-correlation of locally contrast-normalised, high-passed
# luminance inside the eroded merchant alpha, so it follows painted structure, not illumination.
REG_HIGHPASS = 6.0
REG_TILE = 40
REG_SEARCH = 5
REG_LOCAL_SMOOTH = 1.5


def luminance(rgb):
    return rgb @ np.array([0.299, 0.587, 0.114])


def structure(lum, sigma_pre, sigma_hp):
    x = gaussian_filter(lum, sigma_pre) if sigma_pre > 0 else lum
    x = x - gaussian_filter(x, sigma_hp)
    return x / (np.sqrt(gaussian_filter(x * x, 2 * sigma_hp)) + 1.0)


def register(donor, src, alpha, box=TARGET_BOX):
    """Return (dx_map, dy_map, report): donor pixel coordinates for every pixel center of the
    native frame (941 x 1672, or the cup's 1024 x 1536). `src` is the edit target's content at
    native size (RGB), `alpha` (0..255) the region whose painted structure drives the fit, `box`
    the crop the donor regenerates."""
    x0, y0, x1, y1 = box
    scale = (donor.shape[1] / (x1 - x0), donor.shape[0] / (y1 - y0))
    s_struct = structure(luminance(src), 0, REG_HIGHPASS)
    d_struct = structure(luminance(donor), 0.45 * np.mean(scale), REG_HIGHPASS * np.mean(scale))
    inner = binary_erosion(alpha > 250, iterations=6)
    box = np.zeros_like(inner)
    box[y0 + 8:y1 - 8, x0 + 8:x1 - 8] = True
    ys, xs = np.nonzero(inner & box)
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
    best = p0
    # Coarse translation search, then a full affine refinement.
    best_score = -2
    for ox in np.arange(-24, 25, 4):
        for oy in np.arange(-24, 25, 4):
            p = p0 + np.array([0, 0, 0, 0, ox, oy])
            s = ncc(p)
            if s > best_score:
                best, best_score = p, s
    res = minimize(lambda p: -ncc(p), best, method='Powell', options={'xtol': 1e-4, 'ftol': 1e-6, 'maxfev': 6000})
    affine = res.x
    affine_score = -res.fun

    # Local residual displacement: per-tile NCC peak search around the affine prediction.
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float64)
    au, av = mapping(affine, xx, yy)
    tiles = []
    step = REG_TILE // 2
    for ty in range(y0, y1 - REG_TILE + 1, step):
        for tx in range(x0, x1 - REG_TILE + 1, step):
            m = inner[ty:ty + REG_TILE, tx:tx + REG_TILE]
            if m.mean() < .6:
                continue
            s = s_struct[ty:ty + REG_TILE, tx:tx + REG_TILE][m]
            s = (s - s.mean()) / (s.std() + 1e-9)
            bu, bv = au[ty:ty + REG_TILE, tx:tx + REG_TILE][m], av[ty:ty + REG_TILE, tx:tx + REG_TILE][m]
            scores = {}
            for ox in range(-REG_SEARCH, REG_SEARCH + 1):
                for oy in range(-REG_SEARCH, REG_SEARCH + 1):
                    d = map_coordinates(d_struct, [bv + oy, bu + ox], order=1, mode='nearest')
                    d = (d - d.mean()) / (d.std() + 1e-9)
                    scores[(ox, oy)] = float(np.mean(s * d))
            (ox, oy), sc = max(scores.items(), key=lambda kv: kv[1])
            # Sub-pixel refinement by parabola fit on each axis.
            def para(a, b, cc):
                den = a - 2 * b + cc
                return 0.0 if abs(den) < 1e-9 else float(np.clip(0.5 * (a - cc) / den, -.5, .5))
            fx = ox + (para(scores.get((ox - 1, oy), sc), sc, scores.get((ox + 1, oy), sc)) if abs(ox) < REG_SEARCH else 0)
            fy = oy + (para(scores.get((ox, oy - 1), sc), sc, scores.get((ox, oy + 1), sc)) if abs(oy) < REG_SEARCH else 0)
            tiles.append((tx + REG_TILE / 2, ty + REG_TILE / 2, fx, fy, sc, scores[(0, 0)]))
    t = np.array(tiles)
    # Robust smooth field: confident tiles only, Gaussian normalised convolution on the tile grid.
    good = t[:, 4] > 0.35
    gx = np.zeros((h, w))
    gy = np.zeros((h, w))
    wt = np.zeros((h, w))
    for cx, cy, fx, fy, sc, _ in t[good]:
        gx[int(cy), int(cx)] += fx * sc
        gy[int(cy), int(cx)] += fy * sc
        wt[int(cy), int(cx)] += sc
    sig = REG_TILE * REG_LOCAL_SMOOTH
    W = gaussian_filter(wt, sig)
    W2 = gaussian_filter(wt, sig * 3)
    lx = np.where(W > 1e-7, gaussian_filter(gx, sig) / np.maximum(W, 1e-12), gaussian_filter(gx, sig * 3) / np.maximum(W2, 1e-12))
    ly = np.where(W > 1e-7, gaussian_filter(gy, sig) / np.maximum(W, 1e-12), gaussian_filter(gy, sig * 3) / np.maximum(W2, 1e-12))
    lx = np.nan_to_num(lx)
    ly = np.nan_to_num(ly)
    du, dv = au + lx, av + ly

    def score_map(U, V):
        d = map_coordinates(d_struct, [V[ys.astype(int), xs.astype(int)], U[ys.astype(int), xs.astype(int)]], order=1, mode='nearest')
        d = (d - d.mean()) / (d.std() + 1e-9)
        return float(np.mean(sv * d))

    report = {
        'donor_size': [int(donor.shape[1]), int(donor.shape[0])],
        'affine_m00_m01_m10_m11_tx_ty': [float(v) for v in affine],
        'affine_center_native': [float(c[0]), float(c[1])],
        'ncc_affine': affine_score,
        'ncc_affine_plus_local': score_map(du, dv),
        'local_tiles': int(len(t)),
        'local_tiles_used': int(good.sum()),
        'local_shift_max_px': float(np.abs(np.stack([lx, ly]))[:, alpha > 0].max()),
    }
    return du, dv, report


def sample_donor(donor, du, dv):
    return np.stack([map_coordinates(donor[..., ch], [dv, du], order=1, mode='nearest') for ch in range(3)], -1)


# ---------------------------------------------------------------------------------------------
# Lantern response in the merchant frame.
# MerchantLanternResponse.png R is an elliptical linear distance from the lantern, clamped at 1:
#   R = min(1, hypot((bx - LANTERN_BG[0]) / LANTERN_RADIUS_BG[0], (by - LANTERN_BG[1]) / LANTERN_RADIUS_BG[1]))
# with continuous background-pixel coordinates (pixel centers at +0.5). A least-squares fit over
# every unclamped pixel reproduces the installed map within 0.51/255 (quantization).
LANTERN_BG = (90.0, 335.0)
LANTERN_RADIUS_BG = (650.0, 950.0)


def lantern_in_merchant_frame():
    cx = (LANTERN_BG[0] - MAP_OFFSET[0]) / MAP_SCALE[0]
    cy = (LANTERN_BG[1] - MAP_OFFSET[1]) / MAP_SCALE[1]
    return (cx, cy), (LANTERN_RADIUS_BG[0] / MAP_SCALE[0], LANTERN_RADIUS_BG[1] / MAP_SCALE[1])


def lantern_distance(bx, by):
    return np.minimum(1.0, np.hypot((bx - LANTERN_BG[0]) / LANTERN_RADIUS_BG[0], (by - LANTERN_BG[1]) / LANTERN_RADIUS_BG[1]))


def author_response():
    (cx, cy), (rx, ry) = lantern_in_merchant_frame()
    yy, xx = np.mgrid[0:SIZE[1], 0:SIZE[0]].astype(np.float64) + 0.5
    r = np.minimum(1.0, np.hypot((xx - cx) / rx, (yy - cy) / ry))
    out = np.zeros((SIZE[1], SIZE[0], 3))
    out[..., 0] = r * 255
    return out


def verify_response(response):
    """Compare the merchant-frame map with the installed background map sampled through the mapping."""
    bgmap = load('MerchantLanternResponse.png', 'RGB')[..., 0]
    yy, xx = np.mgrid[0:SIZE[1], 0:SIZE[0]].astype(np.float64)
    bx = MAP_SCALE[0] * (xx + 0.5) + MAP_OFFSET[0] - 0.5
    by = MAP_SCALE[1] * (yy + 0.5) + MAP_OFFSET[1] - 0.5
    inside = (bx >= 0) & (bx <= SIZE[0] - 1) & (by >= 0) & (by <= SIZE[1] - 1)
    sampled = map_coordinates(bgmap, [by, bx], order=1, mode='nearest')
    alpha = load('MerchantHost.png')[..., 3] > 0
    diff = np.abs(np.rint(response[..., 0]) - sampled)
    yb, xb = np.mgrid[0:SIZE[1], 0:SIZE[0]].astype(np.float64) + 0.5
    fit = lantern_distance(xb, yb) * 255
    return {
        'max_abs_diff_vs_sampled_background_map_inside_merchant_alpha': float(diff[alpha & inside].max()),
        'mean_abs_diff_vs_sampled_background_map_inside_merchant_alpha': float(diff[alpha & inside].mean()),
        'background_model_max_abs_diff_levels': float(np.abs(fit - bgmap).max()),
    }


# ---------------------------------------------------------------------------------------------
# Coarse depth for computed candle lighting: R = 0 farthest (torso back plane), 1 nearest.
# Native merchant pixels, top-left origin. Fields are smooth analytic shapes combined through the
# existing layer masks; only the bare forearms keep a short (about 3 px) step where they sit in
# front of the torso.
DEPTH_TORSO_PLANE = .18
DEPTH_CHEST = (365, 830, 240, 270, .17)          # cx, cy, rx, ry, forward bulge
DEPTH_HEAD_BASE = .40
DEPTH_FACE = (345, 650, 100, 120, .14)           # nose/cheeks forward of the head base
DEPTH_HAT = (315, 540, 175, 105, .04)
DEPTH_HEAD_BLEND_SIGMA = 10.0
# Image-left upper arm (sleeve, torso layer): shoulder to elbow on the table.
DEPTH_LEFT_UPPER_ARM = ((165, 720, .30), (125, 950, .84), 70.0)   # start, end (x, y, depth), radius
# Supporting bare forearm: elbow on the table to the hand under the cheek.
DEPTH_SUPPORT_FOREARM = ((140, 940, .88), (272, 672, .60))
# Resting arm: shoulder (meets the torso field) to elbow, then forearm across the table to the hand.
DEPTH_REST_ARM = ((540, 690), (600, 950, .82), (285, 975, 1.0))
DEPTH_INFLATION = (.05, 28.0)                    # silhouette recession amount, distance (px)
DEPTH_SMOOTH_SIGMA = 3.0
DEPTH_SEAM_SIGMA = 1.0


def bump(xx, yy, ellipse):
    cx, cy, rx, ry, amount = ellipse
    r2 = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
    return amount * np.sqrt(np.clip(1 - r2, 0, 1))


def smoothstep(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def segment_param(xx, yy, a, b):
    ax, ay = a[0], a[1]
    bx, by = b[0], b[1]
    dx, dy = bx - ax, by - ay
    t = np.clip(((xx - ax) * dx + (yy - ay) * dy) / (dx * dx + dy * dy), 0, 1)
    dist = np.hypot(xx - (ax + t * dx), yy - (ay + t * dy))
    return t, dist


def mask_alpha(name):
    return load(name)[..., 3] / 255.0


def extend_outside(values, inside):
    """Nearest-inside extension of a field beyond the silhouette, then a light blur outside only."""
    _, (iy, ix) = distance_transform_edt(~inside, return_indices=True)
    ext = values[iy, ix]
    soft = gaussian_filter(ext, 2.0)
    return np.where(inside, values, soft)


def author_depth():
    h, w = SIZE[1], SIZE[0]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float64) + 0.5
    host_a = load('MerchantHost.png')[..., 3] / 255.0
    head_m = mask_alpha('MerchantHeadCompleteMask.png')
    sup_m = mask_alpha('MerchantSupportingForearmAnimationMask.png')
    rest_m = mask_alpha('MerchantRestingArmMask.png')

    torso = DEPTH_TORSO_PLANE + bump(xx, yy, DEPTH_CHEST)
    (ax, ay, ad), (bx, by, bd), radius = DEPTH_LEFT_UPPER_ARM
    t, dist = segment_param(xx, yy, (ax, ay), (bx, by))
    arm = ad + (bd - ad) * smoothstep(t)
    weight = 1 - smoothstep((dist - radius * .45) / (radius * .9))
    torso = torso + weight * np.maximum(arm - torso, 0)

    head = DEPTH_HEAD_BASE + bump(xx, yy, DEPTH_FACE) + bump(xx, yy, DEPTH_HAT)
    head_w = gaussian_filter(head_m * (host_a > 0), DEPTH_HEAD_BLEND_SIGMA)
    head_w = np.clip(head_w / max(head_w.max(), 1e-6), 0, 1) ** .5
    depth = torso + (head - torso) * head_w

    # Resting arm: continuous with the torso at the shoulder, forward toward elbow and hand.
    (sx, sy), (ex, ey, ed), (hx, hy, hd) = DEPTH_REST_ARM
    t1, d1 = segment_param(xx, yy, (sx, sy), (ex, ey))
    t2, d2 = segment_param(xx, yy, (ex, ey), (hx, hy))
    upper = depth + (ed - depth) * smoothstep(t1)
    fore = ed + (hd - ed) * t2
    near_fore = smoothstep((d1 - d2) / 40.0 + .5)
    rest_depth = upper + (fore - upper) * near_fore
    rest_w = gaussian_filter(rest_m, DEPTH_SEAM_SIGMA)
    depth = depth + (rest_depth - depth) * rest_w

    (fx, fy, fd), (gx, gy, gd) = DEPTH_SUPPORT_FOREARM
    t3, _ = segment_param(xx, yy, (fx, fy), (gx, gy))
    sup_depth = fd + (gd - fd) * t3
    sup_w = gaussian_filter(sup_m, DEPTH_SEAM_SIGMA)
    depth = depth + (sup_depth - depth) * sup_w

    inside = host_a > 0
    edt = distance_transform_edt(inside)
    amount, reach = DEPTH_INFLATION
    depth = depth - amount * (1 - smoothstep(edt / reach))
    depth = np.clip(depth, 0, 1)
    depth = extend_outside(depth, inside)
    # Final light smoothing keeps the forearm steps short but alias-free.
    depth = gaussian_filter(depth, DEPTH_SMOOTH_SIGMA * .5)
    depth = (depth - depth[inside].min()) / (depth[inside].max() - depth[inside].min())
    depth = np.clip(depth, 0, 1)
    out = np.repeat((depth * 255)[..., None], 3, -1)
    return out


# ---------------------------------------------------------------------------------------------
# Illumination transfer: linear-space ratio maps.
# ratio = G(w * donor) / G(w * source) per channel in linear light, G a Gaussian of RATIO_SIGMA,
# w the merchant interior (alpha > 250, eroded RATIO_ERODE px) where the registered donor is free
# of background contamination. Low-confidence pixels (silhouette ring, outside the alpha) blend
# to a nearest-interior extension, so the map is defined and smooth over the whole frame.
RATIO_SIGMA = 3.0
RATIO_ERODE = 3
RATIO_CLAMP = (0.02, 8.0)
# Highlight cap: a ratio cannot turn a painted warm specular (lantern glints on hat studs, wet
# skin) into a saturated cool one, so each relit channel is limited to CAP_GAIN times the local
# maximum of the registered donor (interior pixels, CAP_WINDOW px square), extended past the edge.
CAP_WINDOW = 9
CAP_GAIN = 1.15
# Saturation cap: thin painted rim strokes (lantern-lit hair strands, the lit edge of a hand) are
# narrower than RATIO_SIGMA, so the relit saturation (max - min) / max in linear light is also
# limited to SAT_GAIN times the local maximum of the donor's saturation (same window), by blending
# toward the pixel's own luminance; luminance and painted texture are kept.
SAT_GAIN = 1.1
SAT_FLOOR = 0.08
FIELD_CHANNELS = 7
REF = ROOT / 'Docs/Reference/Art'
DONORS = {
    'unlit': REF / 'HauntedMerchantRelightUnlitDonor.png',
    'lantern': REF / 'HauntedMerchantRelightLanternDonor.png',
    'canopy_unlit': REF / 'HauntedMerchantRelightCanopyUnlitDonor.png',
}


def ratio_map(donor_srgb, host, sigma=RATIO_SIGMA):
    hl = to_linear(host[..., :3])
    dl = to_linear(donor_srgb)
    w = binary_erosion(host[..., 3] > 250, iterations=RATIO_ERODE).astype(np.float64)
    conf = gaussian_filter(w, sigma)
    good = conf > 0.5
    _, (iy, ix) = distance_transform_edt(~good, return_indices=True)
    out = np.empty_like(hl)
    for ch in range(3):
        r = gaussian_filter(w * dl[..., ch], sigma) / np.maximum(gaussian_filter(w * hl[..., ch], sigma), 1e-6)
        r = np.clip(r, *RATIO_CLAMP)
        ext = gaussian_filter(r[iy, ix], 2 * sigma)
        k = np.clip(conf / 0.5, 0, 1)
        out[..., ch] = np.where(good, r, k * r + (1 - k) * ext)
    return out


def highlight_cap(donor_srgb, host):
    from scipy.ndimage import maximum_filter
    dl = to_linear(donor_srgb)
    w = binary_erosion(host[..., 3] > 250, iterations=1)
    _, (iy, ix) = distance_transform_edt(~w, return_indices=True)
    known = maximum_filter(w, size=CAP_WINDOW)
    smooth = gaussian_filter(dl, (1.0, 1.0, 0))
    hi, lo = smooth.max(-1), smooth.min(-1)
    sat = (hi - lo) / np.maximum(hi, 1e-4)
    chans = [dl[..., 0], dl[..., 1], dl[..., 2], sat]
    gains = [CAP_GAIN] * 3 + [SAT_GAIN]
    cap = np.empty(dl.shape[:2] + (4,))
    for ch, (value, gain) in enumerate(zip(chans, gains)):
        m = maximum_filter(np.where(w, value, 0), size=CAP_WINDOW)
        m = np.where(known, m, m[iy, ix])
        cap[..., ch] = np.clip(m * gain, 1e-4, 1) if ch < 3 else np.clip(m * gain, SAT_FLOOR, 1)
    return cap


def membrane_fill(field, unknown):
    """Harmonic (Laplace) fill of `field` inside `unknown` from the surrounding values, in log space."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.linalg import splu
    h, w = unknown.shape
    idx = -np.ones((h, w), np.int64)
    ys, xs = np.nonzero(unknown)
    n = len(ys)
    idx[ys, xs] = np.arange(n)
    logf = np.log(field)
    rows, cols, vals = [np.arange(n)], [np.arange(n)], [np.zeros(n)]
    rhs = np.zeros((n, field.shape[2]))
    diag = np.zeros(n)
    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        ny, nx = ys + dy, xs + dx
        ok = (ny >= 0) & (ny < h) & (nx >= 0) & (nx < w)
        diag[ok] += 1
        nyo, nxo, io = ny[ok], nx[ok], np.arange(n)[ok]
        j = idx[nyo, nxo]
        inner = j >= 0
        rows.append(io[inner]); cols.append(j[inner]); vals.append(-np.ones(inner.sum()))
        rhs[io[~inner]] += logf[nyo[~inner], nxo[~inner]]
    vals[0] = diag
    A = coo_matrix((np.concatenate(vals), (np.concatenate(rows), np.concatenate(cols))), shape=(n, n)).tocsc()
    lu = splu(A)
    out = field.copy()
    for ch in range(field.shape[2]):
        out[ys, xs, ch] = np.exp(lu.solve(rhs[:, ch]))
    return out


def relit_linear(rgba, field):
    """Relit linear RGB. A field of 2 * FIELD_CHANNELS carries a second (floor) field; the result
    is the per-channel maximum, which keeps lantern-lit >= unlit on every layer."""
    if field.shape[-1] == 2 * FIELD_CHANNELS:
        return np.maximum(relit_linear(rgba, field[..., :FIELD_CHANNELS]), relit_linear(rgba, field[..., FIELD_CHANNELS:]))
    lin = np.minimum(to_linear(rgba[..., :3]) * field[..., :3], field[..., 3:6])
    hi, lo = lin.max(-1), lin.min(-1)
    y = lin @ np.array([0.2126, 0.7152, 0.0722])
    smax = field[..., 6]
    sat = (hi - lo) / np.maximum(hi, 1e-6)
    den = (hi - lo) - smax * (hi - y)
    k = np.where((sat > smax) & (den > 1e-9), smax * y / np.maximum(den, 1e-9), 1.0)
    k = np.clip(k, 0, 1)[..., None]
    return y[..., None] + (lin - y[..., None]) * k


def apply_ratio(rgba, field):
    """Scale straight RGB in linear light by field[..., :3], limited by the highlight cap
    field[..., 3:]; alpha and every alpha-0 pixel stay byte-identical."""
    out = rgba.copy()
    lin = relit_linear(rgba, field)
    lit = np.rint(to_srgb(lin))
    inside = rgba[..., 3] > 0
    out[..., :3][inside] = lit[inside]
    return out


def measure(name, src, out, mask=None):
    changed = np.any(src != out, axis=-1)
    inside = src[..., 3] > 0 if mask is None else mask
    return {
        'texture': name,
        'pixels': int(src.shape[0] * src.shape[1]),
        'mask_pixels_alpha_gt_0': int(inside.sum()),
        'pixels_changed': int(changed.sum()),
        'pixels_changed_outside_mask': int((changed & ~inside).sum()),
        'alpha_identical': bool(np.array_equal(src[..., 3], out[..., 3])),
    }


# ---------------------------------------------------------------------------------------------
# Output, metadata and review.
COLOR_META = 'MerchantHost.png.meta'
DATA_META = 'MerchantLanternResponse.png.meta'


def write_meta(path, template):
    """Create a .meta next to `path` from an existing importer template with a fresh GUID.
    An existing .meta is kept so the asset GUID stays stable across rebuilds."""
    import uuid
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return False
    lines = (ART / template).read_text(encoding='ascii').splitlines()
    lines = [f'guid: {uuid.uuid4().hex}' if ln.startswith('guid: ') else ln for ln in lines]
    meta.write_text('\n'.join(lines) + '\n', encoding='ascii', newline='\n')
    return True


def write_folder_meta(folder):
    import uuid
    meta = Path(str(folder) + '.meta')
    if meta.exists():
        return False
    meta.write_text(
        'fileFormatVersion: 2\n'
        f'guid: {uuid.uuid4().hex}\n'
        'folderAsset: yes\n'
        'DefaultImporter:\n'
        '  externalObjects: {}\n'
        '  userData:\n'
        '  assetBundleName:\n'
        '  assetBundleVariant:\n', encoding='ascii', newline='\n')
    return True


def to_background(rgba):
    """Warp a merchant-frame RGBA onto the background frame (premultiplied bilinear)."""
    h, w = SIZE[1], SIZE[0]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float64)
    sx = (xx + 0.5 - MAP_OFFSET[0]) / MAP_SCALE[0] - 0.5
    sy = (yy + 0.5 - MAP_OFFSET[1]) / MAP_SCALE[1] - 0.5
    a = rgba[..., 3] / 255.0
    pre = rgba[..., :3] * a[..., None]
    inside = (sx > -1) & (sx < w) & (sy > -1) & (sy < h)
    A = map_coordinates(a, [sy, sx], order=1, mode='constant') * inside
    P = np.stack([map_coordinates(pre[..., c], [sy, sx], order=1, mode='constant') for c in range(3)], -1) * inside[..., None]
    return P, A


def over_plate(rgba, plate):
    P, A = to_background(rgba)
    return P + plate * (1 - A[..., None])


def label(img, text):
    from PIL import ImageDraw
    im = Image.fromarray(np.clip(np.rint(img), 0, 255).astype(np.uint8), 'RGB')
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 8 + 7 * len(text), 16], fill=(0, 0, 0))
    d.text((4, 2), text, fill=(255, 255, 255))
    return np.asarray(im).astype(np.float64)


def depth_view(depth):
    v = depth / 255.0
    rgb = np.stack([v, v ** 2, 1 - v], -1) * 255
    lines = np.abs(((v * 10) % 1) - 0.5) > 0.47
    rgb[lines] = 255
    return rgb


def contact_sheet(host, unlit, lantern, depth, response, canopy_unlit, canopy_lit, path, replace):
    amb = load('MerchantAmbientUnlit.png', 'RGB')
    lan = load('MerchantLanternOnly.png', 'RGB')
    cmask = canopy_mask()[..., None]

    # Shader approximation, linear light: lantern 1 = unlit + max(lanternlit - unlit, 0) for every
    # pair (background plates, canopy cloth, merchant). Draw order: plate, canopy, merchant.
    def lantern_on(u, l):
        ul, ll = to_linear(u), to_linear(l)
        return to_srgb(ul + np.maximum(ll - ul, 0))
    lit = unlit.copy()
    lit[..., :3] = lantern_on(unlit[..., :3], lantern[..., :3])
    bg_lit = lantern_on(amb, lan)
    canopy_on = lantern_on(canopy_unlit, canopy_lit)
    scene_on = over(np.concatenate([canopy_on, cmask], -1), bg_lit)
    scene_off = over(np.concatenate([canopy_unlit, cmask], -1), amb)
    scene_cur = over(np.concatenate([load('MerchantOriginal.png', 'RGB'), cmask], -1), bg_lit)
    box = (0, 300, 700, 1040)          # background-frame crop
    sl = (slice(box[1], box[3]), slice(box[0], box[2]))
    a = label(over_plate(lit, scene_on)[sl], '(a) lantern 1: unlit + (lanternlit - unlit)')
    b = label(over_plate(unlit, scene_off)[sl], '(b) lantern 0: unlit')
    c = label(over_plate(host, scene_cur)[sl], '(c) current art over lantern plate')
    dv = depth_view(depth[..., 0])
    alpha = host[..., 3:4] / 255.0
    d = label(over_plate(np.concatenate([dv, host[..., 3:4]], -1), amb * 0.35)[sl], 'MerchantDepth R inside alpha (blue far, yellow near, 0.1 contours)')
    rv = np.stack([response[..., 0], response[..., 1], np.zeros_like(response[..., 0])], -1)
    e = label(over_plate(np.concatenate([rv, host[..., 3:4]], -1), load('MerchantLanternResponse.png', 'RGB'))[sl],
              'MerchantLanternResponseHost inside alpha over background map (seamless = matching)')
    contrib = to_srgb(np.clip((to_linear(lit[..., :3]) - to_linear(unlit[..., :3])) * 2, 0, 1))
    f = label(over_plate(np.concatenate([contrib, host[..., 3:4]], -1), np.zeros_like(amb))[sl], 'merchant lantern contribution x2 (linear)')
    top = np.concatenate([a, b, c], 1)
    mid = np.concatenate([d, e, f], 1)
    # Canopy cloth, background frame (0, 0)-(700, 520): lantern 1, lantern 0, contribution.
    csl = (slice(0, 520), slice(0, 700))
    ccontrib = to_srgb(np.clip((to_linear(canopy_on) - to_linear(canopy_unlit)) * 2, 0, 1))
    canopy_row = np.concatenate([
        label(over_plate(lit, scene_on)[csl], 'canopy lantern 1 (lanternlit = MerchantOriginal cloth)'),
        label(over_plate(unlit, scene_off)[csl], 'canopy lantern 0 (CanopyUnlit)'),
        label(over(np.concatenate([ccontrib, cmask], -1), np.zeros_like(amb))[csl], 'canopy lantern contribution x2 (linear)')], 1)
    # Close crops at native merchant resolution x2: head/hat and shirt, (a) vs (b), with the current art.
    crops = []
    for (cx0, cy0, cx1, cy1), title in (((150, 440, 490, 760), 'head/hat'), ((300, 760, 640, 1000), 'shirt')):
        row = []
        for img, plate, name in ((lit, bg_lit, 'a'), (unlit, amb, 'b'), (host, bg_lit, 'current')):
            frame = plate_in_merchant_frame(plate)
            comp = over(img, frame)[cy0:cy1, cx0:cx1]
            comp = np.asarray(Image.fromarray(np.clip(np.rint(comp), 0, 255).astype(np.uint8)).resize(((cx1 - cx0) * 2, (cy1 - cy0) * 2), Image.LANCZOS)).astype(np.float64)
            row.append(label(comp, f'{title} ({name})'))
        crops.append(np.concatenate(row, 1))
    width = top.shape[1]

    def fit(img):
        if img.shape[1] == width:
            return img
        pad = np.zeros((img.shape[0], width, 3))
        pad[:, :min(width, img.shape[1])] = img[:, :width]
        return pad
    sheet = np.concatenate([top, mid, fit(canopy_row)] + [fit(cr) for cr in crops], 0)
    return save_png(path, sheet, 'RGB', replace)


def load_donor(path):
    return np.asarray(Image.open(path).convert('RGB')).astype(np.float64)


def stage_build(args):
    out_dir = Path(args.out) if args.out else OUT
    replace = args.replace
    host = load('MerchantHost.png')
    head = load('MerchantHeadRepaired.png')
    torso = load('MerchantTorsoAnimation.png')
    blink = load('MerchantBlinkEyes.png')
    head_repair = load('MerchantHeadRepairMask.png')[..., 3] > 0
    torso_repair = load('MerchantTorsoAnimationRepairMask.png')[..., 3] > 0
    report = {'ratio_sigma_px': RATIO_SIGMA, 'ratio_erode_px': RATIO_ERODE, 'ratio_clamp': RATIO_CLAMP}

    donors = {'unlit': Path(args.donor_unlit) if args.donor_unlit else DONORS['unlit'],
              'lantern': Path(args.donor_lantern) if args.donor_lantern else DONORS['lantern']}
    plates = {'unlit': 'MerchantAmbientUnlit.png', 'lantern': 'MerchantLanternOnly.png'}
    ratios = {}
    for key in ('unlit', 'lantern'):
        donor = load_donor(donors[key])
        plate = plate_in_merchant_frame(load(plates[key], 'RGB'))
        du, dv, reg = register(donor, over(host, plate), host[..., 3])
        registered = sample_donor(donor, du, dv)
        reg['donor'] = donors[key].resolve().relative_to(ROOT).as_posix()
        report[f'registration_{key}'] = reg
        if args.save_registered:
            save_png(WORK / 'registered' / f'{key}-registered.png', registered, 'RGB', True)
        ratios[key] = np.concatenate([ratio_map(registered, host), highlight_cap(registered, host)], -1)
    # Lantern light only adds to the night ambient: lanternlit >= unlit in every channel.
    clamp = relit_linear(host, ratios['lantern']) < relit_linear(host, ratios['unlit'])
    inside = host[..., 3] > 0
    report['lantern_raised_to_unlit_fraction_inside_alpha_per_channel_rgb'] = [float(v) for v in clamp[inside].mean(0)]
    for key in ('unlit', 'lantern'):
        r = ratios[key]
        capped = np.any(np.abs(relit_linear(host, r) - to_linear(host[..., :3]) * r[..., :3]) > 1e-9, -1) & inside
        report[f'highlight_or_saturation_cap_active_pixels_{key}'] = int(capped.sum())
    # The lantern field carries the unlit field as a floor: lantern-lit = max(lantern, unlit) per channel.
    ratios['lantern'] = np.concatenate([ratios['lantern'], ratios['unlit']], -1)

    outputs = {}
    measures = []
    for key, suffix in (('unlit', 'Unlit'), ('lantern', 'LanternLit')):
        r = ratios[key]
        host_out = apply_ratio(host, r)
        outputs[f'MerchantHost{suffix}.png'] = (host_out, COLOR_META)
        measures.append(measure(f'MerchantHost{suffix}.png', host, host_out))
        # Head and torso layers: the host ratio on visible pixels, a harmonic fill on concealed repairs.
        r_head = membrane_fill(r, head_repair)
        r_torso = membrane_fill(r, torso_repair)
        head_out = apply_ratio(head, r_head)
        torso_out = apply_ratio(torso, r_torso)
        outputs[f'MerchantHeadRepaired{suffix}.png'] = (head_out, COLOR_META)
        outputs[f'MerchantTorsoAnimation{suffix}.png'] = (torso_out, COLOR_META)
        measures.append(measure(f'MerchantHeadRepaired{suffix}.png', head, head_out))
        measures.append(measure(f'MerchantTorsoAnimation{suffix}.png', torso, torso_out))
        x0, y0, x1, y1 = BLINK_BOX
        blink_out = apply_ratio(blink, r_head[y0:y1, x0:x1])
        outputs[f'MerchantBlinkEyes{suffix}.png'] = (blink_out, COLOR_META)
        measures.append(measure(f'MerchantBlinkEyes{suffix}.png', blink, blink_out))
        # Seam consistency: wherever a layer's source equals MerchantHost, its output equals the host output.
        for lname, src, dst, repair in (('head', head, head_out, head_repair), ('torso', torso, torso_out, torso_repair)):
            same = np.all(src == host, -1) & (host[..., 3] > 0)
            report[f'seam_{lname}_{key}_repair_pixels_equal_to_host_source_by_coincidence'] = int((same & repair).sum())
            same &= ~repair
            report[f'seam_{lname}_{key}_pixels_equal_to_host_source'] = int(same.sum())
            report[f'seam_{lname}_{key}_of_those_differing_from_host_output'] = int((np.any(dst != host_out, -1) & same).sum())
            report[f'{lname}_repair_pixels_{key}'] = int(repair.sum())
        if key == 'unlit':
            unlit_out = host_out
        else:
            lantern_out = host_out
    # Lantern contribution stays non-negative after 8-bit rounding.
    neg = (lantern_out[..., :3] < unlit_out[..., :3]) & (host[..., 3:4] > 0)
    report['lanternlit_below_unlit_channel_values_inside_alpha'] = int(neg.sum())

    # Canopy cloth (background frame). Lantern-lit is the cloth as painted in MerchantOriginal.png;
    # unlit is registered to the canopy target and transferred inside the dilated overhang mask.
    # The lantern only adds light, so unlit must not exceed the painted cloth in any channel: each
    # relit pixel keeps the donor's cool hue and is scaled uniformly (scale smoothed over
    # CANOPY_FIT_SIGMA px against quantization in near-black channels) to fit under the painted
    # value; a final per-channel minimum removes any residue of the smoothing. Lantern 1 then
    # reproduces MerchantOriginal.png exactly.
    from scipy.ndimage import binary_dilation
    original = load('MerchantOriginal.png', 'RGB')
    cmask = canopy_mask()
    cdonor_path = Path(args.donor_canopy) if args.donor_canopy else DONORS['canopy_unlit']
    cdonor = load_donor(cdonor_path)
    csrc = canopy_composite(load('MerchantAmbientUnlit.png', 'RGB'), unlit_out)
    du, dv, creg = register(cdonor, csrc, cmask, CANOPY_BOX)
    creg['donor'] = cdonor_path.resolve().relative_to(ROOT).as_posix()
    report['registration_canopy_unlit'] = creg
    cregistered = sample_donor(cdonor, du, dv)
    if args.save_registered:
        save_png(WORK / 'registered' / 'canopy-unlit-registered.png', cregistered, 'RGB', True)
    cloth = np.concatenate([original, cmask[..., None]], -1)
    cfield = np.concatenate([ratio_map(cregistered, cloth), highlight_cap(cregistered, cloth)], -1)
    region = binary_dilation(cmask > 0, iterations=CANOPY_DILATE)
    apply_rgba = np.concatenate([original, region[..., None] * 255.0], -1)
    clin = relit_linear(apply_rgba, cfield)
    olin = to_linear(original)
    fit = np.minimum(1.0, (olin / np.maximum(clin, 1e-6)).min(-1))
    rw = region.astype(np.float64)
    fit_s = gaussian_filter(fit * rw, CANOPY_FIT_SIGMA) / np.maximum(gaussian_filter(rw, CANOPY_FIT_SIGMA), 1e-6)
    lum = np.array([0.2126, 0.7152, 0.0722])
    report['canopy_unlit_fit_scale_mean'] = float(fit_s[region].mean())
    report['canopy_unlit_luminance_vs_painted_mean'] = float(((clin * fit_s[..., None]) @ lum)[region].mean() / (olin @ lum)[region].mean())
    fitted = clin * fit_s[..., None]
    report['canopy_unlit_residual_channel_minimum_fraction'] = float((fitted > olin)[region].mean())
    clin = np.minimum(fitted, olin)
    canopy_unlit = original.copy()
    canopy_unlit[region] = np.rint(to_srgb(clin))[region]
    report['canopy_unlit_above_painted_channel_values'] = int((canopy_unlit > original)[region].sum())
    canopy_lit = original.copy()
    outputs['CanopyUnlit.png'] = (canopy_unlit, COLOR_META)
    outputs['CanopyLanternLit.png'] = (canopy_lit, COLOR_META)
    for name, img in (('CanopyUnlit.png', canopy_unlit), ('CanopyLanternLit.png', canopy_lit)):
        changed = np.any(img != original, -1)
        report[name] = {
            'mask_pixels': int((cmask > 0).sum()), 'transfer_region_pixels': int(region.sum()),
            'pixels_changed': int(changed.sum()), 'pixels_changed_outside_region': int((changed & ~region).sum()),
        }
    lr = load('MerchantLanternResponse.png', 'RGB')
    yb, xb = np.mgrid[0:SIZE[1], 0:SIZE[0]].astype(np.float64) + 0.5
    report['canopy_response'] = {
        'MerchantLanternResponse_G_max_on_cloth': float(lr[..., 1][cmask > 0].max()),
        'MerchantLanternResponse_R_range_on_cloth': [float(lr[..., 0][cmask > 0].min()), float(lr[..., 0][cmask > 0].max())],
        'R_model_max_abs_diff_levels_on_cloth': float(np.abs(lantern_distance(xb, yb) * 255 - lr[..., 0])[cmask > 0].max()),
    }

    depth = author_depth()
    response = author_response()
    report['response'] = verify_response(response)
    (lx, ly), (rx, ry) = lantern_in_merchant_frame()
    report['response']['lantern_merchant_frame_px'] = [lx, ly]
    report['response']['radius_merchant_frame_px'] = [rx, ry]
    report['response']['lantern_background_px'] = list(LANTERN_BG)
    report['response']['radius_background_px'] = list(LANTERN_RADIUS_BG)
    outputs['MerchantDepth.png'] = (depth, DATA_META)
    outputs['MerchantLanternResponseHost.png'] = (response, DATA_META)
    a = host[..., 3] > 0
    report['depth'] = {
        'min_inside_alpha': float(np.rint(depth[..., 0])[a].min()), 'max_inside_alpha': float(np.rint(depth[..., 0])[a].max()),
        'max_neighbour_step_inside_alpha_levels': float(max(np.abs(np.diff(np.rint(depth[..., 0]), axis=0))[a[1:] & a[:-1]].max(),
                                                         np.abs(np.diff(np.rint(depth[..., 0]), axis=1))[a[:, 1:] & a[:, :-1]].max())),
    }

    out_dir.mkdir(parents=True, exist_ok=True)
    created_meta = []
    if out_dir == OUT and write_folder_meta(OUT):
        created_meta.append(str(OUT) + '.meta')
    for name, (img, template) in outputs.items():
        mode = 'RGBA' if img.shape[2] == 4 else 'RGB'
        path = save_png(out_dir / name, img, mode, replace)
        if out_dir == OUT and write_meta(path, template):
            created_meta.append(str(path) + '.meta')
    # Read back the written files to verify the measurements on disk.
    for m in measures:
        disk = np.asarray(Image.open(out_dir / m['texture']).convert('RGBA')).astype(np.float64)
        src = {'MerchantHost': host, 'MerchantHeadRepaired': head, 'MerchantTorsoAnimation': torso, 'MerchantBlinkEyes': blink}
        base = next(v for k, v in src.items() if m['texture'].startswith(k + 'Unlit') or m['texture'].startswith(k + 'LanternLit'))
        m['on_disk'] = measure(m['texture'], base, disk)
    report['textures'] = measures
    for name in ('CanopyUnlit.png', 'CanopyLanternLit.png'):
        disk = np.asarray(Image.open(out_dir / name).convert('RGB')).astype(np.float64)
        changed = np.any(disk != original, -1)
        report[name]['on_disk_pixels_changed'] = int(changed.sum())
        report[name]['on_disk_pixels_changed_outside_region'] = int((changed & ~region).sum())
        report[name]['on_disk_mode'] = Image.open(out_dir / name).mode
    report['created_meta'] = created_meta
    sheet = contact_sheet(host, unlit_out, lantern_out, depth, response, canopy_unlit, canopy_lit,
                          Path(args.sheet) if args.sheet else WORK / 'contact-sheet.png', True)
    report['contact_sheet'] = str(sheet)
    manifest = Path(args.manifest) if args.manifest else WORK / 'relight-build.json'
    manifest.write_text(json.dumps(report, indent=2), encoding='ascii')
    print(json.dumps(report, indent=2))


# ---------------------------------------------------------------------------------------------
# Hanging cup (HangingCup.prefab image, its own 1024 x 1536 frame). The cup is drawn at a fixed
# rest rect in the plate frame: HangingCup root at 540 x 960 frame (370, 144) from the top-left,
# Cup image 74 x 111 frame units with pivot (0.506, 0.94), converted at 941 / 540 and 1672 / 960.
# Edit targets composite the cup over the scene it hangs in, sampled at that rect: the
# props-removed plate, the hanging pot and pan cutouts and the canopy cloth. The cup's donors are
# registered in the cup frame (box = the whole frame) and transferred through the same ratio maps,
# at CUP_RATIO_SIGMA because the cup texture is drawn at about 1/8 of its native resolution.
CUP_WORK = WORK / 'cup'
CUP_FRAME_UNITS = (370.0, 144.0, 74.0, 111.0, 0.506, 0.94)
CUP_RATIO_SIGMA = 6.0
CUP_DONORS = {
    'unlit': REF / 'HauntedMerchantRelightCupUnlitDonor.png',
    'lantern': REF / 'HauntedMerchantRelightCupLanternDonor.png',
}
CUP_PLATES = {
    'unlit': ('Props/MerchantAmbientUnlitPropsRemoved.png', 'Unlit', 'Relight/CanopyUnlit.png'),
    'lantern': ('Props/MerchantLanternOnlyPropsRemoved.png', 'LanternLit', 'Relight/CanopyLanternLit.png'),
}


def cup_rest_rect():
    """The cup image's rest rect in plate pixels (x, y, w, h), top-left origin."""
    fx, fy, fw, fh, px, py = CUP_FRAME_UNITS
    sx, sy = SIZE[0] / 540.0, SIZE[1] / 960.0
    return ((fx - px * fw) * sx, (fy - (1 - py) * fh) * sy, fw * sx, fh * sy)


def cup_map_rect():
    """The rest rect as a SceneLitLayer MapRect: normalized bottom-left map coordinates."""
    x, y, w, h = cup_rest_rect()
    return (x / SIZE[0], 1 - (y + h) / SIZE[1], w / SIZE[0], h / SIZE[1])


def cup_scene(key):
    """The scene behind the cup at its rest rect (background frame RGB): plate, pot, pan, canopy."""
    plate_name, suffix, canopy_name = CUP_PLATES[key]
    scene = load(plate_name, 'RGB')
    props = json.loads((ART / 'Props/props.json').read_text(encoding='ascii'))
    for piece in sorted(props['pieces'], key=lambda p: p['draw_order']):
        if piece['name'] not in ('HangingPot', 'HangingPan'):
            continue
        cut = load(f"Props/{piece['name']}{suffix}.png")
        r = piece['crop_rect']
        sl = (slice(r['y'], r['y'] + r['h']), slice(r['x'], r['x'] + r['w']))
        scene[sl] = over(cut, scene[sl])
    cloth = np.concatenate([load(canopy_name, 'RGB'), canopy_mask()[..., None]], -1)
    return over(cloth, scene)


def cup_backdrop(key, shape):
    """The cup-frame view of cup_scene(key) (bilinear), one cup texel per output pixel."""
    x, y, w, h = cup_rest_rect()
    scene = cup_scene(key)
    yy, xx = np.mgrid[0:shape[0], 0:shape[1]].astype(np.float64)
    bx = x + (xx + 0.5) * w / shape[1] - 0.5
    by = y + (yy + 0.5) * h / shape[0] - 0.5
    return np.stack([map_coordinates(scene[..., c], [by, bx], order=1, mode='nearest') for c in range(3)], -1)


def cup_target(cup, key, path, replace):
    comp = over(cup, cup_backdrop(key, cup.shape[:2]))
    return save_png(path, comp, 'RGB', replace)


def stage_cup_targets(args):
    """Edit targets in the cup frame: the painted cup over the unlit scene (unlit donor) and, once
    HangingCupUnlit.png exists, the unlit cup over the lantern-lit scene (lantern donor)."""
    cup = load('HangingCup.png')
    print(cup_target(cup, 'unlit', CUP_WORK / 'targets' / 'target-cup-unlit.png', args.replace))
    unlit = (Path(args.out) if args.out else OUT) / 'HangingCupUnlit.png'
    if unlit.exists():
        rgba = np.asarray(Image.open(unlit).convert('RGBA')).astype(np.float64)
        print(cup_target(rgba, 'lantern', CUP_WORK / 'targets' / 'target-cup-lantern-from-unlit.png', args.replace))


def cup_in_plate(rgba, scene):
    """Composite a cup-frame RGBA over a background-frame RGB scene at the rest rect (premultiplied
    area resampling, the cup's bilinear minification approximated by a box filter)."""
    x, y, w, h = cup_rest_rect()
    x0, y0 = int(np.floor(x)), int(np.floor(y))
    x1, y1 = int(np.ceil(x + w)), int(np.ceil(y + h))
    a = rgba[..., 3] / 255.0
    pre = np.concatenate([rgba[..., :3] * a[..., None], a[..., None] * 255.0], -1)
    # Supersample the target pixels and box-average the bilinear samples of the cup texture.
    k = 4
    yy, xx = np.mgrid[0:(y1 - y0) * k, 0:(x1 - x0) * k].astype(np.float64)
    px = x0 + (xx + 0.5) / k
    py = y0 + (yy + 0.5) / k
    u = (px - x) / w * rgba.shape[1] - 0.5
    v = (py - y) / h * rgba.shape[0] - 0.5
    inside = (px >= x) & (px < x + w) & (py >= y) & (py < y + h)
    s = np.stack([map_coordinates(pre[..., c], [v, u], order=1, mode='nearest') * inside for c in range(4)], -1)
    s = s.reshape(y1 - y0, k, x1 - x0, k, 4).mean((1, 3))
    out = scene.copy()
    region = out[y0:y1, x0:x1]
    out[y0:y1, x0:x1] = s[..., :3] + region * (1 - s[..., 3:4] / 255.0)
    return out


def cup_contact_sheet(cup, unlit, lantern, path):
    """Plate-frame crops around the cup, x3: lantern 1 (unlit + max(lanternlit - unlit, 0)) over the
    lantern-lit scene, lantern 0 over the unlit scene, the painted cup over the lantern-lit scene and
    the lantern contribution x4; then the cup frame at half size: painted, unlit, lantern-lit."""
    def lantern_on(u, l):
        ul, ll = to_linear(u), to_linear(l)
        return to_srgb(ul + np.maximum(ll - ul, 0))
    on_scene = lantern_on(cup_scene('unlit'), cup_scene('lantern'))
    off_scene = cup_scene('unlit')
    lit = unlit.copy()
    lit[..., :3] = lantern_on(unlit[..., :3], lantern[..., :3])
    contrib = unlit.copy()
    contrib[..., :3] = to_srgb(np.clip((to_linear(lantern[..., :3]) - to_linear(unlit[..., :3])) * 4, 0, 1))
    box = (500, 200, 760, 520)
    sl = (slice(box[1], box[3]), slice(box[0], box[2]))

    def up(img, text):
        im = Image.fromarray(np.clip(np.rint(img[sl]), 0, 255).astype(np.uint8), 'RGB')
        im = im.resize(((box[2] - box[0]) * 3, (box[3] - box[1]) * 3), Image.LANCZOS)
        return label(np.asarray(im).astype(np.float64), text)
    row = np.concatenate([
        up(cup_in_plate(lit, on_scene), 'lantern 1'),
        up(cup_in_plate(unlit, off_scene), 'lantern 0'),
        up(cup_in_plate(cup, on_scene), 'painted cup, lantern 1 scene'),
        up(cup_in_plate(contrib, np.zeros_like(on_scene)), 'cup lantern contribution x4 (linear)')], 1)
    grey = np.full(cup.shape[:2] + (3,), 96.0)
    frames = []
    for img, text in ((cup, 'HangingCup.png'), (unlit, 'HangingCupUnlit.png'), (lantern, 'HangingCupLanternLit.png')):
        comp = Image.fromarray(np.clip(np.rint(over(img, grey)), 0, 255).astype(np.uint8), 'RGB')
        comp = comp.crop((200, 0, 820, 1536)).resize((310, 768), Image.LANCZOS)
        frames.append(label(np.asarray(comp).astype(np.float64), text))
    frames = np.concatenate(frames, 1)
    pad = np.zeros((frames.shape[0], row.shape[1], 3))
    pad[:, :frames.shape[1]] = frames
    return save_png(path, np.concatenate([row, pad], 0), 'RGB', True)


def stage_cup(args):
    """Relight HangingCup.png: register the preserved cup donors in the cup frame, derive the ratio
    maps and write HangingCupUnlit.png and (once its donor exists) HangingCupLanternLit.png."""
    out_dir = Path(args.out) if args.out else OUT
    cup = load('HangingCup.png')
    shape = cup.shape[:2]
    box = (0, 0, shape[1], shape[0])
    inside = cup[..., 3] > 0
    x, y, w, h = cup_rest_rect()
    report = {'ratio_sigma_px': CUP_RATIO_SIGMA, 'ratio_erode_px': RATIO_ERODE, 'ratio_clamp': RATIO_CLAMP,
              'rest_rect_plate_px': [x, y, w, h], 'map_rect': list(cup_map_rect())}
    donors = {'unlit': Path(args.donor_unlit) if args.donor_unlit else CUP_DONORS['unlit'],
              'lantern': Path(args.donor_lantern) if args.donor_lantern else CUP_DONORS['lantern']}
    fields = {}
    outputs = {}
    for key in ('unlit', 'lantern'):
        if not donors[key].exists():
            if key == 'lantern':
                print(f'No lantern donor at {donors[key]}; writing the unlit cup only.')
                break
            raise SystemExit(f'Missing donor {donors[key]}')
        donor = load_donor(donors[key])
        # The edit target each donor regenerates: the painted cup over the unlit scene, and the
        # unlit result over the lantern-lit scene.
        shown = cup if key == 'unlit' else outputs['unlit']
        du, dv, reg = register(donor, over(shown, cup_backdrop(key, shape)), cup[..., 3], box)
        reg['donor'] = donors[key].resolve().relative_to(ROOT).as_posix()
        report[f'registration_{key}'] = reg
        registered = sample_donor(donor, du, dv)
        if args.save_registered:
            save_png(CUP_WORK / 'registered' / f'cup-{key}-registered.png', registered, 'RGB', True)
        fields[key] = np.concatenate([ratio_map(registered, cup, CUP_RATIO_SIGMA), highlight_cap(registered, cup)], -1)
        if key == 'lantern':
            clamp = relit_linear(cup, fields['lantern']) < relit_linear(cup, fields['unlit'])
            report['lantern_raised_to_unlit_fraction_inside_alpha_per_channel_rgb'] = [float(v) for v in clamp[inside].mean(0)]
        r = fields[key]
        capped = np.any(np.abs(relit_linear(cup, r) - to_linear(cup[..., :3]) * r[..., :3]) > 1e-9, -1) & inside
        report[f'highlight_or_saturation_cap_active_pixels_{key}'] = int(capped.sum())
        # Lantern light only adds to the night ambient: the unlit field is the lantern field's floor.
        field = r if key == 'unlit' else np.concatenate([r, fields['unlit']], -1)
        outputs[key] = apply_ratio(cup, field)

    names = {'unlit': 'HangingCupUnlit.png', 'lantern': 'HangingCupLanternLit.png'}
    out_dir.mkdir(parents=True, exist_ok=True)
    created_meta = []
    for key, img in outputs.items():
        path = save_png(out_dir / names[key], img, 'RGBA', args.replace)
        if out_dir == OUT and write_meta(path, 'HangingCup.png.meta'):
            created_meta.append(str(path) + '.meta')
    report['created_meta'] = created_meta
    # Measurements read back from the written files.
    disk = {key: np.asarray(Image.open(out_dir / names[key]).convert('RGBA')).astype(np.float64) for key in outputs}
    report['textures'] = [measure(names[key], cup, disk[key]) for key in disk]
    lum = np.array([0.2126, 0.7152, 0.0722])
    solid = cup[..., 3] > 250
    report['painted_linear_rgb_mean_alpha_gt_250'] = [float(v) for v in to_linear(cup[..., :3])[solid].mean(0)]
    for key in disk:
        lin = to_linear(disk[key][..., :3])
        report[f'{key}_linear_rgb_mean_alpha_gt_250'] = [float(v) for v in lin[solid].mean(0)]
        report[f'{key}_luminance_vs_painted_mean'] = float((lin @ lum)[solid].mean() / (to_linear(cup[..., :3]) @ lum)[solid].mean())
    if 'lantern' in disk:
        u, l = disk['unlit'], disk['lantern']
        report['lanternlit_below_unlit_channel_values_inside_alpha'] = int(((l[..., :3] < u[..., :3]) & inside[..., None]).sum())
        contrib = to_linear(l[..., :3]) - to_linear(u[..., :3])
        report['cup_lantern_contribution_linear_rgb_mean_alpha_gt_250'] = [float(v) for v in contrib[solid].mean(0)]
        report['cup_lantern_ratio_lit_over_unlit_linear_rgb'] = [
            float(v) for v in to_linear(l[..., :3])[solid].mean(0) / to_linear(u[..., :3])[solid].mean(0)]
        # Reference: the plates and the hanging pan and pot (the nearest lit props) at the cup.
        plate = to_linear(load(CUP_PLATES['lantern'][0], 'RGB')) - to_linear(load(CUP_PLATES['unlit'][0], 'RGB'))
        rx0, ry0, rx1, ry1 = int(round(x)), int(round(y)), int(round(x + w)), int(round(y + h))
        report['plate_lantern_contribution_linear_rgb_mean_in_rest_rect'] = [float(v) for v in plate[ry0:ry1, rx0:rx1].reshape(-1, 3).mean(0)]
        for name in ('HangingPan', 'HangingPot'):
            pu, pl = load(f'Props/{name}Unlit.png'), load(f'Props/{name}LanternLit.png')
            m = pu[..., 3] > 250
            report[f'{name}_linear_rgb_unlit_mean'] = [float(v) for v in to_linear(pu[..., :3])[m].mean(0)]
            report[f'{name}_lantern_ratio_lit_over_unlit_linear_rgb'] = [
                float(v) for v in to_linear(pl[..., :3])[m].mean(0) / to_linear(pu[..., :3])[m].mean(0)]
        resp = load('MerchantLanternResponse.png', 'RGB')
        report['response_R_range_in_rest_rect'] = [float(resp[ry0:ry1, rx0:rx1, 0].min()), float(resp[ry0:ry1, rx0:rx1, 0].max())]
        report['response_G_max_in_rest_rect'] = float(resp[ry0:ry1, rx0:rx1, 1].max())
        sheet = cup_contact_sheet(cup, disk['unlit'], disk['lantern'], Path(args.sheet) if args.sheet else CUP_WORK / 'contact-sheet.png')
        report['contact_sheet'] = str(sheet)
    manifest = Path(args.manifest) if args.manifest else CUP_WORK / 'cup-build.json'
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(json.dumps(report, indent=2), encoding='ascii')
    print(json.dumps(report, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('stage', choices=['targets', 'build', 'cup-targets', 'cup'])
    parser.add_argument('--replace', action='store_true', help='Allow replacing this tool\'s own earlier outputs.')
    parser.add_argument('--out', help='Texture output folder (default: the installed Relight folder).')
    parser.add_argument('--donor-unlit', help='Override the preserved unlit donor (candidate review).')
    parser.add_argument('--donor-lantern', help='Override the preserved lantern donor (candidate review).')
    parser.add_argument('--donor-canopy', help='Override the preserved canopy unlit donor (candidate review).')
    parser.add_argument('--sheet', help='Contact sheet path (default Docs/work/merchant-relight/contact-sheet.png).')
    parser.add_argument('--manifest', help='Build report path (default Docs/work/merchant-relight/relight-build.json).')
    parser.add_argument('--save-registered', action='store_true', help='Also write the registered donors under Docs/work.')
    args = parser.parse_args()
    if args.stage == 'targets':
        stage_targets(args)
    elif args.stage == 'cup-targets':
        stage_cup_targets(args)
    elif args.stage == 'cup':
        stage_cup(args)
    else:
        stage_build(args)


if __name__ == '__main__':
    main()
