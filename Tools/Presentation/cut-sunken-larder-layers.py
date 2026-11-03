"""Cut the Sunken Larder scene concept into its first art layers.

Builds two 941 x 1672 textures in the concept's frame from SunkenLarderOriginal.png, the preserved generated donors
and the boxes and distances declared in Docs/Reference/Art/SunkenLarderLayers.json:

  SunkenLarderBackground.png  RGB   the concept with the old woman removed (clean plate)
  SunkenLarderHag.png         RGBA  the old woman: the generated transparent extraction, registered and colour-matched

This is the Bound Tree method (Tools/Presentation/cut-bound-tree-layers.py, whose PNG, registration, colour match
and review functions it imports). Its extraction cut and plate transfer are that script's with one difference: the
donor is prefiltered with an exact Gaussian (sigma 0.45 donor px per unit of upscale) before it is sampled, since the
Bound Tree three-box approximation would blur this target's 1.22x upscale by more than a concept pixel. The plate's
colour match also starts at a coarser sigma (SunkenLarderLayers.json), so it reaches the middle of the wide removal
region. Each donor is an edit of the same Lanczos 1024 x 1536 upscale of a concept box, registered to the concept
frame by an affine fit on high-passed luminance. The hag donor is her extraction on a transparent background, fitted
on her interior: its alpha is her matte and its colour, low-frequency matched to the concept on her visible pixels, is
her colour. Her silhouette (her alpha above a threshold) defines the plate's
removal region. The plate donor is fitted around her (her silhouette excluded), low-frequency matched to the
surrounding concept and transferred only within the declared distance of her silhouette, blending from full weight
near it to zero at the declared edge. Every pixel outside the declared region is copied byte for byte. The script
verifies byte identity outside it, reports the composite error and the motion margin and writes review images,
including her moved by small offsets and a scale about her seat over the plate.

Python 3.9 or later, no third-party packages. A full run takes several minutes. Run from any directory:

  python Tools/Presentation/cut-sunken-larder-layers.py
  python Tools/Presentation/cut-sunken-larder-layers.py --write-assets

The default writes the textures, review images and report.json to a fresh ignored Docs/work/sunken-larder-layers-*
directory; --output chooses an unused directory under Docs/work. --donor-hag and --donor-background substitute a
candidate donor (an edit of the same target) for review. --write-assets also replaces the two textures under
Dice-Unity/Assets/Dice/Art/SunkenLarder, keeps existing .meta files and writes a .meta (a copy of
SunkenLarderOriginal.png.meta with a fresh GUID) for a texture that has none. The concept and donors are only read.
"""

import argparse
from array import array
import importlib.util
import json
import math
from pathlib import Path
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SPEC = ROOT / "Docs/Reference/Art/SunkenLarderLayers.json"
ART = ROOT / "Dice-Unity/Assets/Dice/Art/SunkenLarder"
WORK = ROOT / "Docs/work"
# review renders of the hag moved over the plate: name -> (x offset px, y offset px, scale about her seat)
REVIEW_MOTIONS = {"left4": (-4, 0, 1.0), "right4": (4, 0, 1.0), "up6": (0, -6, 1.0), "scale103": (0, 0, 1.03)}


def load_bound_tree():
    path = Path(__file__).resolve().parent / "cut-bound-tree-layers.py"
    spec = importlib.util.spec_from_file_location("cut_bound_tree_layers", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bt = load_bound_tree()


def prefilter(p, sigma):
    """Exact separable Gaussian (kernel to 3 sigma, clamped edges): the anti-aliasing applied to a donor before it is
    sampled at concept resolution. The Bound Tree warp's three-box approximation cannot go below a sigma of about
    1.4 donor px, which at this target's 1.22x upscale would soften the donor by more than a concept pixel."""
    if sigma <= 0.05:
        return p.like(array("d", p.data))
    radius = max(1, int(math.ceil(3 * sigma)))
    kernel = [math.exp(-0.5 * (k / sigma) ** 2) for k in range(-radius, radius + 1)]
    total = sum(kernel)
    kernel = [k / total for k in kernel]

    def rows(data, w, h):
        out = array("d")
        for y in range(h):
            row = data[y * w:(y + 1) * w]
            padded = [row[0]] * radius + list(row) + [row[-1]] * radius
            out.extend([sum(kv * padded[x + k] for k, kv in enumerate(kernel)) for x in range(w)])
        return out
    d = rows(p.data, p.w, p.h)
    d = bt.transpose(d, p.w, p.h)
    d = rows(d, p.h, p.w)
    return p.like(bt.transpose(d, p.h, p.w))


def warp(donor_rgb, p, box, centre, scale):
    """Donor colour (prefiltered for its upscale) at every concept pixel of box, as float planes."""
    pre = [prefilter(c, 0.45 * scale) for c in donor_rgb]
    x0, y0, x1, y1 = box
    m00, m01, m10, m11, tx, ty = p
    out = [bt.Plane(x0, y0, x1 - x0, y1 - y0) for _ in range(3)]
    for y in range(y0, y1):
        dy = y - centre[1]
        for x in range(x0, x1):
            dx = x - centre[0]
            u = m00 * dx + m01 * dy + tx
            v = m10 * dx + m11 * dy + ty
            i = (y - y0) * (x1 - x0) + (x - x0)
            for c in range(3):
                out[c].data[i] = bt.bilinear(pre[c], u, v)
    return out


def transfer(base, width, donor_path, box, sil_dist, full, declared, exclude, sigmas):
    """Register the plate donor over box (around the edit, sil_dist >= exclude) and blend it into a copy of base (RGB
    bytes) inside sil_dist < declared, sil_dist the signed distance (negative inside) to her silhouette."""
    dw, dh, dc, dpix = bt.read_png(donor_path)
    if (dw, dh) != (1024, 1536):
        raise ValueError(f"{donor_path} must be 1024 x 1536.")
    donor_rgb = bt.planes_from(dpix, dw, dc, (0, 0, dw, dh), 3)
    concept_luma = bt.luminance(bt.planes_from(base, width, 3, box))

    def in_field(x, y):
        return sil_dist.x0 <= x < sil_dist.x0 + sil_dist.w and sil_dist.y0 <= y < sil_dist.y0 + sil_dist.h

    p, info = bt.register(concept_luma, bt.luminance(donor_rgb), box,
                          lambda x, y: not in_field(x, y) or sil_dist.at(x, y) >= exclude)
    scale = dw / (box[2] - box[0])
    warped = warp(donor_rgb, p, box, info["c"], scale)
    source = bt.planes_from(base, width, 3, box)
    valid = bt.Plane(box[0], box[1], box[2] - box[0], box[3] - box[1])
    for y in range(box[1], box[3]):
        for x in range(box[0], box[2]):
            if not in_field(x, y) or sil_dist.at(x, y) >= declared:
                valid.data[(y - box[1]) * valid.w + (x - box[0])] = 1.0
    matched = bt.match_low_frequency(warped, source, valid, sigmas)
    out = bytearray(base)
    declared_mask = bytearray(len(base) // 3)
    weights = array("d", bytes(8 * (len(base) // 3)))
    for y in range(sil_dist.y0, sil_dist.y0 + sil_dist.h):
        for x in range(sil_dist.x0, sil_dist.x0 + sil_dist.w):
            d = sil_dist.at(x, y)
            if d >= declared:
                continue
            if not (box[0] <= x < box[2] and box[1] <= y < box[3]):
                raise ValueError("The declared region leaves its donor's target box.")
            w = bt.ramp(d, full, declared)
            i = y * width + x
            k = (y - box[1]) * (box[2] - box[0]) + (x - box[0])
            declared_mask[i] = 1
            weights[i] = w
            for c in range(3):
                v = base[i * 3 + c] * (1 - w) + matched[c].data[k] * w
                out[i * 3 + c] = min(255, max(0, int(round(v))))
    info["donor"] = bt.rel(donor_path)
    info["target_box_xyxy"] = list(box)
    return out, declared_mask, weights, info


def cut_hag(spec, concept, width, size, sigmas):
    """Register the hag extraction to the concept frame and match its colour to the concept on her visible pixels.
    Returns her RGBA bytes over the full frame and the registration."""
    donor_path = ROOT / spec["donor"]
    dw, dh, dc, dpix = bt.read_png(donor_path)
    if (dw, dh, dc) != (1024, 1536, 4):
        raise ValueError(f"{donor_path} must be a 1024 x 1536 RGBA PNG.")
    box = tuple(spec["target_box_xyxy"])
    x0, y0, x1, y1 = box
    bw, bh = x1 - x0, y1 - y0
    scale = dw / bw
    lo, hi = spec["alpha_levels"]
    r, g, b, a = bt.planes_from(dpix, dw, dc, (0, 0, dw, dh))
    alpha = a.like(array("d", [min(max((v - lo) / (hi - lo), 0.0), 1.0) for v in a.data]))
    premult = [c.like(array("d", [v * q for v, q in zip(c.data, alpha.data)])) for c in (r, g, b)]

    # registration on her interior, located through the upscale's own mapping
    p0, centre = bt.nominal_affine(box, dw)
    nominal = bt.Plane(x0, y0, bw, bh)
    for y in range(y0, y1):
        for x in range(x0, x1):
            u = p0[0] * (x - centre[0]) + p0[4]
            v = p0[3] * (y - centre[1]) + p0[5]
            nominal.data[(y - y0) * bw + (x - x0)] = 1.0 if bt.bilinear(alpha, u, v) >= 0.5 else 0.0
    inset = spec["registration_inset_px"]
    nominal_sd = bt.mask_distance(nominal, box, inset + 2)
    concept_luma = bt.luminance(bt.planes_from(concept, width, 3, box))
    p, info = bt.register(concept_luma, bt.luminance([r, g, b]), box, lambda x, y: nominal_sd.at(x, y) <= -inset,
                          spec["registration_search_px"])
    info["donor"] = bt.rel(donor_path)
    info["target_box_xyxy"] = list(box)

    # warp premultiplied colour and alpha (prefiltered for the upscale) into the concept frame
    pre = [prefilter(c, 0.45 * scale) for c in premult + [alpha]]
    m00, m01, m10, m11, tx, ty = p
    warped = [bt.Plane(x0, y0, bw, bh) for _ in range(4)]
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
                warped[c].data[k] = bt.bilinear(pre[c], u, v)
    A = warped[3]
    for k in range(bw * bh):
        A.data[k] = min(max(A.data[k], 0.0), 1.0)

    # colour: unpremultiplied where she is present, spread outward a few px for bilinear filtering at her edge
    fill_sigma = spec["edge_fill_sigma_px"]
    spread = [bt.blur(c, fill_sigma) for c in warped]
    colour = [bt.Plane(x0, y0, bw, bh) for _ in range(3)]
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
    sd = bt.mask_distance(silhouette, box, vis_inset + 2)
    source = bt.planes_from(concept, width, 3, box)
    valid = A.like(array("d", [1.0 if d <= -vis_inset else 0.0 for d in sd.data]))
    first = bt.match_low_frequency(colour, source, valid, sigmas[:1])
    residual = [bt.blur(f.like(array("d", [p_ - s_ for p_, s_ in zip(f.data, s.data)])), 1.5)
                for f, s in zip(first, source)]
    limit = spec["occlusion_difference"]
    visible = valid.like(array("d", [1.0 if v > 0 and max(abs(rc.data[k]) for rc in residual) < limit else 0.0
                                     for k, v in enumerate(valid.data)]))
    matched = bt.match_low_frequency(colour, source, visible, sigmas)
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


def build(out_dir, write_assets, donor_hag=None, donor_background=None):
    spec = json.loads(SPEC.read_text(encoding="utf-8"))
    if donor_hag:
        spec["hag"]["donor"] = bt.rel(Path.cwd() / donor_hag)
    if donor_background:
        spec["background"]["donor"] = bt.rel(Path.cwd() / donor_background)
    width, height = spec["size"]
    size = (width, height)
    n = width * height
    concept_path = ROOT / spec["concept"]
    concept_sha = bt.sha256(concept_path)
    cw, chh, cc, concept = bt.read_png(concept_path)
    if (cw, chh, cc) != (width, height, 3):
        raise ValueError("The concept must be a 941 x 1672 RGB PNG.")
    sigmas = spec["registration"]["low_frequency_sigmas"]
    report = {"concept": spec["concept"], "concept_sha256": concept_sha}

    # 1. hag layer: the registered, colour-matched extraction
    bg = spec["background"]
    hag = spec["hag"]
    print("Registering the hag donor...", flush=True)
    hag_rgba, info = cut_hag(hag, concept, width, size, sigmas)
    report["hag"] = {"registration": info}
    alpha_q = array("d", [hag_rgba[i * 4 + 3] / 255.0 for i in range(n)])

    # her silhouette: every pixel at or above the silhouette alpha
    threshold = int(math.ceil(hag["silhouette_alpha"] * 255))
    hx0, hy0, hx1, hy1 = hag["target_box_xyxy"]
    silhouette = bt.Plane(hx0, hy0, hx1 - hx0, hy1 - hy0)
    xs, ys = [], []
    for y in range(hy0, hy1):
        for x in range(hx0, hx1):
            if hag_rgba[(y * width + x) * 4 + 3] >= threshold:
                silhouette.data[(y - hy0) * silhouette.w + (x - hx0)] = 1.0
                xs.append(x)
                ys.append(y)
    report["hag"]["silhouette_box_xyxy"] = [min(xs), min(ys), max(xs) + 1, max(ys) + 1]
    reach = max(bg["declared_px"], bg["registration_exclude_px"]) + 4
    sil_box = (max(0, min(xs) - reach), max(0, min(ys) - reach), min(width, max(xs) + 1 + reach),
               min(height, max(ys) + 1 + reach))
    hag_sd = bt.mask_distance(silhouette, sil_box, reach)

    # 2. clean plate: the hag removed within declared_px of her silhouette
    print("Registering the background donor...", flush=True)
    plate, plate_declared, plate_weight, info = transfer(
        concept, width, ROOT / bg["donor"], tuple(bg["target_box_xyxy"]), hag_sd, bg["full_weight_px"],
        bg["declared_px"], bg["registration_exclude_px"], bg["low_frequency_sigmas"])
    report["background"] = {"registration": info}

    # ---- write
    layers = out_dir / "layers"
    files = {"background": layers / bg["output"], "hag": layers / hag["output"]}
    bt.write_png(files["background"], width, height, 3, plate)
    bt.write_png(files["hag"], width, height, 4, hag_rgba)

    # ---- verify from the written files
    print("Verifying...", flush=True)
    _, _, _, p2 = bt.read_png(files["background"])
    _, _, _, h2 = bt.read_png(files["hag"])
    ver = {
        "concept_unchanged": bt.sha256(concept_path) == concept_sha,
        "background_outside_declared_identical_to_concept": all(
            p2[i * 3:i * 3 + 3] == concept[i * 3:i * 3 + 3] for i in range(n) if not plate_declared[i]),
        "background_declared_pixels": sum(plate_declared),
        "background_full_weight_pixels": sum(1 for i in range(n) if plate_declared[i] and plate_weight[i] == 1.0),
        "background_changed_pixels": sum(1 for i in range(n) if p2[i * 3:i * 3 + 3] != concept[i * 3:i * 3 + 3]),
    }
    # the hag over the plate against the concept
    err_sil = err_opaque = err_soft = 0.0
    cnt_sil = cnt_opaque = cnt_soft = 0
    max_err = 0
    for i in range(n):
        a8 = h2[i * 4 + 3]
        if a8 == 0:
            continue
        a = a8 / 255.0
        e = 0.0
        for c in range(3):
            v = h2[i * 4 + c] * a + p2[i * 3 + c] * (1 - a)
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

    # motion: her seat (bottom centre of her alpha >= 0.5 silhouette) and the largest displacement of a silhouette
    # pixel under each review motion; the plate must be at full weight within motion_margin_px of her silhouette
    sil = [(i % width, i // width) for i in range(n) if h2[i * 4 + 3] >= 128]
    seat = ((min(p[0] for p in sil) + max(p[0] for p in sil) + 1) / 2.0, max(p[1] for p in sil) + 1.0)
    report["hag"]["seat"] = list(seat)
    displacement = {}
    for name, (ox, oy, s) in REVIEW_MOTIONS.items():
        displacement[name] = round(max(math.hypot(ox + (s - 1) * (x + 0.5 - seat[0]),
                                                  oy + (s - 1) * (y + 0.5 - seat[1])) for x, y in sil), 2)
    margin = hag["motion_margin_px"]
    covered = all(plate_weight[y * width + x] == 1.0
                  for y in range(hag_sd.y0, hag_sd.y0 + hag_sd.h)
                  for x in range(hag_sd.x0, hag_sd.x0 + hag_sd.w) if hag_sd.at(x, y) < margin)
    ver["hag"] = {
        "silhouette_pixels_alpha_ge_half": cnt_sil, "opaque_pixels": cnt_opaque, "partial_pixels": cnt_soft,
        "composite_mae_inside_silhouette": round(err_sil / max(cnt_sil, 1), 4),
        "composite_mae_opaque": round(err_opaque / max(cnt_opaque, 1), 4),
        "composite_mae_partial_alpha": round(err_soft / max(cnt_soft, 1), 4),
        "composite_max_abs_error_inside_silhouette": round(max_err, 2),
        "motion_largest_displacement_px": displacement,
        "motion_margin_px": margin,
        "motion_margin_covers_review_motions": all(d <= margin for d in displacement.values()),
        "plate_full_weight_within_motion_margin": covered,
        "note": "0..255 per channel; the hag composited over SunkenLarderBackground compared with the concept"}
    report["verification"] = ver

    # ---- review images
    print("Writing review images...", flush=True)
    review = out_dir / "review"
    rbox = tuple(spec["review_box_xyxy"])
    rw, rh = rbox[2] - rbox[0], rbox[3] - rbox[1]
    bt.write_png(review / "plate.png", width, height, 3, p2)
    bt.write_png(review / "plate-hag-region-2x.png", rw * 2, rh * 2, 3,
                 bt.upscale(bt.crop(p2, width, 3, rbox), rw, rh, 3, 2))
    concept_crop = bt.crop(concept, width, 3, rbox)
    colour = bytearray(n * 3)
    for i in range(n):
        colour[i * 3:i * 3 + 3] = h2[i * 4:i * 4 + 3]
    comp = bt.over(alpha_q, colour, p2, width, rbox)
    bt.write_png(review / "hag-over-plate-2x.png", rw * 2, rh * 2, 3, bt.upscale(comp, rw, rh, 3, 2))
    bt.write_png(review / "concept-vs-hag-over-plate.png", rw * 2, rh, 3, bt.side_by_side(concept_crop, comp, rw, rh))
    grey = bt.over(alpha_q, colour, None, width, rbox, bg=lambda x, y: (128, 128, 128))
    bt.write_png(review / "hag-on-grey-2x.png", rw * 2, rh * 2, 3, bt.upscale(grey, rw, rh, 3, 2))
    checker = bt.over(alpha_q, colour, None, width, rbox,
                      bg=lambda x, y: (200, 200, 200) if ((x // 6 + y // 6) % 2) else (90, 90, 90))
    bt.write_png(review / "hag-on-checker-2x.png", rw * 2, rh * 2, 3, bt.upscale(checker, rw, rh, 3, 2))
    matte = bytearray(min(255, int(round(alpha_q[y * width + x] * 255))) for y in range(rbox[1], rbox[3])
                      for x in range(rbox[0], rbox[2]))
    bt.write_png(review / "hag-matte-2x.png", rw * 2, rh * 2, 1, bt.upscale(matte, rw, rh, 1, 2))
    comp_err = bytearray()
    for y in range(rbox[1], rbox[3]):
        for x in range(rbox[0], rbox[2]):
            i = y * width + x
            d = max(abs(comp[((y - rbox[1]) * rw + (x - rbox[0])) * 3 + c] - concept[i * 3 + c]) for c in range(3))
            v = min(255, d * 4)
            comp_err += bytes((v, v * 3 // 5, 0))
    bt.write_png(review / "hag-over-plate-error-x4-2x.png", rw * 2, rh * 2, 3, bt.upscale(comp_err, rw, rh, 3, 2))
    bt.write_png(review / "diff-background-vs-concept-x4-2x.png", rw * 2, rh * 2, 3,
                 bt.upscale(bt.difference_image(p2, concept, plate_declared, width, height, rbox), rw, rh, 3, 2))
    for name, (ox, oy, s) in REVIEW_MOTIONS.items():
        moved_comp = bt.moved(h2, p2, width, rbox, lambda x, y: (seat[0] + (x - ox - seat[0]) / s,
                                                                 seat[1] + (y - oy - seat[1]) / s))
        bt.write_png(review / f"motion-{name}-2x.png", rw * 2, rh * 2, 3, bt.upscale(moved_comp, rw, rh, 3, 2))

    report["files"] = {k: bt.rel(v) for k, v in files.items()}
    report["review"] = bt.rel(review)
    if write_assets:
        template = ART / "SunkenLarderOriginal.png.meta"
        used = bt.used_guids()
        installed = {}
        for key, name in (("background", bg["output"]), ("hag", hag["output"])):
            target = ART / name
            target.write_bytes(files[key].read_bytes())
            installed[name] = {"sha256": bt.sha256(target), "meta_written": bt.write_meta(target, template, used)}
        report["installed"] = installed
    (out_dir / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="ascii")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", help="unused directory under Docs/work for the layers, review images and report")
    parser.add_argument("--write-assets", action="store_true", help="also replace the two installed textures")
    parser.add_argument("--donor-hag", help="a candidate extraction to use in place of the preserved hag donor")
    parser.add_argument("--donor-background", help="a candidate removal to use in place of the preserved plate donor")
    args = parser.parse_args()
    if args.write_assets and (args.donor_hag or args.donor_background):
        raise ValueError("--write-assets installs only from the preserved donors.")
    if args.output:
        out_dir = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in out_dir.parents:
            raise ValueError("--output must be under Docs/work.")
        out_dir.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        out_dir = Path(tempfile.mkdtemp(prefix="sunken-larder-layers-", dir=WORK))
    report = build(out_dir, args.write_assets, args.donor_hag, args.donor_background)
    print(json.dumps(report["verification"], indent=2))
    print(f"Wrote {out_dir}")


if __name__ == "__main__":
    main()
