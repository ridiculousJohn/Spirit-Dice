# Watchtower perch

The owl at the Watchtower table stands on a wooden T-perch painted into the room, its feet gripping the perch's round crossbar. Three installed textures carry the change, each a masked transfer of a generated donor into the accepted image, by the merchant's method ("Generated edits" in [HauntedMerchantHostLayers.md](HauntedMerchantHostLayers.md)); OwlFlight.png is unchanged. Paths are relative to `Dice-Unity/Assets/Dice/Art/Watchtower` unless stated. Numbers, registrations, hashes and checks are in [WatchtowerPerch.json](WatchtowerPerch.json); prompts are under "Watchtower perch" in [prompts/README.md](prompts/README.md).

| Texture | Change | Declared region (weight > 0) |
| --- | --- | --- |
| WatchtowerRoomUpperArch.png (941 x 1672, RGB) | the perch and its contact shadow | WatchtowerPerchWeight.png |
| OwlRest.png (1024 x 1536, RGBA) | the feet redrawn gripping the crossbar | WatchtowerOwlRestFeetWeight.png |
| OwlLanding.png (1536 x 1024, RGBA) | the feet and lower legs redrawn gripping the crossbar where the rest feet do | WatchtowerOwlLandingFeetWeight.png |

Every pixel outside a declared region, alpha included, equals the accepted image byte for byte; the weight maps (Docs/Reference/Art) hold the transfer weight, 255 full, 0 untouched.

## Geometry

Screen pixels are the 540 x 960 portrait with a top-left origin; the painting is drawn at one scale (941 / 540 horizontally, 1672 / 960 vertically). The owl layers sit at their settled placement (WatchtowerHostPortrait.prefab: position (270, 587.52) from the Portrait Layers' bottom-left; rest 214.49 x 321.73 with pivot (0.5, 0.075), landing 472.38 x 314.92 with pivot (0.51, 0.055)), so both pivots, the feet contact the host authors, are at screen (270, 372.48).

- Crossbar: centre on the pivot line, screen y 372.48 (painting 648.7); diameter 14 screen px (24.4 painting px), screen y 365.5 to 379.5; screen x 205 to 335 (painting 357.2 to 583.8), a little wider than the redrawn stance (about 222 to 318).
- Post: from the crossbar's centre, screen x 270 (painting 470.5), 24 painting px wide, straight down to a round base disc on the floor ring's stone rim, base contact painting y 965 (screen 554.1), disc radii 52 x 11 and 12 thick, in front of the parapet. At 540 the TO CLEAR panel and the HANDS / REROLLS row cover the post except the gap between the two row panels; the base shows between the row (bottom y 529) and the dice (tops y 562).
- Before the change the rest toes lay at screen y 357 to 375 with talon tips down to 392, x 217 to 322; the settled landing toes at 337 to 357, talons to 380.

## Method

Each donor is a generated edit (imagegen) of a Lanczos upscale of a box of the image, registered to the image by an affine fit (normalized cross-correlation of high-passed luminance, sigma 6 image px, on a 3 px grid away from the edit), its low-frequency colour matched to the image on the unchanged pixels around the edit (masked Gaussian averages at 24, 10 and 4 px, starting from the mean difference), and blended in with full weight near the edited shape and zero at the declared edge.

- Painting: the edit target is the box (310, 560)-(630, 1040) at 3.2x with the perch drawn as a flat brown placeholder (WatchtowerPerchTarget.png; without it WatchtowerPerchTargetPlain.png); the donor repaints it as weathered wood lit by the moon from behind right and faintly by the lantern, with a contact shadow. The edited shape is the placeholder, a shadow ellipse round the base (radii 70 x 19) and every pixel within 10 px of the placeholder whose registered donor luminance differs from the painting by more than 14 levels; full weight to 1.5 px outside it, zero at 5 px.
- Feet, in two generated steps as for the Bound Tree creatures (a scene donor, then its transparent extraction): the scene target is the cutout box over the perch painting resampled into the cutout's frame at its settled placement (rest box (212, 1136)-(812, 1536), landing (510, 664)-(1050, 1024), both at 1536 x 1024); the scene donor redraws the feet gripping the bar; the extraction cuts the owl out of it with the bar, background and shadows removed, so only toe parts in front of the bar remain. The extraction is registered on the owl's interior above the feet (rest rows above 1265, landing above 820) and its premultiplied colour and alpha are transferred. The edited shape is the old and new feet (cream legs and toes, dark talons, parts of at least 400 px, not the blue-white wings) below the feet rows, plus every visible pixel within 8 px of the old feet below the toe row (the old toes' pale rims); full weight to 2 px outside it, zero at 6 px, faded in over rows 1275 to 1295 (rest) and 826 to 842 (landing) across the legs.
- The landing feet line up with the rest feet: the landing scene edit takes as its second image the rest owl with its redrawn feet resampled into the landing target's frame (WatchtowerOwlLandingFeetReference.png), and copies those feet. At the settled placement, with each layer's deformation mesh applied, the two sets of toes coincide within about half a screen pixel.

The working scripts (Python with numpy, Pillow and scipy; `Docs/work/merchant-table-clear/.venv`) and every candidate are in `Docs/work/owl-perch` (git-ignored): `perch_geometry.py`, `owl_frames.py`, `perchlib.py` (a numpy port of the Bound Tree registration and transfer), `make_paint_target.py`, `build_paint.py`, `make_feet_target.py`, `make_rest_reference.py`, `build_feet.py`, `verify_installed.py`, `deformation_check.py`, `meshrender.py` and the review renders.

## Motion

The feet stay planted: the deformation shapes of WatcherPhysicalRestMesh.asset are scaled by the contact field, zero below the pivot line plus 0.025, so in every rest-pose state (idle breathing, head tilt, reaction, victory, defeat, farewell) the toes on the crossbar move at most 0.014 screen px and the lower legs inside the feet region at most 0.56 px (farewell). The arrival's grounded settle moves the toes on the bar at most 0.08 px. The landing layer's Landing Registration (weight 1 throughout) lifts its legs above the toes as authored; its toes on the bar stay within 0.14 px in the arrival.

The rest texture also shows in Waiting, at 6 percent of its size on the far arch with no crossbar under it; its curled toes there are a few pixels.
