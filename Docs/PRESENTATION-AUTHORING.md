# Presentation authoring

Start the game from `Dice-Unity/Assets/Scenes/Application.unity` (Dice > Open Application). It owns campaign/run state, startup/loading UI, the input system, and the main camera. `SpiritApplicationPresentation` loads the front end or the selected encounter scene additively through Addressables. Encounter state remains in Application while presentation scenes change.

## Scenes

- `Assets/Scenes/Frontend.unity`: main menu, save selection/recovery, chapters, investigation board, Collection.
- `Assets/Scenes/FoxEncounter.unity`: inn/fox composition, also used by the return story.
- `Assets/Scenes/WatchtowerEncounter.unity`: owl composition.
- `Assets/Scenes/MothEncounter.unity`: cart/moth composition.
- `Assets/Scenes/DiagnosticEncounter.unity`: separate diagnostic UI.

The old SpiritHousePrototype scene is excluded from the build. Application is the entry scene; the front end and encounter scenes are Addressable assets. Inspect encounter scenes directly for visual editing; play through Application for game state and content loading.

## Shared assets

`Assets/Dice/Content/Presentation/UI` contains the screen, popup, row, card, HUD, and button prefabs. `Buttons` contains a base prefab and explicit appearance variants. Change typography, padding, anchors, colors, button decoration, or child hierarchy on the relevant prefab. Scene overrides specialize the shared encounter screen for its background, host, and tray placement. Runtime view code binds serialized references and updates text, state, and animation; it does not construct the visual hierarchy or select styles from object names.

`Assets/Dice/Content/Presentation/Rigs` contains tray rigs, modifier-token prefabs, and authored mesh/material assets. Each tray presentation references its rig prefab. Each host presentation references its portrait and sigil prefabs. Dynamic card/token lists instantiate those explicitly referenced prefabs. Render textures, material instances, and animated mesh state are runtime resources, not replacement hierarchy builders.

An encounter table's Scene reference selects its scene. The application's Frontend Scene reference selects the front end. The table and journey assets still provide content, narration, rules, audio, and lighting. Gameplay mechanics and persistence remain separate from these scene references.

## Adding or changing presentation

Duplicate the nearest scene or prefab in Unity, edit the hierarchy, and assign the view component's serialized references. For a new encounter, assign its authored scene to the table presentation and register the scene in Addressables. Retain the shared HUD prefab where applicable; specialize the host, background, camera, lights, and tray rig through explicit scene references.

Edit saved scenes and prefabs in place through Unity's Inspector or Editor API, including CLI/tool access. Preserve GUIDs, references, and unrelated overrides. Existing assets do not need to be recreated to change their hierarchy, components, layout, or appearance.

The migration generators, their helpers, and repair commands are removed. Dice > Open Application opens the existing Application.unity scene without generating assets. Inspect a missing asset and report it rather than silently recreating it.

Safe-area fitting, standard Unity layout groups, projected dice hit regions, and animation still adapt at runtime. Their controlling references and parameters are authored. Ordinary binding must preserve Inspector typography and layout settings.

The tray and lighting preview windows sample the selected table's authored encounter scene. Edit the scene's DiceTrayFrame for placement and its rig camera for perspective; normal Inspector Undo applies. The previews use isolated copies and include unsaved changes from an open source scene. Legacy tray placement/camera seed fields do not override authored scenes.

## Ambient scene motion

Open `Assets/Scenes/AmbientMotionPreview.unity` and enter Play mode to see the original hanging canopy fabric animate against the original merchant scene. The separate cup instance is disabled because this source contains its original cookware. Both layers share the background's coordinate frame across Game view aspect ratios. This isolated visual prototype is not registered in the campaign, Addressables, or build scenes.

`Assets/Dice/Content/Presentation/Ambient/HangingCup.prefab` is an authored UI prop with a native Unity Animation component. Its four-second `HangingCupSwing.anim` loop rotates the `Swing` child by +/-3 degrees. The root is the suspension point; the transparent `Cup` image has its pivot placed at the cord knot. Move the root to attach the prop, scale it to fit the composition, and edit the clip in the Animation window. The clip does not animate root placement. The prop does not intercept pointer input.

Parent attached props to the background artwork's RectTransform so their anchors follow the artwork rather than the screen edges. The preview cup is a child of Merchant Ambient Plate, anchored 370 units right and 144 units down from the plate's top-left corner. This preserves its attachment when the canvas aspect ratio or scale changes. Instances share the clip asset but have separate animation state. This first clip starts at the same phase on each instance; varied timing or event reactions are future work. Rigid hanging objects can use the same native animation approach. Host dialogue/reaction states remain in the host presentation system, and ambient motion consumes no gameplay random numbers.

### Merchant artwork layers

Read the [merchant requirements and implementation audit](Reference/Art/HauntedMerchantRequirements.md) before editing this composition. The isolated preview uses SceneLightingPresentation and the existing shared lighting state for independent lantern/candle contributions. The candle footprint, flame and receiver position follow its transform. Four merchant portrait parts, a blink overlay and separate candle body/flame are installed. All merchant layers share one breathing deformation field, so the figure breathes as a whole and blinks without any part moving on its own (see `Tools/Presentation/author-merchant-presentation.py`). Play Mode advances the authored idle automatically. Reaction and outcome sequences use the existing host state API. Gameplay placement remains separate work.

The cloth source is `MerchantOriginal.png`, the native 941 x 1672 original concept. `CanopyOriginalOverhang.png` and `CanopyOriginalOverhangMask.png` retain its front and rear hanging fabric. The ambient background is `MerchantAmbientUnlit.png`, with independent lantern reference `MerchantLanternOnly.png`. `MerchantHost.png` and `ProtectiveCandle.png` remain the transparent source images; ownership masks separate four host parts and candle body/flame inside the Merchant and Protective Candle groups under Merchant Ambient Plate. Their authored RectTransforms register them to the shared background frame; either can be hidden independently. See [host and candle layers](Reference/Art/HauntedMerchantHostLayers.md) for provenance, dimensions, transforms and measured differences. The registered roof source remains preserved. The front extraction includes the complete source seam and the rear extraction includes its attachment folds. See [attachment registration](Reference/Art/HauntedMerchantAttachmentRepair.md). Do not use the masked `MerchantOriginalRoofPlate.png`. The `Fixed Canopy` gameobject is disabled because its complementary mask redraws remnants. The animated `CanopyArtwork` and separated merchant/candle pieces use `SourceMaskedImage.shader`. Its mask uses bilinear alpha coverage without a hard threshold; coverage affects alpha only so the cut edge stays smooth without darkening. Hiding `Canopy` must expose the clean background with no fragments. The 96 by 192 deformation mesh and two native animation channels remain unchanged.

`CanopyFlutter.anim` drives two `SpiritPortraitDeformationChannel` components over a 31-second native Animation loop with quiet stretches, quick twitches and gusts. Spatial shape weights vary across the fringe. Edit `CanopyArtwork/LeftFlutter` and `CanopyArtwork/RightFlutter` scalar curves in the Animation window. The adapter forwards each scalar to one mesh weight, supporting arbitrary channel counts without an additional update loop or gameplay random numbers.

The Canopy root and its single image retain the same 540 by 960 scene frame and full UV rectangle as the background. The cup and cloth root are sibling prefabs under Merchant Ambient Plate, preserving aspect-ratio alignment and keeping cloth deformation separate from the cup's attachment. Prior artwork and mask experiments remain on disk but are not referenced by this preview.

Prepare background pixels behind every moving object, including the area exposed at its largest displacement. Do not leave a painted copy of the object in the background. Preserve registration, lighting direction, edge transparency and foreground occlusion when separating layers.

| Part | Layer and motion plan |
| --- | --- |
| Cart, road, distant marsh | Static clean plate; occluding cart edges can be separate foreground pieces where needed. |
| Hanging cups and cookware | Transparent rigid props with suspension pivots and individual clips. The prototype validates this workflow with one cup. |
| Torn canopy | Original roof and underside in a static background layer; original hanging pixels in a complementary layer with fixed attachments and native deformation curves. |
| Moss and sagging branches | Separate selected foreground pieces, with fixed branch attachment points and gentle deformation. |
| Mist and water | Separate translucent atmosphere and surface layers; author motion within masks so it stays in the marsh. |
| Lantern and candle | Separate flame/glow layers paired with the existing scene-light flicker; a swinging fixture needs its own pivot. |
| Merchant | Separate host artwork using the existing host idle/reaction presentation. |

The loose cloth is animated in this prototype; the reusable cup animation is retained separately. Gameplay haunting reactions and the movable protective candle remain separate systems to implement after the encounter composition is approved. Source notes are recorded in [hanging cup artwork](Reference/Art/HauntedMerchantAmbient.md) and [registered canopy layers](Reference/Art/HauntedMerchantRegisteredCanopy.md).
