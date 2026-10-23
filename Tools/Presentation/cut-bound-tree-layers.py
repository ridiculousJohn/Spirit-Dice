"""Cut the Bound Tree scene concept into its first art layers.

Builds four 941 x 1672 textures in the concept's frame from BoundTreeOriginal.png, the preserved generated donors and
the polygons and parameters declared in Docs/Reference/Art/BoundTreeLayers.json:

  BoundTreeBackground.png  RGB   the concept with the nymph removed (clean plate)
  BoundTreeNymph.png       RGBA  the nymph: the generated transparent extraction, registered and colour-matched
  BoundTreeFaceless.png    RGB   BoundTreeBackground.png with the spirit's face replaced by plain bark
  BoundTreeFaceMask.png    RGBA  white, alpha = the face region (BoundTreeBackground.png through it is the face layer)

Each generated donor is an edit of a Lanczos 1024 x 1536 upscale of a concept box, registered to the concept frame by
an affine fit on high-passed luminance. The nymph donor is her extraction on a transparent background, fitted on her
interior: its alpha is her matte and its colour, low-frequency matched to the concept on her visible pixels, is her
colour. Her silhouette (donor alpha above a threshold) defines the plate's removal region. The plate and faceless
donors are fitted around their edits (edit region excluded), low-frequency matched to the surrounding image and
transferred only inside the declared region, blending from full weight near the silhouette or polygon to zero at
the declared edge. Every pixel outside a declared region is copied byte for byte. The script verifies byte identity
outside the declared regions, reports the nymph composite error and writes review images, including the nymph moved
by small offsets and a scale about her seat over the plate.

Python 3.9 or later, no third-party packages. A full run takes a few minutes. Run from any directory:

  python Tools/Presentation/cut-bound-tree-layers.py
  python Tools/Presentation/cut-bound-tree-layers.py --write-assets

The default writes the textures, review images and report.json to a fresh ignored Docs/work/bound-tree-layers-*
directory; --output chooses an unused directory under Docs/work. --write-assets also replaces the four textures under
Dice-Unity/Assets/Dice/Art/BoundTree, keeps existing .meta files and writes a .meta (a copy of
BoundTreeOriginal.png.meta with a fresh GUID) for a texture that has none. The concept and donors are only read.
"""

import argparse
from array import array
from itertools import accumulate
import hashlib
import json
import math
from pathlib import Path
import struct
import tempfile
import uuid
import zlib

ROOT = Path(__file__).resolve().parents[2]
SPEC = ROOT / "Docs/Reference/Art/BoundTreeLayers.json"
ART = ROOT / "Dice-Unity/Assets/Dice/Art/BoundTree"
WORK = ROOT / "Docs/work"
DISTANCE_REACH = 24  # px; polygon distances are exact up to this reach
LUMA = (0.299, 0.587, 0.114)
# review renders of the nymph moved over the plate: name -> (x offset px, y offset px, scale about her seat)
REVIEW_MOTIONS = {"left4": (-4, 0, 1.0), "right4": (4, 0, 1.0), "up6": (0, -6, 1.0), "scale103": (0, 0, 1.03)}


# ------------------------------------------------------------------------------------------------------- PNG
def read_png(path):
    """Return (width, height, channels, bytearray) for a non-interlaced 8-bit gray, RGB or RGBA PNG."""
    data = Path(path).read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path} is not a PNG.")
    offset, chunks, header = 8, [], None
    while offset < len(data):
        length, kind = struct.unpack(">I4s", data[offset:offset + 8])
        body = data[offset + 8:offset + 8 + length]
        offset += 12 + length
        if kind == b"IHDR":
            header = struct.unpack(">IIBBBBB", body)
        elif kind == b"IDAT":
            chunks.append(body)
    width, height, depth, color, _, _, interlace = header
    channels = {0: 1, 2: 3, 6: 4}.get(color)
    if depth != 8 or channels is None or interlace != 0:
        raise ValueError(f"{path} must be a non-interlaced 8-bit gray, RGB or RGBA PNG.")
    raw = zlib.decompress(b"".join(chunks))
    stride, bpp = width * channels, channels
    pixels = bytearray(height * stride)
    previous = bytearray(stride)
    position = 0
    for y in range(height):
        kind = raw[position]
        line = bytearray(raw[position + 1:position + 1 + stride])
        position += 1 + stride
        if kind == 1:
            for x in range(bpp, stride):
                line[x] = (line[x] + line[x - bpp]) & 255
        elif kind == 2:
            line = bytearray((a + b) & 255 for a, b in zip(line, previous))
        elif kind == 3:
            for x in range(stride):
                left = line[x - bpp] if x >= bpp else 0
                line[x] = (line[x] + (left + previous[x]) // 2) & 255
        elif kind == 4:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                b = previous[x]
                c = previous[x - bpp] if x >= bpp else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        elif kind != 0:
            raise ValueError(f"{path} uses unknown PNG filter {kind}.")
        pixels[y * stride:(y + 1) * stride] = line
        previous = line
    return width, height, channels, pixels


def write_png(path, width, height, channels, pixels):
    color = {1: 0, 3: 2, 4: 6}[channels]
    stride = width * channels
    rows = b"".join(b"\x00" + bytes(pixels[y * stride:(y + 1) * stride]) for y in range(height))

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, color, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b""))


# ---------------------------------------------------------------------------------------------- float planes
class Plane:
    """A w x h float plane (row-major array('d')) covering image box (x0, y0, x0 + w, y0 + h)."""

    def __init__(self, x0, y0, w, h, data=None):
        self.x0, self.y0, self.w, self.h = x0, y0, w, h
        self.data = data if data is not None else array("d", bytes(8 * w * h))

    def like(self, data=None):
        return Plane(self.x0, self.y0, self.w, self.h, data)

    def at(self, x, y):
        return self.data[(y - self.y0) * self.w + (x - self.x0)]


def planes_from(pixels, width, channels, box, count=None):
    """Float planes of the first `count` channels of an 8-bit image inside box (x0, y0, x1, y1)."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    out = []
    for c in range(count or channels):
        data = array("d")
        for y in range(y0, y1):
            row = pixels[(y * width + x0) * channels + c:(y * width + x1) * channels:channels]
            data.extend(row)
        out.append(Plane(x0, y0, w, h, data))
    return out


def box_rows(data, w, h, r):
    if r <= 0:
        return data
    inv = 1.0 / (2 * r + 1)
    out = array("d")
    span = 2 * r + 1
    for y in range(h):
        row = data[y * w:(y + 1) * w]
        padded = [row[0]] * r + list(row) + [row[-1]] * r
        c = list(accumulate(padded, initial=0.0))
        out.extend([(c[x + span] - c[x]) * inv for x in range(w)])
    return out


def transpose(data, w, h):
    return array("d", [data[y * w + x] for x in range(w) for y in range(h)])


def blur(p, sigma):
    """Gaussian approximation by three box passes per axis with clamped edges."""
    if sigma <= 0.3:
        return p.like(array("d", p.data))
    r = max(1, int(round((math.sqrt(12 * sigma * sigma / 3 + 1) - 1) / 2)))
    d = p.data
    for _ in range(3):
        d = box_rows(d, p.w, p.h, r)
    d = transpose(d, p.w, p.h)
    for _ in range(3):
        d = box_rows(d, p.h, p.w, r)
    return p.like(transpose(d, p.h, p.w))


def luminance(rgb):
    r, g, b = (q.data for q in rgb)
    return rgb[0].like(array("d", [LUMA[0] * x + LUMA[1] * y + LUMA[2] * z for x, y, z in zip(r, g, b)]))


def highpass(p, sigma):
    low = blur(p, sigma)
    return p.like(array("d", [a - b for a, b in zip(p.data, low.data)]))


def bilinear(p, u, v):
    """Sample plane p at plane-index coordinates (u, v), clamped to its edges."""
    w, h, d = p.w, p.h, p.data
    u = min(max(u, 0.0), w - 1.000001)
    v = min(max(v, 0.0), h - 1.000001)
    x = int(u)
    y = int(v)
    fx = u - x
    fy = v - y
    i = y * w + x
    top = d[i] + (d[i + 1] - d[i]) * fx
    bottom = d[i + w] + (d[i + w + 1] - d[i + w]) * fx
    return top + (bottom - top) * fy


# --------------------------------------------------------------------------------------------- polygon fields
def polygon_distance(poly, box, reach=DISTANCE_REACH):
    """Signed distance (px) from pixel centres in box to the polygon edge: negative inside. Exact up to reach,
    clamped to +-(reach + 1) beyond it."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    far = reach + 1.0
    dist = array("d", [far]) * (w * h)
    n = len(poly)
    for k in range(n):
        ax, ay = poly[k]
        bx, by = poly[(k + 1) % n]
        dx, dy = bx - ax, by - ay
        ll = dx * dx + dy * dy
        i0 = max(x0, int(math.floor(min(ax, bx) - reach - 1)))
        i1 = min(x1, int(math.ceil(max(ax, bx) + reach + 1)))
        j0 = max(y0, int(math.floor(min(ay, by) - reach - 1)))
        j1 = min(y1, int(math.ceil(max(ay, by) + reach + 1)))
        for j in range(j0, j1):
            py = j + 0.5
            base = (j - y0) * w - x0
            for i in range(i0, i1):
                px = i + 0.5
                t = ((px - ax) * dx + (py - ay) * dy) / ll if ll else 0.0
                t = 0.0 if t < 0 else 1.0 if t > 1 else t
                ex = px - ax - t * dx
                ey = py - ay - t * dy
                d = math.sqrt(ex * ex + ey * ey)
                if d < dist[base + i]:
                    dist[base + i] = d
    for j in range(h):
        yc = y0 + j + 0.5
        xs = []
        for k in range(n):
            ax, ay = poly[k]
            bx, by = poly[(k + 1) % n]
            if (ay <= yc) != (by <= yc):
                xs.append(ax + (yc - ay) * (bx - ax) / (by - ay))
        xs.sort()
        for k in range(0, len(xs) - 1, 2):
            a = max(0, int(math.ceil(xs[k] - x0 - 0.5)))
            b = min(w - 1, int(math.floor(xs[k + 1] - x0 - 0.5)))
            for i in range(a, b + 1):
                dist[j * w + i] = -min(dist[j * w + i], far)
    return Plane(x0, y0, w, h, dist)


def ramp(value, lo, hi):
    """1 at value <= lo, 0 at value >= hi, linear between."""
    if value <= lo:
        return 1.0
    if value >= hi:
        return 0.0
    return (hi - value) / (hi - lo)


def edt_1d(f, n):
    """Squared Euclidean distance transform of one line of squared costs (Felzenszwalb and Huttenlocher)."""
    d = [0.0] * n
    v = [0] * n
    z = [0.0] * (n + 1)
    k = 0
    z[0], z[1] = -1e30, 1e30
    for q in range(1, n):
        s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k])
        while s <= z[k]:
            k -= 1
            s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k])
        k += 1
        v[k] = q
        z[k] = s
        z[k + 1] = 1e30
    k = 0
    for q in range(n):
        while z[k + 1] < q:
            k += 1
        d[q] = (q - v[k]) ** 2 + f[v[k]]
    return d


def edt(inside, w, h):
    """Distance (px) from every pixel centre of a w x h mask to the nearest pixel centre where inside is false."""
    big = 1e12
    cols = [[0.0 if not inside[y * w + x] else big for y in range(h)] for x in range(w)]
    cols = [edt_1d(c, h) for c in cols]
    out = array("d", bytes(8 * w * h))
    for y in range(h):
        row = edt_1d([cols[x][y] for x in range(w)], w)
        out[y * w:(y + 1) * w] = array("d", [math.sqrt(v) for v in row])
    return out


def mask_distance(mask, box, reach):
    """Signed distance (px) from pixel centres in box to the edge of a mask plane (true inside): negative inside,
    clamped to +-(reach + 1). The mask is given as a Plane covering at least box; pixels outside it count as outside."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    m = [False] * (w * h)
    for y in range(y0, y1):
        for x in range(x0, x1):
            if mask.x0 <= x < mask.x0 + mask.w and mask.y0 <= y < mask.y0 + mask.h and mask.at(x, y) > 0.5:
                m[(y - y0) * w + (x - x0)] = True
    outside = edt([not q for q in m], w, h)  # distance to the nearest mask pixel
    inside = edt(m, w, h)  # distance to the nearest non-mask pixel
    far = reach + 1.0
    dist = array("d", [-min(i - 0.5, far) if q else min(o - 0.5, far) for q, i, o in zip(m, inside, outside)])
    return Plane(x0, y0, w, h, dist)


def poly_box(poly, margin, size):
    xs = [p[0] for p in poly]
    ys = [p[1] for p in poly]
    return (max(0, int(min(xs)) - margin), max(0, int(min(ys)) - margin),
            min(size[0], int(math.ceil(max(xs))) + margin), min(size[1], int(math.ceil(max(ys))) + margin))


# ------------------------------------------------------------------------------------------------ registration
def nelder_mead(f, x0, step, iters):
    n = len(x0)
    pts = [list(x0)]
    for i in range(n):
        p = list(x0)
        p[i] += step[i]
        pts.append(p)
    vals = [f(p) for p in pts]
    for _ in range(iters):
        order = sorted(range(n + 1), key=lambda i: vals[i])
        pts = [pts[i] for i in order]
        vals = [vals[i] for i in order]
        if abs(vals[-1] - vals[0]) < 1e-10:
            break
        cen = [sum(p[i] for p in pts[:-1]) / n for i in range(n)]
        worst = pts[-1]
        xr = [c + (c - q) for c, q in zip(cen, worst)]
        fr = f(xr)
        if fr < vals[0]:
            xe = [c + 2 * (c - q) for c, q in zip(cen, worst)]
            fe = f(xe)
            pts[-1], vals[-1] = (xe, fe) if fe < fr else (xr, fr)
        elif fr < vals[-2]:
            pts[-1], vals[-1] = xr, fr
        else:
            xc = [c + 0.5 * (q - c) for c, q in zip(cen, worst)]
            fc = f(xc)
            if fc < vals[-1]:
                pts[-1], vals[-1] = xc, fc
            else:
                for i in range(1, n + 1):
                    pts[i] = [a + 0.5 * (b - a) for a, b in zip(pts[0], pts[i])]
                    vals[i] = f(pts[i])
    best = min(range(n + 1), key=lambda i: vals[i])
    return pts[best], vals[best]


def nominal_affine(box, donor_w):
    """The upscale's own mapping: pixel index i has centre i + 0.5; donor index u has centre u + 0.5."""
    bx0, by0, bx1, by1 = box
    scale = donor_w / (bx1 - bx0)
    cx, cy = (bx0 + bx1) / 2.0, (by0 + by1) / 2.0
    return [scale, 0.0, 0.0, scale, (cx + 0.5 - bx0) * scale - 0.5, (cy + 0.5 - by0) * scale - 0.5], (cx, cy)


def register(concept_luma, donor_luma, box, use_sample, search=4):
    """Affine donor_px = M (p - c) + t (p concept pixel index) maximising the NCC of high-passed luminance on the
    3 px grid points of box (inset 8 px) for which use_sample(x, y) is true."""
    bx0, by0, bx1, by1 = box
    scale = donor_luma.w / (bx1 - bx0)
    dl = highpass(blur(donor_luma, 0.45 * scale), 6 * scale)
    cl = highpass(concept_luma, 6)
    cx, cy = (bx0 + bx1) / 2.0, (by0 + by1) / 2.0
    samples = []
    for y in range(by0 + 8, by1 - 8, 3):
        for x in range(bx0 + 8, bx1 - 8, 3):
            if use_sample(x, y):
                samples.append((x - cx, y - cy, cl.at(x, y)))
    mean = sum(s[2] for s in samples) / len(samples)
    sd = math.sqrt(sum((s[2] - mean) ** 2 for s in samples) / len(samples))
    pts = [(s[0], s[1], (s[2] - mean) / sd) for s in samples]

    def cost(p):
        m00, m01, m10, m11, tx, ty = p
        vals = [bilinear(dl, m00 * dx + m01 * dy + tx, m10 * dx + m11 * dy + ty) for dx, dy, _ in pts]
        mu = sum(vals) / len(vals)
        var = sum((v - mu) ** 2 for v in vals) / len(vals)
        cov = sum((v - mu) * s for v, (_, _, s) in zip(vals, pts)) / len(vals)
        return -cov / (math.sqrt(var) + 1e-9)

    p0, _ = nominal_affine(box, donor_luma.w)
    best = (cost(p0), p0)
    offsets = range(-search, search + 1, 2)
    for ox in offsets:
        for oy in offsets:
            p = p0[:4] + [p0[4] + ox, p0[5] + oy]
            c = cost(p)
            if c < best[0]:
                best = (c, p)
    p, _ = nelder_mead(cost, best[1], [0.02, 0.02, 0.02, 0.02, 1.0, 1.0], 300)
    p, fval = nelder_mead(cost, p, [0.004, 0.004, 0.004, 0.004, 0.25, 0.25], 300)
    return p, {"c": [cx, cy], "M": [[p[0], p[1]], [p[2], p[3]]], "t": [p[4], p[5]],
               "ncc_nominal": -cost(p0), "ncc_registered": -fval, "samples": len(pts)}


def warp(donor_rgb, p, box, centre, scale):
    """Donor colour (anti-aliased by its upscale) at every concept pixel of box, as float planes."""
    pre = [blur(c, 0.45 * scale) for c in donor_rgb]
    x0, y0, x1, y1 = box
    m00, m01, m10, m11, tx, ty = p
    out = [Plane(x0, y0, x1 - x0, y1 - y0) for _ in range(3)]
    for y in range(y0, y1):
        dy = y - centre[1]
        for x in range(x0, x1):
            dx = x - centre[0]
            u = m00 * dx + m01 * dy + tx
            v = m10 * dx + m11 * dy + ty
            i = (y - y0) * (x1 - x0) + (x - x0)
            for c in range(3):
                out[c].data[i] = bilinear(pre[c], u, v)
    return out


def match_low_frequency(donor, source, valid, sigmas):
    """Add the low-frequency difference source - donor measured on valid pixels, extrapolated into the invalid
    area coarse to fine (masked Gaussian averages; confidence from the valid weight)."""
    out = []
    weight = valid.like(array("d", valid.data))
    dens = {s: blur(weight, s) for s in sigmas}
    for d, s_ in zip(donor, source):
        diff = d.like(array("d", [(b - a) * w for a, b, w in zip(d.data, s_.data, weight.data)]))
        est = None
        for s in sorted(sigmas, reverse=True):
            num = blur(diff, s).data
            den = dens[s].data
            cur = array("d", [n / max(q, 1e-9) for n, q in zip(num, den)])
            if est is None:
                est = cur
            else:
                est = array("d", [e * (1 - min(q / 0.3, 1.0)) + c * min(q / 0.3, 1.0)
                                  for e, c, q in zip(est, cur, den)])
        out.append(d.like(array("d", [a + e for a, e in zip(d.data, est)])))
    return out


def transfer(base, width, donor_path, box, poly_dist, full, declared, exclude, sigmas, register_against):
    """Register the donor over box and blend it into a copy of base (RGB bytes) inside poly_dist < declared.
    poly_dist is the signed distance (negative inside) to the edited shape: a polygon or the nymph's silhouette."""
    dw, dh, dc, dpix = read_png(donor_path)
    if (dw, dh) != (1024, 1536):
        raise ValueError(f"{donor_path} must be 1024 x 1536.")
    donor_rgb = planes_from(dpix, dw, dc, (0, 0, dw, dh), 3)
    concept_luma = luminance(planes_from(register_against, width, 3, box))

    def away_from_edit(x, y):
        inside = poly_dist.x0 <= x < poly_dist.x0 + poly_dist.w and poly_dist.y0 <= y < poly_dist.y0 + poly_dist.h
        return not inside or poly_dist.at(x, y) >= exclude
    p, info = register(concept_luma, luminance(donor_rgb), box, away_from_edit)
    scale = dw / (box[2] - box[0])
    warped = warp(donor_rgb, p, box, info["c"], scale)
    source = planes_from(base, width, 3, box)
    valid = Plane(box[0], box[1], box[2] - box[0], box[3] - box[1])
    for y in range(box[1], box[3]):
        for x in range(box[0], box[2]):
            inside = poly_dist.x0 <= x < poly_dist.x0 + poly_dist.w and poly_dist.y0 <= y < poly_dist.y0 + poly_dist.h
            if not inside or poly_dist.at(x, y) >= declared:
                valid.data[(y - box[1]) * valid.w + (x - box[0])] = 1.0
    matched = match_low_frequency(warped, source, valid, sigmas)
    out = bytearray(base)
    declared_mask = bytearray(len(base) // 3)
    weights = array("d", bytes(8 * (len(base) // 3)))
    for y in range(poly_dist.y0, poly_dist.y0 + poly_dist.h):
        for x in range(poly_dist.x0, poly_dist.x0 + poly_dist.w):
            d = poly_dist.at(x, y)
            if d >= declared:
                continue
            if not (box[0] <= x < box[2] and box[1] <= y < box[3]):
                raise ValueError("A declared region leaves its donor's target box.")
            w = ramp(d, full, declared)
            i = y * width + x
            k = (y - box[1]) * (box[2] - box[0]) + (x - box[0])
            declared_mask[i] = 1
            weights[i] = w
            for c in range(3):
                v = base[i * 3 + c] * (1 - w) + matched[c].data[k] * w
                out[i * 3 + c] = min(255, max(0, int(round(v))))
    info["donor"] = rel(donor_path)
    info["target_box_xyxy"] = list(box)
    return out, declared_mask, weights, info


# ------------------------------------------------------------------------------------------------------ nymph
def cut_nymph(spec, concept, width, size, sigmas):
    """Register the nymph extraction to the concept frame and match its colour to the concept on her visible pixels.
    Returns her RGBA bytes over the full frame, her alpha as a Plane over the target box and the registration."""
    donor_path = ROOT / spec["donor"]
    dw, dh, dc, dpix = read_png(donor_path)
    if (dw, dh, dc) != (1024, 1536, 4):
        raise ValueError(f"{donor_path} must be a 1024 x 1536 RGBA PNG.")
    box = tuple(spec["target_box_xyxy"])
    x0, y0, x1, y1 = box
    bw, bh = x1 - x0, y1 - y0
    scale = dw / bw
    lo, hi = spec["alpha_levels"]
    r, g, b, a = planes_from(dpix, dw, dc, (0, 0, dw, dh))
    alpha = a.like(array("d", [min(max((v - lo) / (hi - lo), 0.0), 1.0) for v in a.data]))
    premult = [c.like(array("d", [v * q for v, q in zip(c.data, alpha.data)])) for c in (r, g, b)]

    # registration on her interior, located through the upscale's own mapping
    p0, centre = nominal_affine(box, dw)
    nominal = Plane(x0, y0, bw, bh)
    for y in range(y0, y1):
        for x in range(x0, x1):
            u = p0[0] * (x - centre[0]) + p0[4]
            v = p0[3] * (y - centre[1]) + p0[5]
            nominal.data[(y - y0) * bw + (x - x0)] = 1.0 if bilinear(alpha, u, v) >= 0.5 else 0.0
    inset = spec["registration_inset_px"]
    nominal_sd = mask_distance(nominal, box, inset + 2)
    concept_luma = luminance(planes_from(concept, width, 3, box))
    p, info = register(concept_luma, luminance([r, g, b]), box, lambda x, y: nominal_sd.at(x, y) <= -inset,
                       spec["registration_search_px"])
    info["donor"] = rel(donor_path)
    info["target_box_xyxy"] = list(box)

    # warp premultiplied colour and alpha (anti-aliased by the upscale) into the concept frame
    pre = [blur(c, 0.45 * scale) for c in premult + [alpha]]
    m00, m01, m10, m11, tx, ty = p
    warped = [Plane(x0, y0, bw, bh) for _ in range(4)]
    for y in range(y0, y1):
        dy = y - centre[1]
        for x in range(x0, x1):
            dx = x - centre[0]
            u = m00 * dx + m01 * dy + tx
            v = m10 * dx + m11 * dy + ty
            if u < 0 or v < 0 or u > dw - 1 or v > dh - 1:
                continue
            k = (y - y0) * bw + (x - x0)
            for c in range(4):
                warped[c].data[k] = bilinear(pre[c], u, v)
    A = warped[3]
    for k in range(bw * bh):
        A.data[k] = min(max(A.data[k], 0.0), 1.0)

    # colour: unpremultiplied where she is present, spread outward a few px for bilinear filtering at her edge
    fill_sigma = spec["edge_fill_sigma_px"]
    spread = [blur(c, fill_sigma) for c in warped]
    colour = [Plane(x0, y0, bw, bh) for _ in range(3)]
    has_colour = bytearray(bw * bh)
    for k in range(bw * bh):
        if A.data[k] > 0.05:
            for c in range(3):
                colour[c].data[k] = warped[c].data[k] / A.data[k]
            has_colour[k] = 1
        elif spread[3].data[k] > 1e-3:
            for c in range(3):
                colour[c].data[k] = spread[c].data[k] / spread[3].data[k]
            has_colour[k] = 1

    # low-frequency colour match on her visible pixels: inset from her edge, without pixels the concept hides
    silhouette = A.like(array("d", [1.0 if v >= 0.5 else 0.0 for v in A.data]))
    vis_inset = spec["visible_inset_px"]
    sd = mask_distance(silhouette, box, vis_inset + 2)
    source = planes_from(concept, width, 3, box)
    valid = A.like(array("d", [1.0 if d <= -vis_inset else 0.0 for d in sd.data]))
    first = match_low_frequency(colour, source, valid, sigmas[:1])
    residual = [blur(f.like(array("d", [p_ - s_ for p_, s_ in zip(f.data, s.data)])), 1.5)
                for f, s in zip(first, source)]
    limit = spec["occlusion_difference"]
    visible = valid.like(array("d", [1.0 if v > 0 and max(abs(rc.data[k]) for rc in residual) < limit else 0.0
                                     for k, v in enumerate(valid.data)]))
    matched = match_low_frequency(colour, source, visible, sigmas)
    info["visible_pixels"] = int(sum(visible.data))
    info["occluded_pixels_excluded"] = int(sum(valid.data) - sum(visible.data))

    rgba = bytearray(width * size[1] * 4)
    for y in range(y0, y1):
        for x in range(x0, x1):
            k = (y - y0) * bw + (x - x0)
            i = (y * width + x) * 4
            if has_colour[k]:
                for c in range(3):
                    rgba[i + c] = min(255, max(0, int(round(matched[c].data[k]))))
            rgba[i + 3] = int(round(A.data[k] * 255))
    return rgba, info


# --------------------------------------------------------------------------------------------------- outputs
def rel(path):
    return str(Path(path).resolve().relative_to(ROOT)).replace("\\", "/")


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def used_guids():
    used = set()
    for meta in (ROOT / "Dice-Unity/Assets").rglob("*.meta"):
        for line in meta.read_text(encoding="utf-8", errors="ignore").splitlines()[:3]:
            if line.startswith("guid:"):
                used.add(line.split(":", 1)[1].strip())
    return used


def write_meta(path, template, used):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return False
    guid = uuid.uuid4().hex
    while guid in used:
        guid = uuid.uuid4().hex
    used.add(guid)
    lines = template.read_text(encoding="ascii").splitlines()
    lines = ["guid: " + guid if line.startswith("guid:") else line for line in lines]
    meta.write_text("\n".join(lines) + "\n", encoding="ascii", newline="\n")
    return True


def crop(pixels, width, channels, box):
    x0, y0, x1, y1 = box
    out = bytearray()
    for y in range(y0, y1):
        out += pixels[(y * width + x0) * channels:(y * width + x1) * channels]
    return out


def upscale(pixels, w, h, channels, s):
    """Nearest-neighbour enlargement by an integer factor (review images only)."""
    out = bytearray()
    for y in range(h):
        row = bytearray()
        for x in range(w):
            row += pixels[(y * w + x) * channels:(y * w + x + 1) * channels] * s
        out += bytes(row) * s
    return out


def outline(declared, width, height):
    """Pixels of a declared region that touch a pixel outside it."""
    edge = bytearray(len(declared))
    for y in range(height):
        for x in range(width):
            i = y * width + x
            if declared[i] and ((x == 0 or not declared[i - 1]) or (x == width - 1 or not declared[i + 1])
                                or (y == 0 or not declared[i - width]) or (y == height - 1 or not declared[i + width])):
                edge[i] = 1
    return edge


def difference_image(a, b, declared, width, height, box, gain=4):
    """|a - b| (max over RGB) x gain as an amber heat map, with the declared region's edge in cyan."""
    edge = outline(declared, width, height)
    x0, y0, x1, y1 = box
    out = bytearray()
    for y in range(y0, y1):
        for x in range(x0, x1):
            i = y * width + x
            if edge[i]:
                out += bytes((0, 200, 255))
                continue
            d = max(abs(a[i * 3 + c] - b[i * 3 + c]) for c in range(3))
            v = min(255, d * gain)
            out += bytes((v, v * 3 // 5, 0))
    return out


def over(rgba_alpha, colour, background_rgb, width, box, bg=None):
    """Composite colour (RGB bytes) through alpha (floats 0..1) over a background (RGB bytes or a callable)."""
    x0, y0, x1, y1 = box
    out = bytearray()
    for y in range(y0, y1):
        for x in range(x0, x1):
            i = y * width + x
            a = rgba_alpha[i]
            back = bg(x, y) if bg else background_rgb[i * 3:i * 3 + 3]
            for c in range(3):
                out.append(min(255, max(0, int(round(colour[i * 3 + c] * a + back[c] * (1 - a))))))
    return out


def side_by_side(left, right, w, h):
    """Two w x h RGB crops joined horizontally."""
    out = bytearray()
    for y in range(h):
        out += left[y * w * 3:(y + 1) * w * 3] + right[y * w * 3:(y + 1) * w * 3]
    return out


def moved(rgba, background_rgb, width, box, inverse, margin=40):
    """RGBA bytes moved by an inverse mapping (output pixel centre -> source position) and composited over a
    background (RGB bytes) inside box, sampling premultiplied colour bilinearly."""
    x0, y0, x1, y1 = box
    height = len(background_rgb) // (3 * width)
    sx0, sy0 = max(0, x0 - margin), max(0, y0 - margin)
    sx1, sy1 = min(width, x1 + margin), min(height, y1 + margin)
    planes = [Plane(sx0, sy0, sx1 - sx0, sy1 - sy0) for _ in range(4)]
    for y in range(sy0, sy1):
        for x in range(sx0, sx1):
            i = (y * width + x) * 4
            k = (y - sy0) * (sx1 - sx0) + (x - sx0)
            a = rgba[i + 3] / 255.0
            for c in range(3):
                planes[c].data[k] = rgba[i + c] * a
            planes[3].data[k] = a
    out = bytearray()
    for y in range(y0, y1):
        for x in range(x0, x1):
            sx, sy = inverse(x + 0.5, y + 0.5)
            u, v = sx - 0.5 - sx0, sy - 0.5 - sy0
            a = bilinear(planes[3], u, v)
            i = (y * width + x) * 3
            for c in range(3):
                out.append(min(255, max(0, int(round(bilinear(planes[c], u, v) + background_rgb[i + c] * (1 - a))))))
    return out


def build(out_dir, write_assets):
    spec = json.loads(SPEC.read_text(encoding="utf-8"))
    width, height = spec["size"]
    size = (width, height)
    concept_path = ROOT / spec["concept"]
    concept_sha = sha256(concept_path)
    cw, chh, cc, concept = read_png(concept_path)
    if (cw, chh, cc) != (width, height, 3):
        raise ValueError("The concept must be a 941 x 1672 RGB PNG.")
    sigmas = spec["registration"]["low_frequency_sigmas"]
    report = {"concept": spec["concept"], "concept_sha256": concept_sha}

    # 1. nymph layer: the registered, colour-matched extraction
    bg = spec["background"]
    nymph = spec["nymph"]
    print("Registering the nymph donor...", flush=True)
    nymph_rgba, info = cut_nymph(nymph, concept, width, size, sigmas)
    report["nymph"] = {"registration": info}
    alpha_q = array("d", [nymph_rgba[i * 4 + 3] / 255.0 for i in range(width * height)])

    # her silhouette: every pixel at or above the silhouette alpha
    threshold = int(math.ceil(nymph["silhouette_alpha"] * 255))
    nx0, ny0, nx1, ny1 = nymph["target_box_xyxy"]
    silhouette = Plane(nx0, ny0, nx1 - nx0, ny1 - ny0)
    xs, ys = [], []
    for y in range(ny0, ny1):
        for x in range(nx0, nx1):
            if nymph_rgba[(y * width + x) * 4 + 3] >= threshold:
                silhouette.data[(y - ny0) * silhouette.w + (x - nx0)] = 1.0
                xs.append(x)
                ys.append(y)
    reach = max(bg["declared_px"], bg["registration_exclude_px"]) + 4
    sil_box = (max(0, min(xs) - reach), max(0, min(ys) - reach), min(width, max(xs) + 1 + reach),
               min(height, max(ys) + 1 + reach))
    nymph_sd = mask_distance(silhouette, sil_box, reach)

    # 2. clean plate: the nymph removed within declared_px of her silhouette
    print("Registering the background donor...", flush=True)
    plate, plate_declared, plate_weight, info = transfer(
        concept, width, ROOT / bg["donor"], tuple(bg["target_box_xyxy"]), nymph_sd, bg["full_weight_px"],
        bg["declared_px"], bg["registration_exclude_px"], sigmas, concept)
    report["background"] = {"registration": info}

    # 3. faceless trunk: the face replaced by plain bark, into the plate
    face = spec["faceless"]
    print("Registering the faceless donor...", flush=True)
    face_box = poly_box(face["face"], DISTANCE_REACH + 2, size)
    face_sd = polygon_distance(face["face"], face_box)
    faceless, face_declared, face_weight, info = transfer(
        plate, width, ROOT / face["donor"], tuple(face["target_box_xyxy"]), face_sd, face["full_weight_px"],
        face["declared_px"], face["registration_exclude_px"], sigmas, concept)
    report["faceless"] = {"registration": info}

    # 4. face mask: 1 over the whole transfer region, a soft edge outside it
    mask = bytearray(width * height * 4)
    edge = face["mask_edge_px"]
    for i in range(width * height):
        mask[i * 4:i * 4 + 3] = b"\xff\xff\xff"
    face_alpha = array("d", bytes(8 * width * height))
    for y in range(face_sd.y0, face_sd.y0 + face_sd.h):
        for x in range(face_sd.x0, face_sd.x0 + face_sd.w):
            m = ramp(face_sd.at(x, y), face["declared_px"], face["declared_px"] + edge)
            mask[(y * width + x) * 4 + 3] = int(round(m * 255))
            face_alpha[y * width + x] = mask[(y * width + x) * 4 + 3] / 255.0

    # ---- write
    layers = out_dir / "layers"
    files = {"background": layers / bg["output"], "nymph": layers / nymph["output"],
             "faceless": layers / face["output"], "mask": layers / face["mask_output"]}
    write_png(files["background"], width, height, 3, plate)
    write_png(files["nymph"], width, height, 4, nymph_rgba)
    write_png(files["faceless"], width, height, 3, faceless)
    write_png(files["mask"], width, height, 4, mask)

    # ---- verify from the written files
    print("Verifying...", flush=True)
    _, _, _, p2 = read_png(files["background"])
    _, _, _, n2 = read_png(files["nymph"])
    _, _, _, f2 = read_png(files["faceless"])
    _, _, _, m2 = read_png(files["mask"])
    n = width * height

    def outside_identical(a, b, declared):
        return all(a[i * 3:i * 3 + 3] == b[i * 3:i * 3 + 3] for i in range(n) if not declared[i])

    changed = lambda a, b: sum(1 for i in range(n) if a[i * 3:i * 3 + 3] != b[i * 3:i * 3 + 3])
    ver = {
        "concept_unchanged": sha256(concept_path) == concept_sha,
        "background_outside_declared_identical_to_concept": outside_identical(p2, concept, plate_declared),
        "background_declared_pixels": sum(plate_declared),
        "background_changed_pixels": changed(p2, concept),
        "faceless_outside_declared_identical_to_background": outside_identical(f2, p2, face_declared),
        "faceless_declared_pixels": sum(face_declared),
        "faceless_changed_pixels": changed(f2, p2),
    }
    # nymph over the plate against the concept, inside her silhouette (alpha >= 0.5)
    err_sil = err_opaque = err_soft = 0.0
    cnt_sil = cnt_opaque = cnt_soft = 0
    max_err = 0
    for i in range(n):
        a8 = n2[i * 4 + 3]
        if a8 == 0:
            continue
        a = a8 / 255.0
        e = 0.0
        for c in range(3):
            v = n2[i * 4 + c] * a + p2[i * 3 + c] * (1 - a)
            d = abs(v - concept[i * 3 + c])
            e += d
            if a8 >= 128:
                max_err = max(max_err, d)
        e /= 3
        if a8 >= 128:
            err_sil += e
            cnt_sil += 1
        if a8 == 255:
            err_opaque += e
            cnt_opaque += 1
        else:
            err_soft += e
            cnt_soft += 1
    margin = nymph["motion_margin_px"]
    covered = all(plate_weight[y * width + x] == 1.0
                  for y in range(nymph_sd.y0, nymph_sd.y0 + nymph_sd.h)
                  for x in range(nymph_sd.x0, nymph_sd.x0 + nymph_sd.w) if nymph_sd.at(x, y) < margin)
    ver["nymph"] = {
        "silhouette_pixels_alpha_ge_half": cnt_sil, "opaque_pixels": cnt_opaque, "partial_pixels": cnt_soft,
        "composite_mae_inside_silhouette": round(err_sil / max(cnt_sil, 1), 4),
        "composite_mae_opaque": round(err_opaque / max(cnt_opaque, 1), 4),
        "composite_mae_partial_alpha": round(err_soft / max(cnt_soft, 1), 4),
        "composite_max_abs_error_inside_silhouette": round(max_err, 2),
        "plate_full_weight_within_motion_margin": covered,
        "note": "0..255 per channel; nymph composited over BoundTreeBackground compared with the concept; the "
                "silhouette includes her hem and feet where the concept shows leaves over them"}
    # face layer at full opacity over the faceless trunk reproduces the plate
    face_err = 0
    for i in range(n):
        if face_declared[i]:
            a = m2[i * 4 + 3] / 255.0
            for c in range(3):
                face_err = max(face_err, abs(p2[i * 3 + c] * a + f2[i * 3 + c] * (1 - a) - p2[i * 3 + c]))
    ver["face_mask_opaque_over_declared"] = all(m2[i * 4 + 3] == 255 for i in range(n) if face_declared[i])
    ver["face_over_faceless_max_abs_error"] = round(face_err, 3)

    def glow(img):
        count = 0
        for i in range(n):
            if face_declared[i]:
                r, g, b = img[i * 3], img[i * 3 + 1], img[i * 3 + 2]
                if g > 60 and g - max(r, b) > 12:
                    count += 1
        return count
    ver["glow_pixels_in_face_region"] = {"background": glow(p2), "faceless": glow(f2),
                                         "rule": "G > 60 and G - max(R, B) > 12"}
    report["verification"] = ver

    # ---- review images
    print("Writing review images...", flush=True)
    review = out_dir / "review"
    nbox = (60, 590, 460, 1050)
    fbox = (300, 110, 700, 710)
    nw, nh = nbox[2] - nbox[0], nbox[3] - nbox[1]
    fw, fh = fbox[2] - fbox[0], fbox[3] - fbox[1]
    write_png(review / "plate.png", width, height, 3, p2)
    write_png(review / "plate-nymph-region-2x.png", nw * 2, nh * 2, 3, upscale(crop(p2, width, 3, nbox), nw, nh, 3, 2))
    concept_crop = crop(concept, width, 3, nbox)
    write_png(review / "concept-nymph-region-2x.png", nw * 2, nh * 2, 3, upscale(concept_crop, nw, nh, 3, 2))
    colour = bytearray(width * height * 3)
    for i in range(n):
        colour[i * 3:i * 3 + 3] = n2[i * 4:i * 4 + 3]
    comp = over(alpha_q, colour, p2, width, nbox)
    write_png(review / "nymph-over-plate-2x.png", nw * 2, nh * 2, 3, upscale(comp, nw, nh, 3, 2))
    write_png(review / "concept-vs-nymph-over-plate-2x.png", nw * 4, nh * 2, 3,
              upscale(side_by_side(concept_crop, comp, nw, nh), nw * 2, nh, 3, 2))
    grey = over(alpha_q, colour, None, width, nbox, bg=lambda x, y: (128, 128, 128))
    write_png(review / "nymph-on-grey-2x.png", nw * 2, nh * 2, 3, upscale(grey, nw, nh, 3, 2))
    checker = over(alpha_q, colour, None, width, nbox,
                   bg=lambda x, y: (200, 200, 200) if ((x // 6 + y // 6) % 2) else (90, 90, 90))
    write_png(review / "nymph-on-checker-2x.png", nw * 2, nh * 2, 3, upscale(checker, nw, nh, 3, 2))
    matte = bytearray(min(255, int(round(alpha_q[y * width + x] * 255))) for y in range(nbox[1], nbox[3])
                      for x in range(nbox[0], nbox[2]))
    write_png(review / "nymph-matte-2x.png", nw * 2, nh * 2, 1, upscale(matte, nw, nh, 1, 2))
    comp_err = bytearray()
    for y in range(nbox[1], nbox[3]):
        for x in range(nbox[0], nbox[2]):
            i = y * width + x
            d = max(abs(comp[((y - nbox[1]) * nw + (x - nbox[0])) * 3 + c] - concept[i * 3 + c]) for c in range(3))
            v = min(255, d * 4)
            comp_err += bytes((v, v * 3 // 5, 0))
    write_png(review / "nymph-over-plate-error-x4-2x.png", nw * 2, nh * 2, 3, upscale(comp_err, nw, nh, 3, 2))
    write_png(review / "diff-background-vs-concept-x4-2x.png", nw * 2, nh * 2, 3,
              upscale(difference_image(p2, concept, plate_declared, width, height, nbox), nw, nh, 3, 2))
    # the nymph moved over the plate: offsets and a scale about her seat (bottom centre of her silhouette)
    sil = [(i % width, i // width) for i in range(n) if n2[i * 4 + 3] >= 128]
    seat = ((min(p[0] for p in sil) + max(p[0] for p in sil) + 1) / 2.0, max(p[1] for p in sil) + 1.0)
    report["nymph"]["seat"] = list(seat)
    for name, (ox, oy, s) in REVIEW_MOTIONS.items():
        moved_comp = moved(n2, p2, width, nbox, lambda x, y: (seat[0] + (x - ox - seat[0]) / s,
                                                              seat[1] + (y - oy - seat[1]) / s))
        write_png(review / f"motion-{name}-2x.png", nw * 2, nh * 2, 3, upscale(moved_comp, nw, nh, 3, 2))
    write_png(review / "faceless.png", width, height, 3, f2)
    write_png(review / "faceless-face-region-2x.png", fw * 2, fh * 2, 3, upscale(crop(f2, width, 3, fbox), fw, fh, 3, 2))
    write_png(review / "background-face-region-2x.png", fw * 2, fh * 2, 3,
              upscale(crop(p2, width, 3, fbox), fw, fh, 3, 2))
    face_comp = over(face_alpha, p2, f2, width, fbox)
    write_png(review / "face-over-faceless-2x.png", fw * 2, fh * 2, 3, upscale(face_comp, fw, fh, 3, 2))
    fade = over(array("d", [a * 0.5 for a in face_alpha]), p2, f2, width, fbox)
    write_png(review / "face-over-faceless-half-2x.png", fw * 2, fh * 2, 3, upscale(fade, fw, fh, 3, 2))
    write_png(review / "diff-faceless-vs-background-x4-2x.png", fw * 2, fh * 2, 3,
              upscale(difference_image(f2, p2, face_declared, width, height, fbox), fw, fh, 3, 2))

    report["files"] = {k: rel(v) for k, v in files.items()}
    report["review"] = rel(review)
    if write_assets:
        template = ART / "BoundTreeOriginal.png.meta"
        used = used_guids()
        installed = {}
        for key, name in (("background", bg["output"]), ("nymph", nymph["output"]),
                          ("faceless", face["output"]), ("mask", face["mask_output"])):
            target = ART / name
            target.write_bytes(files[key].read_bytes())
            installed[name] = {"sha256": sha256(target), "meta_written": write_meta(target, template, used)}
        report["installed"] = installed
    (out_dir / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="ascii")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", help="unused directory under Docs/work for the layers, review images and report")
    parser.add_argument("--write-assets", action="store_true", help="also replace the four installed textures")
    args = parser.parse_args()
    if args.output:
        out_dir = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in out_dir.parents:
            raise ValueError("--output must be under Docs/work.")
        out_dir.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        out_dir = Path(tempfile.mkdtemp(prefix="bound-tree-layers-", dir=WORK))
    report = build(out_dir, args.write_assets)
    print(json.dumps(report["verification"], indent=2))
    print(f"Wrote {out_dir}")


if __name__ == "__main__":
    main()
