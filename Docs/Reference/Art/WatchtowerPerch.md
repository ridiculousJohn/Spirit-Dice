# Watchtower perch

The owl at the Watchtower table stands on a wooden T-perch painted into the room. The perch is a masked transfer of a generated donor into the accepted WatchtowerRoomUpperArch.png (941 x 1672, RGB), by the merchant's method ("Generated edits" in [HauntedMerchantHostLayers.md](HauntedMerchantHostLayers.md)); the owl's textures (OwlRest.png, OwlLanding.png, OwlFlight.png) are the accepted originals. Paths are relative to `Dice-Unity/Assets/Dice/Art/Watchtower` unless stated. Numbers, registrations, hashes and checks are in [WatchtowerPerch.json](WatchtowerPerch.json); the prompt is under "Watchtower perch" in [prompts/README.md](prompts/README.md).

The declared region (weight > 0) is WatchtowerPerchWeight.png (Docs/Reference/Art): the perch and its contact shadow. Every pixel outside it equals the accepted painting byte for byte; the weight map holds the transfer weight, 255 full, 0 untouched.

## Geometry

Screen pixels are the 540 x 960 portrait with a top-left origin; the painting is drawn at one scale (941 / 540 horizontally, 1672 / 960 vertically). The owl layers sit at their settled placement (WatchtowerHostPortrait.prefab: position (270, 587.52) from the Portrait Layers' bottom-left; rest 214.49 x 321.73 with pivot (0.5, 0.075), landing 472.38 x 314.92 with pivot (0.51, 0.055)), so both pivots, the feet contact the host authors, are at screen (270, 372.48).

- Crossbar: centre on the pivot line, screen y 372.48 (painting 648.7); diameter 14 screen px (24.4 painting px), screen y 365.5 to 379.5; screen x 205 to 335 (painting 357.2 to 583.8), a little wider than the owl's stance (x 217 to 322).
- Post: from the crossbar's centre, screen x 270 (painting 470.5), 24 painting px wide, straight down to a round base disc on the floor ring's stone rim, base contact painting y 965 (screen 554.1), disc radii 52 x 11 and 12 thick, in front of the parapet. At 540 the TO CLEAR panel and the HANDS / REROLLS row cover the post except the gap between the two row panels; the base shows between the row (bottom y 529) and the dice (tops y 562).
- The owl's feet are the original drawing, not shaped to the bar: the rest toes lie at screen y 357 to 375 with talon tips down to 392, x 217 to 322; the settled landing toes at 337 to 357, talons to 380.

## Method

The donor is a generated edit (imagegen) of a Lanczos upscale of a box of the painting, registered to the image by an affine fit (normalized cross-correlation of high-passed luminance, sigma 6 image px, on a 3 px grid away from the edit), its low-frequency colour matched to the image on the unchanged pixels around the edit (masked Gaussian averages at 24, 10 and 4 px, starting from the mean difference), and blended in with full weight near the edited shape and zero at the declared edge.

The edit target is the box (310, 560)-(630, 1040) at 3.2x with the perch drawn as a flat brown placeholder (WatchtowerPerchTarget.png; without it WatchtowerPerchTargetPlain.png); the donor repaints it as weathered wood lit by the moon from behind right and faintly by the lantern, with a contact shadow. The edited shape is the placeholder, a shadow ellipse round the base (radii 70 x 19) and every pixel within 10 px of the placeholder whose registered donor luminance differs from the painting by more than 14 levels; full weight to 1.5 px outside it, zero at 5 px.

The working scripts (Python with numpy, Pillow and scipy; `Docs/work/merchant-table-clear/.venv`) and every candidate are in `Docs/work/owl-perch` (git-ignored): `perch_geometry.py`, `owl_frames.py`, `perchlib.py` (a numpy port of the Bound Tree registration and transfer), The working scripts (Python with numpy, Pillow and scipy; `Docs/work/merchant-table-clear/.venv`) and every candidate are in `Docs/work/owl-perch` (git-ignored): `perch_geometry.py`, `owl_frames.py`, `perchlib.py` (a numpy port of the Bound Tree registration and transfer), `make_paint_target.py`, `build_paint.py`, `verify_installed.py` and the review renders.
