# Loose canopy cloth artwork

Generated with the built-in image generation tool on 2026-10-20.

The supported roof and broad underside stay in the static scene plate. Only four narrow dangling strips and one larger rear flap are animated, each sampled from its own atlas rectangle and attached to an authored local pivot. The previous whole-canopy artwork is retained but is not used by this preview.

- Static plate: `Dice-Unity/Assets/Dice/Art/HauntedMerchant/MerchantRagPlate.png` (941 x 1671).
- Loose cloth atlas: `Dice-Unity/Assets/Dice/Art/HauntedMerchant/CanopyRags.png` (941 x 1672).
- Reference for extraction and plate: `Dice-Unity/Assets/Dice/Art/HauntedMerchant/MerchantAmbientPlate.png`.
- Atlas source iteration: generated sparse extraction, then edited to separate its rear flap from the neighboring strip.
- Atlas UV crops and scene placement are authored in `Canopy.prefab`. Artwork is not a full-canvas overlay; each crop is positioned independently in the shared 540 x 960 scene frame.

## Loose pieces extraction prompt

Use case: background-extraction. Make a transparent animation layer from this EXACT941x1672 scene, retaining original full canvas size and pixel registration. Extract ONLY a few loose dangling torn cloth pieces along the LOWER ragged edge of the canopy, plus the long hanging rear flap. The solid sloped red-and-cream striped ROOF and the broad dark UNDERSIDE MUST NOT be included at all. Leave them completely transparent. Specifically retain small irregular hanging fringe pieces near these top-left image coordinates: (175,220), (300,255), (355,275), (500,305), and the larger rear flap around (490,365) with its tip at y410. Each small piece is just a lower dangling tip about20-45pixels wide and20-60pixels tall; keep a small fixed attachment edge at its top. The rear flap is about110pixels wide fromx435to545 andy325to410. Preserve texture, subdued night lighting, ragged shape and exact original positions. All other fabric, especially continuous roof panels or underside, must be transparent. All scene/merchant/cart/trees/table must be transparent. Do not add or extend strips, do not enlarge or recenter them. No checkerboard. Genuine alpha. This is ONLY a sparse set of hanging rags, NOT a cutout of the canopy.

## Atlas separation prompt

Use case: precise-object-edit. Edit target is this sparse transparent sheet of five cloth scraps. Preserve all five scraps' exact shapes, textures, lighting and sizes. Keep canvas941x1672 and genuine transparency. Move ONLY the large rectangular rear flap currently atx430..550,y455..600 to a separate empty location centered atx740,y565 (top-left roughly680,495). It must be clearly separated by at least80pixels from every other scrap. Leave the four skinny scraps in their existing positions. Where the moved flap overlapped the rightmost skinny scrap, reconstruct only the skinny scrap's own tapering torn tip. All else remains transparent. No roof, no extra cloth, no backdrop. This is a texture atlas so each of five pieces must fit its own rectangular crop without including any part of another piece.

## Static plate prompt

Use case: precise-object-edit. Create static background for the scene in this941x1672 image. Keep the entire substantial canopy ROOF, all red/cream striped supported fabric and all broad dark UNDERSIDE completely intact at their EXACT current positions and same night lighting. Remove ONLY loose dangling torn scraps at its lower front edge, and remove the hanging rear flap atx435..550,y325..415. Specifically clean away hanging tips aroundx155..205,y195..270; x280..330,y225..300; x350..390,y250..325; x480..550,y265..340. These should leave short ragged attachment stubs at their top edges, with original wood, dark underside, shadows or background visible behind where the loose scraps hung. Do NOT remove or reshape the main roof or underside! Keep the detailed tears/holes higher up in supported fabric. Preserve ALL other image content exactly: merchant, face, clothing, hat, lantern, candle, wares, table, broken wheel, swamp, trees, moonlight, original camera and composition. No new hanging objects. This plate receives separately animated scraps later; roof and underside stay static. Maintain941x1672portrait.
