#!/usr/bin/env python3
"""Cut movable poltergeist props out of the haunted merchant background plates.

Pipeline (numpy + Pillow only):
  1. Rasterise each prop piece's silhouette from declared vector primitives (8x supersampled
     coverage gives the anti-aliased alpha). Unlit and lantern-lit cutouts share this alpha.
  2. Register each generated donor (imagegen edits of an upscaled crop of
     MerchantAmbientUnlit.png, kept in Docs/work/merchant-props/donors) to the native plate
     frame with an affine fit on high-passed luminance around the props (props excluded).
  3. Transfer donor pixels only inside the declared patch masks (silhouette dilated by the
     piece's maximum motion plus one pixel), after a low-frequency colour match to the
     surrounding plate, with a linear boundary blend. Pixels outside the masks are copied
     byte for byte.
  4. Derive the lantern-lit patch from the unlit patch with a masked local linear model
     lit = a * unlit + b fitted on surrounding plate pixels (guided-filter style), so both
     patched plates follow the same reconstruction.
  5. Cut each piece from its plate. Colours at partial-coverage edges are un-premultiplied
     against the layers drawn below, so compositing the pieces back over the patched plate at
     rest reproduces the original plate inside the silhouettes. Regions hidden behind a piece
     drawn in front are completed from a second donor and declared.
  6. Verify byte identity outside the masks, identical alpha, at-rest and displaced
     composites, then write props.json, the .meta files and the contact sheet.

Usage: cut-merchant-props.py [--no-sheet]
Run with any Python that has numpy and Pillow.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import uuid

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Dice-Unity/Assets/Dice/Art/HauntedMerchant'
PROPS = ART / 'Props'
WORK = ROOT / 'Docs/work/merchant-props'
UNLIT_PLATE = ART / 'MerchantAmbientUnlit.png'
LIT_PLATE = ART / 'MerchantLanternOnly.png'
META_TEMPLATE = ART / 'MerchantAmbientUnlit.png.meta'
W, H = 941, 1672
SS = 8  # silhouette supersampling

# ---------------------------------------------------------------------------------------------
# Edit regions. Each donor is an imagegen edit of target box (x0, y0, x1, y1) of the unlit plate,
# upscaled to 1024 x 1536 with Lanczos (targets/<region>-target.png).
REGIONS = {
    'pots': {'box': (520, 252, 712, 540),
             'clean': 'pots-remove-all-1.png',
             'completion': 'pots-remove-pan-1.png',
             'window': (548, 292, 700, 505)},
    'bottle': {'box': (160, 280, 300, 490),
               'clean': 'bottle-remove-1.png',
               'window': (190, 300, 270, 405)},
    'trunk': {'box': (140, 360, 290, 585),
              'clean': 'trunk-lid-remove-1.png',
              'window': (150, 392, 275, 492)},
}

# Pieces in draw order (back to front). Coordinates are plate pixels, top-left origin; pixel
# (i, j) covers [i, i + 1) x [j, j + 1). Primitive kinds: ellipse (cx, cy, rx, ry, degrees),
# poly (points), line (points, width).
PIECES = [
    {'name': 'HangingPot', 'region': 'pots', 'kind': 'swing', 'motion_fraction': 0.06,
     'pivot': (614.5, 411.5), 'pivot_note': 'top of the hanging ring on the post (hang point)',
     'prims': [('ellipse', (624.6, 457.6, 21.0, 31.6, 8.0)),
               ('line', [(614.5, 412.0), (614.8, 420.0), (615.5, 428.0)], 2.4)]},
    # Removal only, not a movable prop: HangingCup.prefab (540 x 960 frame position (370, 144)
    # from the top-left, 74 x 111, pivot (0.506, 0.94), +-3 degree swing) hangs its cup directly
    # over this painted cage basket, so the plate would show two hanging vessels in one place.
    {'name': 'HangingCage', 'region': 'pots', 'kind': 'removal', 'motion_fraction': 0.0,
     'pivot': (643.5, 305.0), 'pivot_note': 'top of the strap bracket on the post arm (former hang point)',
     'removal_reason': 'painted cage basket under the HangingCup.prefab cup footprint',
     'prims': [('poly', [(638.5, 305.0), (648.5, 305.0), (649.5, 320.0), (648.0, 333.0), (639.5, 333.0), (638.0, 320.0)]),
               ('line', [(644.0, 332.0), (644.0, 347.0)], 2.4),
               ('line', [(641.0, 330.5), (622.0, 350.5)], 3.0),
               ('line', [(647.0, 325.5), (679.5, 350.5)], 3.6),
               ('ellipse', (651.0, 350.2, 28.6, 4.6, 0.0)),
               ('line', [(678.5, 351.5), (672.0, 376.0)], 3.0),
               ('line', [(625.0, 352.5), (626.0, 368.0)], 2.2),
               ('line', [(644.0, 352.0), (644.0, 362.0)], 3.0),
               ('poly', [(626, 366), (632, 362), (645, 360.2), (660, 361), (668, 364), (672, 369), (672, 398),
                         (669.5, 410), (669, 420), (661, 425), (640, 425.5), (628.5, 423.5), (625.5, 418), (624, 370)])]},
    {'name': 'HangingPan', 'region': 'pots', 'kind': 'swing', 'motion_fraction': 0.06,
     'pivot': (595.0, 373.5), 'pivot_note': 'top of the ring hooked over the post (hang point)',
     'prims': [('ellipse', (592.8, 419.6, 32.6, 35.6, -18.0)),
               ('line', [(590.5, 378.0), (595.0, 373.5), (600.5, 376.0), (601.0, 385.0)], 3.2)]},
    {'name': 'ShelfBottle', 'region': 'bottle', 'kind': 'wobble', 'motion_fraction': 0.03,
     'pivot': (229.5, 383.2), 'pivot_note': 'centre of the base where it stands on the shelf',
     'contacts': [(216.6, 383.2), (242.4, 383.2)],
     'prims': [('poly', [(227.5, 323.5), (234.0, 323.5), (234.2, 327.0), (233.2, 328.5), (233.2, 341.0), (236.5, 343.0),
                         (240.2, 346.5), (242.6, 351.0), (243.4, 356.0), (243.4, 378.0), (242.4, 381.5), (239.0, 383.2),
                         (220.0, 383.2), (216.6, 381.5), (215.6, 378.0), (215.6, 356.0), (216.4, 351.0), (219.0, 346.5),
                         (223.0, 343.0), (228.2, 341.0), (228.2, 328.5), (227.2, 327.0)])]},
    {'name': 'TrunkLid', 'region': 'trunk', 'kind': 'lid', 'motion_fraction': 0.03,
     'pivot': (213.0, 409.0), 'pivot_note': 'middle of the back (hinge) edge of the lid top',
     'hinge': [(170.0, 411.5), (257.0, 423.0)],
     # Front seam where the lid meets the trunk body. Under the lid the patched plates show the
     # open trunk: the body's front rim along this seam and the dark interior above it, so a
     # lifted lid opens a dark gap rather than sliding over planks.
     'seam': [(169.5, 453.5), (200.0, 455.0), (216.5, 457.0), (233.0, 458.5), (259.0, 462.5)],
     'prims': [('poly', [(169.5, 412.0), (175.0, 408.5), (196.0, 407.5), (202.0, 405.5), (215.0, 404.0), (227.0, 405.0),
                         (231.0, 411.0), (245.0, 417.0), (256.0, 422.5), (258.5, 425.0), (259.0, 462.5), (233.0, 458.5),
                         (233.0, 476.0), (226.0, 478.0), (217.5, 475.0), (216.5, 457.0), (200.0, 455.0), (169.5, 453.5)])]},
]
MIN_MOTION = 3.0   # px; smallest motion allowance for any moving piece
REMOVAL_DILATION = 6  # px; patch margin around a removal-only silhouette
SAFETY = 1.0       # px added to the motion for bilinear sampling at the moved edge
CROP_MARGIN = 3    # px of transparent margin around each cutout


# ------------------------------------------------------------------------------- image helpers
def load_rgb(p):
    return np.asarray(Image.open(p).convert('RGB')).astype(np.float64)


def to_u8(a):
    return np.clip(np.round(a), 0, 255).astype(np.uint8)


def box_blur_axis(a, r, axis):
    if r <= 0:
        return a
    n = a.shape[axis]
    pad = [(0, 0)] * a.ndim
    pad[axis] = (r + 1, r)
    p = np.pad(a, pad, mode='edge')
    c = np.cumsum(p, axis=axis)
    hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
    lo = np.take(c, np.arange(0, n), axis=axis)
    return (hi - lo) / (2 * r + 1)


def blur(a, sigma):
    """Gaussian approximation by three box passes per axis."""
    if sigma <= 0.3:
        return a
    r = max(1, int(round((math.sqrt(12 * sigma * sigma / 3 + 1) - 1) / 2)))
    out = a
    for axis in (0, 1):
        for _ in range(3):
            out = box_blur_axis(out, r, axis)
    return out


def nblur(v, w, sigma):
    """Normalised (masked) blur; returns value and weight."""
    if v.ndim == 3:
        num = blur(v * w[..., None], sigma)
        den = blur(w, sigma)
        return num / np.maximum(den, 1e-9)[..., None], den
    num = blur(v * w, sigma)
    den = blur(w, sigma)
    return num / np.maximum(den, 1e-9), den


def bilinear(img, u, v):
    """Sample img (H, W[, C]) at float pixel-index coordinates u (x), v (y); clamped edges."""
    h, w = img.shape[:2]
    u = np.clip(u, 0, w - 1.000001)
    v = np.clip(v, 0, h - 1.000001)
    x0 = np.floor(u).astype(int)
    y0 = np.floor(v).astype(int)
    fx = u - x0
    fy = v - y0
    if img.ndim == 3:
        fx = fx[..., None]
        fy = fy[..., None]
    a = img[y0, x0]
    b = img[y0, x0 + 1]
    c = img[y0 + 1, x0]
    d = img[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def lum(a):
    return a @ np.array([0.299, 0.587, 0.114])


def nelder_mead(f, x0, step, iters=600, tol=1e-7):
    n = len(x0)
    pts = [np.array(x0, float)]
    for i in range(n):
        p = np.array(x0, float)
        p[i] += step[i]
        pts.append(p)
    vals = [f(p) for p in pts]
    for _ in range(iters):
        order = np.argsort(vals)
        pts = [pts[i] for i in order]
        vals = [vals[i] for i in order]
        if abs(vals[-1] - vals[0]) < tol:
            break
        cen = np.mean(pts[:-1], axis=0)
        xr = cen + (cen - pts[-1])
        fr = f(xr)
        if fr < vals[0]:
            xe = cen + 2 * (cen - pts[-1])
            fe = f(xe)
            pts[-1], vals[-1] = (xe, fe) if fe < fr else (xr, fr)
        elif fr < vals[-2]:
            pts[-1], vals[-1] = xr, fr
        else:
            xc = cen + 0.5 * (pts[-1] - cen)
            fc = f(xc)
            if fc < vals[-1]:
                pts[-1], vals[-1] = xc, fc
            else:
                for i in range(1, len(pts)):
                    pts[i] = pts[0] + 0.5 * (pts[i] - pts[0])
                    vals[i] = f(pts[i])
    i = int(np.argmin(vals))
    return pts[i], vals[i]


# ----------------------------------------------------------------------------------- geometry
def ellipse_pts(cx, cy, rx, ry, ang=0.0, n=120):
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    out = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x, y = rx * math.cos(t), ry * math.sin(t)
        out.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return out


def render_prims(prims):
    """Coverage (0..1) of the union of primitives over the full plate."""
    pts_all = []
    for pr in prims:
        if pr[0] == 'ellipse':
            pts_all += ellipse_pts(*pr[1])
        else:
            pts_all += list(pr[1])
    xs = [p[0] for p in pts_all]
    ys = [p[1] for p in pts_all]
    x0 = max(0, int(math.floor(min(xs))) - 4)
    y0 = max(0, int(math.floor(min(ys))) - 4)
    x1 = min(W, int(math.ceil(max(xs))) + 4)
    y1 = min(H, int(math.ceil(max(ys))) + 4)
    im = Image.new('L', ((x1 - x0) * SS, (y1 - y0) * SS), 0)
    d = ImageDraw.Draw(im)

    def tr(p):
        return ((p[0] - x0) * SS, (p[1] - y0) * SS)
    for pr in prims:
        if pr[0] == 'poly':
            d.polygon([tr(p) for p in pr[1]], fill=255)
        elif pr[0] == 'ellipse':
            d.polygon([tr(p) for p in ellipse_pts(*pr[1])], fill=255)
        elif pr[0] == 'line':
            pts = [tr(p) for p in pr[1]]
            wdt = pr[2] * SS
            d.line(pts, fill=255, width=max(1, int(round(wdt))))
            for p in pts:
                r = wdt / 2
                d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=255)
    a = np.asarray(im, np.float64) / 255.0
    a = a.reshape(y1 - y0, SS, x1 - x0, SS).mean(axis=(1, 3))
    full = np.zeros((H, W))
    full[y0:y1, x0:x1] = a
    return full


def distance_outside(mask, rmax):
    """Approximate Euclidean distance (px) from each pixel to the nearest True pixel of mask,
    capped at rmax + 1. Brute force over disc offsets; only used on small masks."""
    ys, xs = np.nonzero(mask)
    if len(ys) == 0:
        return np.full(mask.shape, rmax + 1.0)
    R = int(math.ceil(rmax)) + 1
    y0, y1 = max(0, ys.min() - R), min(mask.shape[0], ys.max() + R + 1)
    x0, x1 = max(0, xs.min() - R), min(mask.shape[1], xs.max() + R + 1)
    m = mask[y0:y1, x0:x1]
    dist = np.where(m, 0.0, rmax + 1.0)
    hh, ww = m.shape
    for dy in range(-R, R + 1):
        for dx in range(-R, R + 1):
            r = math.hypot(dx, dy)
            if r == 0 or r > R:
                continue
            sh = np.zeros_like(m)
            sy0, sy1 = max(0, dy), min(hh, hh + dy)
            sx0, sx1 = max(0, dx), min(ww, ww + dx)
            sh[sy0:sy1, sx0:sx1] = m[sy0 - dy:sy1 - dy, sx0 - dx:sx1 - dx]
            dist = np.where(sh & (dist > r), r, dist)
    full = np.full(mask.shape, rmax + 1.0)
    full[y0:y1, x0:x1] = dist
    return full


# ------------------------------------------------------------------------------- registration
def register(donor_path, region, exclude):
    """Affine fit donor_px = M (p - c) + t (p plate pixel index) maximising NCC of high-passed
    luminance over the region window, excluding the edited pixels."""
    bx0, by0, bx1, by1 = region['box']
    wx0, wy0, wx1, wy1 = region['window']
    D = load_rgb(donor_path)
    scale0 = D.shape[1] / (bx1 - bx0)
    Dl = lum(blur(D, scale0 * 0.45))
    Dl = Dl - blur(Dl, 6 * scale0)
    U = load_rgb(UNLIT_PLATE)
    Ul = lum(U)
    Ul = Ul - blur(Ul, 6)
    yy, xx = np.mgrid[wy0:wy1, wx0:wx1]
    keep = ~exclude[wy0:wy1, wx0:wx1]
    ys = yy[keep].astype(float)
    xs = xx[keep].astype(float)
    sv = Ul[yy[keep], xx[keep]]
    sv = (sv - sv.mean()) / sv.std()
    c = np.array([(wx0 + wx1) / 2.0, (wy0 + wy1) / 2.0])

    def mapping(p, X, Y):
        m00, m01, m10, m11, tx, ty = p
        dx, dy = X - c[0], Y - c[1]
        return m00 * dx + m01 * dy + tx, m10 * dx + m11 * dy + ty

    def cost(p):
        u, v = mapping(p, xs, ys)
        d = bilinear(Dl, u, v)
        d = (d - d.mean()) / (d.std() + 1e-9)
        return -float(np.mean(d * sv))
    # pixel index i has centre i + 0.5; donor index u has centre u + 0.5 at scale0
    p0 = np.array([scale0, 0, 0, scale0, (c[0] + 0.5 - bx0) * scale0 - 0.5, (c[1] + 0.5 - by0) * scale0 - 0.5])
    best = (cost(p0), p0)
    for ox in np.arange(-24, 25, 6):
        for oy in np.arange(-24, 25, 6):
            for s in (0.97, 1.0, 1.03):
                p = p0.copy()
                p[[0, 3]] *= s
                p[4] += ox
                p[5] += oy
                cst = cost(p)
                if cst < best[0]:
                    best = (cst, p)
    p, fval = nelder_mead(cost, best[1], [0.05, 0.05, 0.05, 0.05, 2.0, 2.0], iters=1500, tol=1e-9)
    p, fval = nelder_mead(cost, p, [0.01, 0.01, 0.01, 0.01, 0.4, 0.4], iters=1500, tol=1e-10)

    def warp():
        gy, gx = np.mgrid[0:H, 0:W].astype(float)
        sub = (slice(by0, by1), slice(bx0, bx1))
        u, v = mapping(p, gx[sub], gy[sub])
        out = np.zeros((H, W, 3))
        out[sub] = bilinear(blur(D, scale0 * 0.45), u, v)
        return out
    info = {'donor': str(Path(donor_path).relative_to(ROOT)).replace('\\', '/'),
            'donor_size': [D.shape[1], D.shape[0]], 'target_box_xyxy': list(region['box']),
            'window_xyxy': list(region['window']),
            'model': 'donor_px = M (p - c) + t; p plate pixel index (top-left origin), donor pixel index',
            'c': c.tolist(), 'M': [[p[0], p[1]], [p[2], p[3]]], 't': [p[4], p[5]],
            'ncc_initial': -cost(p0), 'ncc_registered': -fval}
    return warp(), info


# ------------------------------------------------------------------------------ reconstruction
def extrapolate(v, valid, sigmas):
    """Masked average of v over valid pixels, filled into invalid areas coarse to fine."""
    out = None
    for s in sorted(sigmas, reverse=True):
        est, den = nblur(v, valid.astype(float), s)
        conf = np.clip(den / 0.3, 0, 1)
        if v.ndim == 3:
            conf = conf[..., None]
        out = est if out is None else out * (1 - conf) + est * conf
    return out


def lowfreq_match(donor, orig, valid, sigmas=(4, 10, 24), detail=True):
    """Match a registered donor to the plate around the edit: scale the donor's fine detail
    (sigma 1.5 high-pass) by the plate/donor detail ratio measured on valid pixels (clamped to
    0.35..1.3), then add the low-frequency difference orig - donor measured on valid pixels.
    Both corrections are extrapolated into the invalid area coarse to fine."""
    if detail:
        hd = donor - blur(donor, 1.5)
        ho = orig - blur(orig, 1.5)
        ed = extrapolate(lum(hd) ** 2, valid, (6, 14, 30))
        eo = extrapolate(lum(ho) ** 2, valid, (6, 14, 30))
        gain = np.clip(np.sqrt((eo + 1.0) / (ed + 1.0)), 0.35, 1.3)
        donor = donor - hd + hd * gain[..., None]
    return donor + extrapolate(orig - donor, valid, sigmas)


def guided_lit(unlit_new, unlit_ref, lit_ref, valid, sigmas=(5, 12, 30), eps=30.0):
    """Predict lit from unlit with a per-channel local linear model lit = a * unlit + b fitted
    on valid pixels of the reference plates (masked guided filter, coarse to fine)."""
    wv = valid.astype(float)
    A = B = None
    for s in sorted(sigmas, reverse=True):
        mu, den = nblur(unlit_ref, wv, s)
        ml, _ = nblur(lit_ref, wv, s)
        muu, _ = nblur(unlit_ref * unlit_ref, wv, s)
        mul, _ = nblur(unlit_ref * lit_ref, wv, s)
        var = np.maximum(muu - mu * mu, 0)
        cov = mul - mu * ml
        a = cov / (var + eps)
        b = ml - a * mu
        a, _ = nblur(a, wv, s)
        b, _ = nblur(b, wv, s)
        conf = np.clip(den / 0.3, 0, 1)[..., None]
        if A is None:
            A, B = a, b
        else:
            A = A * (1 - conf) + a * conf
            B = B * (1 - conf) + b * conf
    return A * unlit_new + B


def bleed(rgb, known, iters=24):
    """Fill RGB of unknown pixels from known neighbours (for transparent texels under bilinear
    filtering). Known pixels are unchanged."""
    out = rgb.copy()
    k = known.copy()
    for _ in range(iters):
        if k.all():
            break
        acc = np.zeros_like(out)
        cnt = np.zeros(k.shape)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)):
            sk = np.roll(np.roll(k, dy, 0), dx, 1)
            so = np.roll(np.roll(out, dy, 0), dx, 1)
            acc += so * sk[..., None]
            cnt += sk
        grow = (~k) & (cnt > 0)
        out[grow] = acc[grow] / cnt[grow][:, None]
        k = k | grow
    if k.any():
        out[~k] = out[k].mean(axis=0)
    return out


def piece_motion(p, alpha):
    """Silhouette height, maximum displacement (px) and patch dilation (px) of a piece."""
    ys = np.nonzero(alpha.max(axis=1) > 0)[0]
    height = float(ys.max() - ys.min() + 1)
    if p['kind'] == 'removal':
        return height, 0.0, REMOVAL_DILATION
    motion = max(MIN_MOTION, p['motion_fraction'] * height)
    return height, motion, int(math.ceil(motion + SAFETY))


def max_reach(p, alpha):
    """Largest distance from the rotation pivot to a covered pixel centre."""
    ys, xs = np.nonzero(alpha > 0)
    pv = p['pivot']
    if p['kind'] == 'wobble':
        return float(max(np.max(np.hypot(xs + 0.5 - c[0], ys + 0.5 - c[1])) for c in p['contacts']))
    return float(np.max(np.hypot(xs + 0.5 - pv[0], ys + 0.5 - pv[1])))


def displace(rgba, x0, y0, p, sign, motion, reach):
    """Move a cutout placed at plate (x0, y0) to one extreme of its motion. Swing rotates about the
    hang point, wobble rocks about the left or right base contact, lid lifts straight up.
    Returns premultiplied RGB and alpha over the full plate."""
    h, w = rgba.shape[:2]
    gy, gx = np.mgrid[0:H, 0:W].astype(float) + 0.5
    if p['kind'] == 'lid':
        sx, sy = gx, gy + motion
    else:
        pv = (p['contacts'][1] if sign > 0 else p['contacts'][0]) if p['kind'] == 'wobble' else p['pivot']
        ang = sign * motion / reach
        ca, sa = math.cos(ang), math.sin(ang)
        dx, dy = gx - pv[0], gy - pv[1]
        sx = pv[0] + dx * ca + dy * sa
        sy = pv[1] - dx * sa + dy * ca
    u = sx - x0 - 0.5
    v = sy - y0 - 0.5
    inside = (u > -1) & (u < w) & (v > -1) & (v < h)
    pm = rgba.astype(float).copy()
    pm[..., 3] /= 255.0
    pm[..., :3] *= pm[..., 3:4]
    pad = np.zeros((h + 2, w + 2, 4))
    pad[1:-1, 1:-1] = pm
    s = np.zeros((H, W, 4))
    s[inside] = bilinear(pad, u[inside] + 1, v[inside] + 1)
    return s[..., :3], s[..., 3]


def open_top(cu, Uc, alpha, seam, bx0, by0, rim=1.5):
    """Paint the open trunk under a lid: inside the lid silhouette, pixels above the front seam
    become the dark interior (0.22..0.34 of the body colour, darker toward the front rim, keeping
    a quarter of the donor's fine texture) and a rim band of `rim` px above the seam takes the body
    plank colour sampled just below the seam, lifted 12% as a top-lit edge."""
    h, w = alpha.shape
    gy, gx = np.mgrid[0:h, 0:w].astype(float) + 0.5
    sx = np.array([p[0] for p in seam]) - bx0
    sy = np.array([p[1] for p in seam]) - by0
    seam_y = np.interp(gx[0], sx, sy)[None, :].repeat(h, 0)
    t = seam_y - gy  # px above the seam
    inside = alpha > 0
    cols = np.nonzero(inside.any(axis=0))[0]
    body = np.zeros((w, 3))
    for x in cols:
        y0 = int(seam_y[0, x]) + 2
        ys = [y for y in range(y0, y0 + 5) if y < h and alpha[y, x] == 0]
        body[x] = Uc[ys, x].mean(axis=0) if ys else Uc[min(h - 1, y0 + 3), x]
    body_mean = body[cols].mean(axis=0)
    top = max(1.0, float(t[inside].max()))
    shade = 0.22 + 0.12 * np.clip((t - rim) / top, 0, 1)
    tex = cu - blur(cu, 1.5)
    interior = body_mean[None, None, :] * shade[..., None] + 0.25 * tex
    rimc = np.clip(body[None, :, :] * 1.12, 0, 255).repeat(h, 0)
    w_int = np.clip(t - rim + 0.5, 0, 1)            # 1 above the rim band
    w_rim = np.clip(t + 0.5, 0, 1) * (1 - w_int)     # rim band just above the seam
    out = cu.copy()
    m = inside[..., None]
    mixed = cu * (1 - w_int - w_rim)[..., None] + interior * w_int[..., None] + rimc * w_rim[..., None]
    return np.where(m, mixed, out)


def build():
    U = load_rgb(UNLIT_PLATE)
    L = load_rgb(LIT_PLATE)
    U8 = np.asarray(Image.open(UNLIT_PLATE).convert('RGB'))
    L8 = np.asarray(Image.open(LIT_PLATE).convert('RGB'))
    PU = U8.copy()
    PL = L8.copy()
    declared = np.zeros((H, W), bool)
    weight_full = np.zeros((H, W))
    sil_all = np.zeros((H, W), bool)
    alphas = {}
    for p in PIECES:
        a = np.round(render_prims(p['prims']) * 255) / 255.0
        alphas[p['name']] = a
        sil_all |= a > 0
    exclude = distance_outside(sil_all, 8) <= 8
    regs = {}
    results = {}
    report = {}
    for rname, R in REGIONS.items():
        bx0, by0, bx1, by1 = R['box']
        sub = (slice(by0, by1), slice(bx0, bx1))
        clean, info_c = register(WORK / 'donors' / R['clean'], R, exclude)
        regs[rname] = {'clean': info_c}
        comp = None
        if 'completion' in R:
            comp, info_k = register(WORK / 'donors' / R['completion'], R, exclude)
            regs[rname]['completion'] = info_k
        pieces = [p for p in PIECES if p['region'] == rname]
        Uc, Lc = U[sub], L[sub]
        wr = np.zeros(Uc.shape[:2])
        dec = np.zeros(Uc.shape[:2], bool)
        for p in pieces:
            a = alphas[p['name']][sub]
            height, motion, D = piece_motion(p, alphas[p['name']])
            dist = distance_outside(a > 0, D + 1)
            w = np.clip((D - dist) / max(D - 1.0, 1.0), 0, 1)
            w[dist <= 1] = 1
            p['_height'], p['_motion'], p['_dilation'] = height, motion, D
            p['_reach'] = max_reach(p, alphas[p['name']])
            full = np.zeros((H, W), bool)
            full[sub] = dist < D
            p['_declared'] = full
            wr = np.maximum(wr, w)
            dec |= dist < D
        valid = ~dec
        cu = lowfreq_match(clean[sub], Uc, valid)
        for p in pieces:
            if 'seam' in p:
                cu = open_top(cu, Uc, alphas[p['name']][sub], p['seam'], bx0, by0)
        cl = guided_lit(cu, Uc, Lc, valid)
        pu = Uc * (1 - wr[..., None]) + cu * wr[..., None]
        pl = Lc * (1 - wr[..., None]) + cl * wr[..., None]
        pu8 = np.where(dec[..., None], to_u8(pu), U8[sub])
        pl8 = np.where(dec[..., None], to_u8(pl), L8[sub])
        PU[sub] = np.where(dec[..., None], pu8, PU[sub])
        PL[sub] = np.where(dec[..., None], pl8, PL[sub])
        declared[sub] |= dec
        weight_full[sub] = np.maximum(weight_full[sub], wr * dec)
        # pieces back to front; colours at partial coverage are un-premultiplied against the
        # layers below so the rest composite reproduces the plate
        layer_u = pu8.astype(float)
        layer_l = pl8.astype(float)
        movers = [p for p in pieces if p['kind'] != 'removal']
        for i, p in enumerate(movers):
            a = alphas[p['name']][sub]
            cover = np.zeros_like(a)
            for q in movers[i + 1:]:
                cover = 1 - (1 - cover) * (1 - alphas[q['name']][sub])
            need = (a > 0) & (cover > 0)
            est_u = (Uc - (1 - a[..., None]) * layer_u) / np.maximum(a, 1e-6)[..., None]
            est_l = (Lc - (1 - a[..., None]) * layer_l) / np.maximum(a, 1e-6)[..., None]
            fu, fl = est_u, est_l
            if need.any():
                if comp is None:
                    raise SystemExit(p['name'] + ' is occluded but its region has no completion donor')
                vis = (a >= 0.999) & (cover == 0)
                ku = lowfreq_match(comp[sub], Uc, vis)
                kl = guided_lit(ku, Uc, Lc, vis)
                c3 = cover[..., None]
                fu = np.where(need[..., None], c3 * ku + (1 - c3) * est_u, est_u)
                fl = np.where(need[..., None], c3 * kl + (1 - c3) * est_l, est_l)
            known = a >= 0.1
            fu8 = to_u8(bleed(np.clip(fu, 0, 255), known))
            fl8 = to_u8(bleed(np.clip(fl, 0, 255), known))
            a8 = to_u8(a * 255)
            aq = a8.astype(float)[..., None] / 255
            layer_u = aq * fu8 + (1 - aq) * layer_u
            layer_l = aq * fl8 + (1 - aq) * layer_l
            ys, xs = np.nonzero(a8 > 0)
            cx0 = max(0, xs.min() - CROP_MARGIN)
            cy0 = max(0, ys.min() - CROP_MARGIN)
            cx1 = min(a.shape[1], xs.max() + 1 + CROP_MARGIN)
            cy1 = min(a.shape[0], ys.max() + 1 + CROP_MARGIN)
            cs = (slice(cy0, cy1), slice(cx0, cx1))
            results[p['name']] = {
                'unlit': np.dstack([fu8[cs], a8[cs]]), 'lit': np.dstack([fl8[cs], a8[cs]]),
                'x': bx0 + cx0, 'y': by0 + cy0, 'completed': need[cs] & (a8[cs] > 0)}
            p['_crop'] = (bx0 + cx0, by0 + cy0, cx1 - cx0, cy1 - cy0)
        inside = np.zeros_like(dec)
        removed = np.zeros_like(dec)
        for p in pieces:
            if p['kind'] == 'removal':
                removed |= p['_declared'][sub]
            else:
                inside |= alphas[p['name']][sub] > 0
        du = np.abs(layer_u - U8[sub].astype(float)).max(axis=2)
        dl = np.abs(layer_l - L8[sub].astype(float)).max(axis=2)
        removed &= ~inside
        ring = dec & ~inside & ~removed
        report[rname] = {
            'box_xyxy': list(R['box']), 'declared_pixels': int(dec.sum()),
            'silhouette_pixels': int(inside.sum()),
            'removal_only_pixels': int(removed.sum()),
            'changed_pixels_unlit': int(np.any(pu8 != U8[sub], axis=2).sum()),
            'changed_pixels_lit': int(np.any(pl8 != L8[sub], axis=2).sum()),
            'rest_vs_plate_inside_silhouettes': {
                'unlit_max': float(du[inside].max()), 'unlit_mean': float(du[inside].mean()),
                'lit_max': float(dl[inside].max()), 'lit_mean': float(dl[inside].mean())},
            'rest_vs_plate_dilation_ring': {
                'unlit_max': float(du[ring].max()), 'unlit_mean': float(du[ring].mean()),
                'lit_max': float(dl[ring].max()), 'lit_mean': float(dl[ring].mean())},
            '_rest_unlit': to_u8(layer_u), '_rest_lit': to_u8(layer_l)}
    return dict(U8=U8, L8=L8, PU=PU, PL=PL, declared=declared, weight=weight_full, alphas=alphas,
                results=results, regs=regs, report=report)


# ------------------------------------------------------------------------------------ outputs
CUP_RECT = {'frame_anchor_top_left': (370.0, 144.0), 'size': (74.0, 111.0), 'pivot': (0.506, 0.94),
            'swing_degrees': 3.0}  # MerchantEncounter.unity HangingCup instance, HangingCup.prefab, HangingCupSwing.anim


def cup_alpha(angle_deg=0.0):
    """HangingCup.png coverage in the plate frame (540 x 960 frame scaled by 941/540, 1672/960)."""
    cup = np.asarray(Image.open(ART / 'HangingCup.png').convert('RGBA')).astype(float)
    sx, sy = W / 540.0, H / 960.0
    w, h = CUP_RECT['size'][0] * sx, CUP_RECT['size'][1] * sy
    px, py = CUP_RECT['frame_anchor_top_left'][0] * sx, CUP_RECT['frame_anchor_top_left'][1] * sy
    left, top = px - CUP_RECT['pivot'][0] * w, py - (1 - CUP_RECT['pivot'][1]) * h
    gy, gx = np.mgrid[0:H, 0:W].astype(float) + 0.5
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    dx, dy = gx - px, gy - py
    ux, uy = px + dx * ca + dy * sa, py - dx * sa + dy * ca
    u = (ux - left) / w * cup.shape[1] - 0.5
    v = (uy - top) / h * cup.shape[0] - 0.5
    ins = (u >= 0) & (u < cup.shape[1] - 1) & (v >= 0) & (v < cup.shape[0] - 1)
    out = np.zeros((H, W, 4))
    out[ins] = bilinear(cup, u[ins], v[ins])
    return out, (px, py)


def guid_in_use():
    used = set()
    for m in (ROOT / 'Dice-Unity/Assets').rglob('*.meta'):
        try:
            for line in m.read_text(encoding='utf-8', errors='ignore').splitlines()[:3]:
                if line.startswith('guid:'):
                    used.add(line.split(':', 1)[1].strip())
        except OSError:
            pass
    return used


def write_meta(path, kind, used):
    """Write a .meta beside path unless one exists (an existing meta keeps its GUID)."""
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return False
    g = uuid.uuid4().hex
    while g in used:
        g = uuid.uuid4().hex
    used.add(g)
    if kind == 'texture':
        lines = META_TEMPLATE.read_text(encoding='ascii').splitlines()
        lines = ['guid: ' + g if l.startswith('guid:') else l for l in lines]
        text = '\n'.join(lines).rstrip('\n') + '\n'
    elif kind == 'folder':
        text = ('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
                '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % g)
    else:
        text = ('fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n'
                '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % g)
    meta.write_text(text, encoding='ascii', newline='\n')
    return True


PROTECTED = {UNLIT_PLATE.resolve(), LIT_PLATE.resolve(), (ART / 'MerchantLanternResponse.png').resolve()}


def save_png(arr, path):
    path = Path(path)
    if path.resolve() in PROTECTED:
        raise SystemExit('refusing to write an installed plate: %s' % path)
    if not (str(path.resolve()).startswith(str(PROPS.resolve())) or str(path.resolve()).startswith(str(WORK.resolve()))):
        raise SystemExit('refusing to write outside Props/ and the work folder: %s' % path)
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(arr).save(path, optimize=True)


def rel(p):
    return str(Path(p).resolve().relative_to(ROOT)).replace('\\', '/')


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def composite(base, layers):
    """Composite RGBA layers [(rgba_u8, x, y)] over base (u8) in order; float result."""
    out = base.astype(float).copy()
    for rgba, x, y in layers:
        h, w = rgba.shape[:2]
        a = rgba[..., 3:4].astype(float) / 255
        out[y:y + h, x:x + w] = a * rgba[..., :3] + (1 - a) * out[y:y + h, x:x + w]
    return out


def write_outputs(B):
    plates_before = {rel(UNLIT_PLATE): sha(UNLIT_PLATE), rel(LIT_PLATE): sha(LIT_PLATE)}
    PROPS.mkdir(parents=True, exist_ok=True)
    (WORK / 'masks').mkdir(parents=True, exist_ok=True)
    out_u = PROPS / 'MerchantAmbientUnlitPropsRemoved.png'
    out_l = PROPS / 'MerchantLanternOnlyPropsRemoved.png'
    save_png(B['PU'], out_u)
    save_png(B['PL'], out_l)
    save_png((B['declared'] * 255).astype(np.uint8), WORK / 'masks/props-declared-mask.png')
    save_png(to_u8(B['weight'] * 255), WORK / 'masks/props-patch-weight.png')
    files = {}
    for p in PIECES:
        save_png((p['_declared'] * 255).astype(np.uint8), WORK / 'masks' / (p['name'] + '-declared.png'))
        if p['kind'] == 'removal':
            continue
        r = B['results'][p['name']]
        fu = PROPS / (p['name'] + 'Unlit.png')
        fl = PROPS / (p['name'] + 'LanternLit.png')
        save_png(r['unlit'], fu)
        save_png(r['lit'], fl)
        files[p['name']] = (fu, fl)
        if r['completed'].any():
            save_png((r['completed'] * 255).astype(np.uint8), WORK / 'masks' / (p['name'] + '-completed.png'))
    # ---- verification from the written files
    U8 = np.asarray(Image.open(UNLIT_PLATE).convert('RGB'))
    L8 = np.asarray(Image.open(LIT_PLATE).convert('RGB'))
    PU = np.asarray(Image.open(out_u).convert('RGB'))
    PL = np.asarray(Image.open(out_l).convert('RGB'))
    dec = B['declared']
    ver = {'installed_plates_sha256_before': plates_before,
           'installed_plates_sha256_after': {rel(UNLIT_PLATE): sha(UNLIT_PLATE), rel(LIT_PLATE): sha(LIT_PLATE)},
           'outside_declared_identical_unlit': bool(np.array_equal(PU[~dec], U8[~dec])),
           'outside_declared_identical_lit': bool(np.array_equal(PL[~dec], L8[~dec])),
           'declared_pixels': int(dec.sum()),
           'changed_pixels_unlit': int(np.any(PU != U8, axis=2).sum()),
           'changed_pixels_lit': int(np.any(PL != L8, axis=2).sum())}
    ver['installed_plates_unchanged'] = ver['installed_plates_sha256_before'] == ver['installed_plates_sha256_after']
    alpha_same = {}
    layers_u, layers_l = [], []
    for p in PIECES:
        if p['kind'] == 'removal':
            continue
        fu, fl = files[p['name']]
        a = np.asarray(Image.open(fu))
        b = np.asarray(Image.open(fl))
        alpha_same[p['name']] = bool(np.array_equal(a[..., 3], b[..., 3]))
        x, y = B['results'][p['name']]['x'], B['results'][p['name']]['y']
        layers_u.append((a, x, y))
        layers_l.append((b, x, y))
    ver['alpha_identical_unlit_vs_lit'] = alpha_same
    rest_u = composite(PU, layers_u)
    rest_l = composite(PL, layers_l)
    sil = np.zeros((H, W), bool)
    removed = np.zeros((H, W), bool)
    for p in PIECES:
        if p['kind'] == 'removal':
            removed |= p['_declared']
        else:
            sil |= B['alphas'][p['name']] > 0
    removed &= ~sil
    ring = dec & ~sil & ~removed
    du = np.abs(rest_u - U8).max(axis=2)
    dl = np.abs(rest_l - L8).max(axis=2)
    core = np.zeros((H, W), bool)
    for p in PIECES:
        if p['kind'] != 'removal':
            core |= B['alphas'][p['name']] >= 1.0

    def st(m):
        return {'pixels': int(m.sum()), 'unlit_max': int(round(du[m].max())), 'unlit_mean': round(float(du[m].mean()), 4),
                'lit_max': int(round(dl[m].max())), 'lit_mean': round(float(dl[m].mean()), 4)}
    ver['rest_composite_vs_plate'] = {
        'note': 'max over RGB channels per pixel, 0..255; pieces composited over the patched plates at rest',
        'full_frame': st(np.ones((H, W), bool)),
        'silhouettes_full_coverage': st(core),
        'silhouettes_all': st(sil),
        'dilation_ring_outside_silhouettes': st(ring),
        'cup_removal_HangingCage': st(removed)}
    B['rest_u'], B['rest_l'] = to_u8(rest_u), to_u8(rest_l)
    B['layers_u'], B['layers_l'] = layers_u, layers_l
    B['files'] = files
    return ver


def build_json(B, ver):
    pieces = []
    removals = []
    for order, p in enumerate([q for q in PIECES if q['kind'] != 'removal']):
        x, y, w, h = [int(v) for v in p['_crop']]
        fu, fl = B['files'][p['name']]
        r = B['results'][p['name']]
        kind = {'swing': 'swing (hang point)', 'wobble': 'wobble (base contact)', 'lid': 'lid (hinge edge)'}[p['kind']]
        px, py = p['pivot']
        ang = math.degrees(p['_motion'] / p['_reach'])
        if p['kind'] == 'swing':
            amp = {'rotate_degrees': round(ang, 2), 'about': 'pivot',
                   'max_displacement_px': round(p['_motion'], 2),
                   'suggestion': 'damped pendulum swing, +-%.1f degrees about the hang point, 0.8..1.2 s per half cycle' % ang}
        elif p['kind'] == 'wobble':
            amp = {'rotate_degrees': round(ang, 2), 'about': 'left contact for negative angles, right contact for positive',
                   'max_displacement_px': round(p['_motion'], 2),
                   'suggestion': 'rock +-%.1f degrees on alternating base contacts, 2..4 rocks settling over about 1 s' % ang}
        else:
            amp = {'lift_px': round(p['_motion'], 2), 'max_displacement_px': round(p['_motion'], 2),
                   'suggestion': 'lift %.1f px over about 80 ms (optionally tilt up to %.1f degrees about the hinge edge), drop back in 50 ms, 1..3 thumps' % (p['_motion'], ang)}
        entry = {
            'name': p['name'], 'kind': kind, 'draw_order': order,
            'files': {'unlit': rel(fu), 'lantern_lit': rel(fl)},
            'crop_rect': {'x': x, 'y': y, 'w': w, 'h': h},
            'pivot': {'plate': [px, py], 'in_crop': [round(px - x, 2), round(py - y, 2)],
                      'unity_normalized': [round((px - x) / w, 4), round(1 - (py - y) / h, 4)],
                      'note': p['pivot_note']},
            'frame_540x960': {'x': round(x * 540 / W, 3), 'y_from_top': round(y * 960 / H, 3),
                              'w': round(w * 540 / W, 3), 'h': round(h * 960 / H, 3)},
            'silhouette_height_px': p['_height'], 'reach_px': round(p['_reach'], 2),
            'motion': amp,
            'masks': {'alpha': 'alpha channel of the cutout files (identical in both)',
                      'silhouette_primitives': [list(pr) for pr in p['prims']],
                      'declared_patch_mask': rel(WORK / 'masks' / (p['name'] + '-declared.png')),
                      'dilation_px': p['_dilation'],
                      'completed_region': rel(WORK / 'masks' / (p['name'] + '-completed.png')) if r['completed'].any() else None,
                      'completed_pixels': int(r['completed'].sum())}}
        if 'contacts' in p:
            entry['pivot']['base_contacts_plate'] = [list(c) for c in p['contacts']]
        if 'hinge' in p:
            entry['pivot']['hinge_edge_plate'] = [list(c) for c in p['hinge']]
        if 'seam' in p:
            entry['pivot']['front_seam_plate'] = [list(c) for c in p['seam']]
        pieces.append(entry)
    for p in PIECES:
        if p['kind'] != 'removal':
            continue
        removals.append({'name': p['name'], 'reason': p['removal_reason'],
                         'silhouette_primitives': [list(pr) for pr in p['prims']],
                         'declared_patch_mask': rel(WORK / 'masks' / (p['name'] + '-declared.png')),
                         'dilation_px': p['_dilation'], 'declared_pixels': int(p['_declared'].sum())})
    regions = {}
    for k, v in B['report'].items():
        regions[k] = {kk: vv for kk, vv in v.items() if not kk.startswith('_')}
        regions[k]['donors'] = B['regs'][k]
        regions[k]['prompts'] = [rel(WORK / 'prompts' / f) for f in PROMPTS[k]]
    return {
        'source_plates': {'unlit': rel(UNLIT_PLATE), 'lantern_lit': rel(LIT_PLATE), 'size': [W, H]},
        'patched_plates': {'unlit': rel(PROPS / 'MerchantAmbientUnlitPropsRemoved.png'),
                           'lantern_lit': rel(PROPS / 'MerchantLanternOnlyPropsRemoved.png'),
                           'declared_mask': rel(WORK / 'masks/props-declared-mask.png'),
                           'patch_weight': rel(WORK / 'masks/props-patch-weight.png')},
        'coordinates': 'plate pixels, top-left origin; pixel (i, j) covers [i, i + 1) x [j, j + 1)',
        'draw_order': 'patched plate, then pieces in draw_order (HangingPot behind HangingPan)',
        'pieces': pieces, 'removals': removals, 'regions': regions,
        'hanging_cup': B['cup'], 'verification': ver,
        'script': rel(Path(__file__)), 'work_folder': rel(WORK)}


PROMPTS = {'pots': ['pots-remove-all.txt', 'pots-remove-pan.txt'], 'bottle': ['bottle-remove.txt'],
           'trunk': ['trunk-lid-remove.txt']}


def cup_report(B):
    rest, pivot = cup_alpha(0)
    foot = rest[..., 3].copy()
    for ang in (-CUP_RECT['swing_degrees'], CUP_RECT['swing_degrees']):
        foot = np.maximum(foot, cup_alpha(ang)[0][..., 3])
    ra = rest[..., 3] / 255
    fa = foot / 255
    ys, xs = np.nonzero(ra > 0.5)
    out = {'source': 'MerchantEncounter.unity HangingCup instance (540 x 960 frame (370, 144) from top-left), '
                      'HangingCup.prefab Cup rect 74 x 111 pivot (0.506, 0.94), HangingCupSwing.anim +-3 degrees',
           'hang_point_plate': [round(pivot[0], 2), round(pivot[1], 2)],
           'rest_alpha_bounds_plate_xyxy': [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1],
           'painted_cup_in_plates': False,
           'finding': 'No cup or mug is painted at or near the cup position in either plate. The cup hangs directly over '
                      'the painted cage basket (strap, bail and body, plate x 618..681, y 302..426), so the scene showed '
                      'two hanging vessels in one place. The cage is removed from both patched plates as a removal for '
                      'the cup, not a movable prop.',
           'overlap_pixels_alpha_gt_0.1': {}}
    for p in PIECES:
        a = B['alphas'][p['name']] > 0
        out['overlap_pixels_alpha_gt_0.1'][p['name']] = {
            'rest': int((a & (ra > 0.1)).sum()), 'swing_extremes': int((a & (fa > 0.1)).sum()), 'silhouette': int(a.sum())}
    B['cup_rest'] = rest
    return out


# ------------------------------------------------------------------------------- contact sheet
CANDIDATES = [
    # (label, box xyxy, status) status: chosen / removal / considered
    ('HangingPan', (556, 368, 629, 458), 'chosen'),
    ('HangingPot', (600, 407, 649, 493), 'chosen'),
    ('HangingCage: removed for cup', (618, 302, 684, 429), 'removal'),
    ('ShelfBottle', (212, 320, 247, 387), 'chosen'),
    ('TrunkLid', (166, 401, 263, 482), 'chosen'),
    ('left pans: under canopy rags', (498, 300, 575, 475), 'considered'),
    ('pale jar', (138, 322, 175, 370), 'considered'),
    ('brass jar', (250, 340, 285, 392), 'considered'),
    ('shelf crate: no distinct lid', (286, 350, 362, 410), 'considered'),
    ('left crate: merchant elbow', (0, 838, 138, 1035), 'considered'),
    ('chest: under merchant arms', (278, 885, 432, 962), 'considered'),
]
DISPLAY_GAIN = 1.6  # unlit tiles are brightened for review only


def font(size):
    from PIL import ImageFont
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


def checker(h, w, cell=6):
    yy, xx = np.mgrid[0:h, 0:w]
    c = ((yy // cell + xx // cell) % 2).astype(float)
    return (110 + 50 * c)[..., None].repeat(3, 2)


def tile(arr, scale=1, gain=1.0):
    a = np.clip(arr.astype(float) * gain, 0, 255).astype(np.uint8)
    im = Image.fromarray(a)
    if scale != 1:
        im = im.resize((int(im.width * scale), int(im.height * scale)), Image.NEAREST if scale >= 2 else Image.LANCZOS)
    return im


def on_checker(rgba, scale, gain=1.0):
    h, w = rgba.shape[:2]
    big = np.kron(rgba.astype(float), np.ones((scale, scale, 1)))
    a = big[..., 3:4] / 255
    out = big[..., :3] * gain * a + checker(h * scale, w * scale) * (1 - a)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def scene_layers():
    """Plate plus canopy overhang (MerchantOriginal through CanopyOriginalOverhangMask) and merchant
    (MerchantHost mapped by x = 0.9136 sx + 21, y = 0.94 sy + 28.8), for candidate review."""
    orig = np.asarray(Image.open(ART / 'MerchantOriginal.png').convert('RGB')).astype(float)
    m = np.asarray(Image.open(ART / 'CanopyOriginalOverhangMask.png').convert('RGBA')).astype(float)[..., 3:4] / 255
    host = Image.open(ART / 'MerchantHost.png').convert('RGBA')
    hs = host.resize((round(host.width * 0.9136), round(host.height * 0.94)), Image.LANCZOS)
    lay = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    lay.paste(hs, (21, 29))
    return orig, m, np.asarray(lay).astype(float)


def over(base, rgba):
    a = rgba[..., 3:4] / 255
    return base * (1 - a) + rgba[..., :3] * a


def contact_sheet(B, ver, path):
    from PIL import ImageDraw
    f14, f18, f24 = font(14), font(18), font(24)
    sections = []
    U8, L8, PU, PL = B['U8'], B['L8'], B['PU'], B['PL']
    orig, cm, host = scene_layers()
    cup = B['cup_rest']

    # 1. candidates
    scene = U8.astype(float) * (1 - cm) + orig * cm
    scene = over(scene, host)
    scene = over(scene, cup)
    crop = (0, 280, 760, 1060)
    im = tile(scene[crop[1]:crop[3], crop[0]:crop[2]], 1.0, DISPLAY_GAIN)
    d = ImageDraw.Draw(im)
    colors = {'chosen': (60, 230, 90), 'removal': (255, 150, 30), 'considered': (170, 170, 170)}
    for label, (x0, y0, x1, y1), st in CANDIDATES:
        bx = (x0 - crop[0], y0 - crop[1], x1 - crop[0], y1 - crop[1])
        d.rectangle(bx, outline=colors[st], width=2 if st != 'considered' else 1)
        d.text((bx[0] + 2, bx[3] + 1 if st == 'considered' else bx[1] - 16), label, fill=colors[st], font=f14)
    t1 = (im, 'Scene at plate scale (plate + canopy + merchant + cup at rest). Green: chosen, orange: removal for the cup, grey: considered')
    post = (540, 236, 720, 500)
    a = tile(over(U8.astype(float), cup)[post[1]:post[3], post[0]:post[2]], 2, DISPLAY_GAIN)
    pieces_u = composite(PU, B['layers_u'])
    b = tile(over(pieces_u, cup)[post[1]:post[3], post[0]:post[2]], 2, DISPLAY_GAIN)
    c = tile(U8[post[1]:post[3], post[0]:post[2]], 2, DISPLAY_GAIN)
    sections.append(('1. Candidates and the hanging cup', [t1, (c, 'Plate: painted cage basket under the cup position'),
                                                         (a, 'Installed plate + cup: two vessels'),
                                                         (b, 'Patched plate + pan/pot pieces + cup')]))
    # 2. cutouts
    tiles = []
    for p in PIECES:
        if p['kind'] == 'removal':
            continue
        r = B['results'][p['name']]
        s = 4 if r['unlit'].shape[0] < 80 else 3
        tiles.append((on_checker(r['unlit'], s, DISPLAY_GAIN), p['name'] + 'Unlit'))
        tiles.append((on_checker(r['lit'], s), p['name'] + 'LanternLit'))
    sections.append(('2. Cutouts on checkerboard (unlit tiles at %.1fx brightness)' % DISPLAY_GAIN, tiles))
    # 3. patched plates
    views = {'pots': (548, 292, 700, 505, 2), 'bottle': (195, 310, 265, 395, 4), 'trunk': (155, 395, 270, 490, 3)}
    tiles = [(tile(PU, 0.4, DISPLAY_GAIN), 'MerchantAmbientUnlitPropsRemoved (full)'),
             (tile(PL, 0.4), 'MerchantLanternOnlyPropsRemoved (full)')]
    for k, (x0, y0, x1, y1, s) in views.items():
        tiles.append((tile(U8[y0:y1, x0:x1], s, DISPLAY_GAIN), k + ': installed unlit'))
        tiles.append((tile(PU[y0:y1, x0:x1], s, DISPLAY_GAIN), k + ': patched unlit'))
        tiles.append((tile(PL[y0:y1, x0:x1], s), k + ': patched lit'))
    sections.append(('3. Patched plates (props and cup-duplicating cage removed)', tiles))
    # 4. rest composite
    tiles = []
    rs = ver['rest_composite_vs_plate']
    for k, (x0, y0, x1, y1, s) in views.items():
        for lab, ref, rest, g in (('unlit', U8, B['rest_u'], DISPLAY_GAIN), ('lit', L8, B['rest_l'], 1.0)):
            diff = np.abs(rest.astype(float) - ref.astype(float)).max(axis=2)[y0:y1, x0:x1]
            heat = np.clip(diff * 4, 0, 255)
            hm = np.stack([heat, heat * 0.5, heat * 0], -1)
            tiles.append((tile(rest[y0:y1, x0:x1], s, g), '%s %s: pieces over patched plate at rest' % (k, lab)))
            tiles.append((tile(hm, s), '%s %s: |rest - installed| x4' % (k, lab)))
    sections.append(('4. Rest composite vs installed plates. Full coverage: unlit max %d mean %.3f, lit max %d mean %.3f; '
                     'all silhouette pixels: unlit max %d mean %.3f; dilation ring: unlit max %d mean %.2f; cage removal: unlit mean %.2f'
                     % (rs['silhouettes_full_coverage']['unlit_max'], rs['silhouettes_full_coverage']['unlit_mean'],
                        rs['silhouettes_full_coverage']['lit_max'], rs['silhouettes_full_coverage']['lit_mean'],
                        rs['silhouettes_all']['unlit_max'], rs['silhouettes_all']['unlit_mean'],
                        rs['dilation_ring_outside_silhouettes']['unlit_max'], rs['dilation_ring_outside_silhouettes']['unlit_mean'],
                        rs['cup_removal_HangingCage']['unlit_mean']), tiles))
    # 5. displaced
    tiles = []
    movers = [p for p in PIECES if p['kind'] != 'removal']
    for k, (x0, y0, x1, y1, s) in views.items():
        group = [p for p in movers if p['region'] == k]
        for sign in (-1, 1):
            for lab, base, layers, g in (('unlit', PU, B['layers_u'], DISPLAY_GAIN), ('lit', PL, B['layers_l'], 1.0)):
                if lab == 'lit' and sign < 0:
                    continue
                out = base.astype(float)
                for p, (rgba, lx, ly) in zip(movers, layers):
                    if p['region'] == k:
                        sg = sign if (group.index(p) % 2 == 0) else -sign
                        if p['kind'] == 'lid' and sign < 0:
                            sg = 1
                        prgb, pa = displace(rgba, lx, ly, p, sg, p['_motion'], p['_reach'])
                        out = prgb + out * (1 - pa[..., None])
                    else:
                        a = rgba[..., 3:4].astype(float) / 255
                        out[ly:ly + rgba.shape[0], lx:lx + rgba.shape[1]] = a * rgba[..., :3] + (1 - a) * out[ly:ly + rgba.shape[0], lx:lx + rgba.shape[1]]
                desc = ', '.join('%s %s' % (p['name'], ('lift %.1fpx' % p['_motion']) if p['kind'] == 'lid' else
                                 ('%+.1f deg' % (math.degrees(p['_motion'] / p['_reach']) * (sign if group.index(p) % 2 == 0 else -sign))))
                                 for p in group)
                tiles.append((tile(out[y0:y1, x0:x1], s, g), '%s %s: %s' % (k, lab, desc)))
    sections.append(('5. Pieces at maximum motion over the patched plates (pan and pot in opposite directions)', tiles))

    # layout
    Wsheet, pad = 2200, 14
    rendered = []
    for title, ts in sections:
        rows, row, x = [], [], pad
        for im, cap in ts:
            if x + im.width + pad > Wsheet and row:
                rows.append(row)
                row, x = [], pad
            row.append((im, cap))
            x += im.width + pad
        if row:
            rows.append(row)
        rendered.append((title, rows))
    total = 20
    for title, rows in rendered:
        total += 44
        for row in rows:
            total += max(im.height for im, _ in row) + 46
    sheet = Image.new('RGB', (Wsheet, total), (24, 24, 28))
    d = ImageDraw.Draw(sheet)
    y = 10
    for title, rows in rendered:
        d.text((pad, y + 8), title, fill=(240, 240, 240), font=f24 if len(title) < 100 else f18)
        y += 44
        for row in rows:
            x = pad
            hmax = max(im.height for im, _ in row)
            for im, cap in row:
                sheet.paste(im, (x, y))
                words, lines, cur = cap.split(' '), [], ''
                for wd in words:
                    trial = (cur + ' ' + wd).strip()
                    if d.textlength(trial, font=f14) > max(im.width, 160) and cur:
                        lines.append(cur)
                        cur = wd
                    else:
                        cur = trial
                lines.append(cur)
                for i, ln in enumerate(lines[:2]):
                    d.text((x, y + im.height + 3 + 16 * i), ln, fill=(220, 220, 220), font=f14)
                x += im.width + pad
            y += hmax + 46
    save_png(np.asarray(sheet), path)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--no-sheet', action='store_true')
    args = ap.parse_args()
    B = build()
    B['cup'] = cup_report(B)
    ver = write_outputs(B)
    doc = build_json(B, ver)
    jp = PROPS / 'props.json'
    jp.write_text(json.dumps(doc, indent=2) + '\n', encoding='ascii', newline='\n')
    used = guid_in_use()
    made = [write_meta(PROPS, 'folder', used)]
    for f in sorted(PROPS.glob('*.png')):
        made.append(write_meta(f, 'texture', used))
    made.append(write_meta(jp, 'text', used))
    (WORK / 'registered').mkdir(parents=True, exist_ok=True)
    for k, v in B['regs'].items():
        (WORK / 'registered' / (k + '.json')).write_text(json.dumps(v, indent=2) + '\n', encoding='ascii', newline='\n')
    if not args.no_sheet:
        contact_sheet(B, ver, WORK / 'contact-sheet.png')
    ok = (ver['installed_plates_unchanged'] and ver['outside_declared_identical_unlit'] and
          ver['outside_declared_identical_lit'] and all(ver['alpha_identical_unlit_vs_lit'].values()))
    print(json.dumps({'ok': ok, 'new_meta_files': sum(made), 'verification': ver}, indent=2))
    if not ok:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
