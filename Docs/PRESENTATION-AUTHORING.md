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

The remaining `Spirit*Authoring` scripts and repair commands are migration leftovers, not the source of truth or the ongoing editing workflow. Removal was proposed but has not been implemented or explicitly authorized. They do not run during gameplay, but Dice > Open Application still invokes generation if Application.unity is missing. Do not use that fallback or run generation/repair commands to apply presentation changes. Inspect a missing asset and report it rather than silently recreating it.

Safe-area fitting, standard Unity layout groups, projected dice hit regions, and animation still adapt at runtime. Their controlling references and parameters are authored. Ordinary binding must preserve Inspector typography and layout settings.

The tray and lighting preview windows sample the selected table's authored encounter scene. Edit the scene's DiceTrayFrame for placement and its rig camera for perspective; normal Inspector Undo applies. The previews use isolated copies and include unsaved changes from an open source scene. Legacy tray placement/camera seed fields do not override authored scenes.
