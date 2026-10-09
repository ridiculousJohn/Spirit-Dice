# Spirit Dice project handoff

Updated October 9, 2026. This is the continuation point after the encounter journey, charm badges, and Addressables shutdown fixes.

## Start here

- Repository: `git@github.com:ridiculousJohn/Spirit-Dice.git`, branch `main`.
- Local root: `C:/Users/jsp73/Projects/Dice`; Unity project: `Dice-Unity`.
- Unity version: `6000.6.2f1`. Editor: `C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe`.
- Main playable scene: `Dice-Unity/Assets/Scenes/SpiritHousePrototype.unity`.
- Immediate pending task: improve the selected charm's description. John specifically dislikes the diagnostic phrase `condition not met` in that description. Card badges are implemented, but the selected-detail panel still uses diagnostic status prose. The prior request prioritized the validation and Addressables fixes; those are complete. The replacement wording or presentation has not been agreed upon.
- The latest work was validated in the Unity editor. No Android rebuild or install was performed for these latest changes; do not assume the phone matches the repository.

## Design decisions to preserve

The mechanical baseline is Dice of Kalma, adapted into encounters with spirits. The original death/crypt setting is not the intended presentation. The first spirit is a fox host at The Last Lantern. The wider game follows an investigation through different spirits and places; future encounters need not be houses. A spirit can return in later encounters with different rules, difficulty, and story context.

Mechanics, content, progression, and presentation must remain separate. Mechanical content does not include names, icons, dialogue, art, audio, or other presentation. The engine must be usable under another skin or inside a larger game. Encounter behavior and journey sequencing must be driven by authored data, with no shared-controller branches for the fox or first encounter ID.

Each modifier has an individual mechanical ScriptableObject and a separate presentation asset. Addressables discovers the labeled assets at startup. Preserve the per-condition implementation classes; do not reintroduce a central condition-evaluation switch or large presentation lookup dictionaries.

Use Newtonsoft JSON for saves and profiles. New ordinary runs use fresh seeds; fixed seeds are an explicit debugging option. Save/resume preserves randomness and the pending reward choices.

The accepted visual direction is a portrait-friendly hybrid: painted room/host artwork with physical 3D rounded bone-like dice, a deep perspective tray, and a smaller 3D modifier history tray. The seated fox should feel large and present at the table. Spirit motion can use a faint spectral silhouette that anticipates the physical movement. Future hosts can be entirely ghostly animals. Charms and Hand Ranks have separate screens.

Scoring presentation is deliberately sequential. Begin with zero base, multiplier, and total. Roll-triggered modifier bonuses accumulate before playing. On Play Hand, count the raw dice into base, then apply the hand category's complete upgraded base and multiplier together, then present each matching modifier, then calculate base times multiplier and apply it to the target. Do not present a hand upgrade as a separate modifier. Show modifier feedback over its action button and slide its icon die into the history tray. Chained or post-total modifier activation must not be inferred from the reference without evidence.

## Implemented state

- The first fox encounter has ten stages, curated reward pools, tier-weighted offers, and a first reroll each hand that does not spend a paid reroll. The ordinary 25-stage encounter remains available to diagnostics/direct initialization.
- Milestones at cleared stages 3, 6, and 10 permanently unlock all four variants of Open Gate, Still Reflection, and Rising Sun respectively. These unlocks survive loss/restart and join eligible reward pools; they are not automatically equipped.
- Those milestone unlocks use fox spirit scope. They can appear in permitted pools for current or future fox encounters, but not another spirit's encounters. Global, spirit, and encounter scopes and encounter-exclusive modifiers are supported.
- Profile persistence stores unlocks, visits, and completion history separately from a run save. Loading an older run does not erase profile unlocks or reroll an already-generated offer.
- The reusable journey covers investigation selection, approach, arrival, bargain, invitation/rule explanation, and acceptance with an animated opening roll. Returning visits can skip to the invitation. Active runs can return to the board and resume. Authored win/loss endings and a victory clue complete the flow.
- The main authored journey assets are under `Dice-Unity/Assets/Dice/Content/Presentation/SpiritHouse`: `SpiritJourneyCatalog.asset`, `LastLanternJourney.asset`, `LastLanternTable.asset`, and `LanternKeeperHost.asset`.
- Charm names distinguish effect variants. Cards wrap their full rules/effects and use corner state badges: check for ready, hourglass for waiting, cross for used, circular arrow for roll evaluation, pause for inactive, and a diamond fallback for kept/uninspected.
- Badges display remaining uses as `1/1`, `0/1`, or infinity. All 296 authored modifier definitions currently use once-per-hand activation. The engine also supports once-per-stage and unlimited activation; it does not currently author arbitrary N-use limits.
- The Clues button now draws behind Charms, Hand Ranks, and Menu. Both modifier loaders tolerate handles invalidated by Addressables shutdown; valid owned handles are released exactly once.
- Rain volume is reduced. Fox movement uses three quiet randomized whooshes, avoiding immediate repeats when alternatives exist. Audio sources and generation records are preserved in the repository.

## Pending and proposed work

- Fix the selected charm description's diagnostic status wording first, after agreeing on how it should read or use the badge.
- A permanent unlocked-modifier collection screen is not implemented. The Charms screen shows owned modifiers for the current run; it is not the player's entire unlocked collection.
- Consumables, their loadout and transaction-safe consumption, physical victory tokens, and authored rematches remain proposals. Do not treat the proposal's reward table as proof those rewards exist.
- Additional spirit encounters are not yet authored as playable content. Synthetic test fixtures exercise reuse but are not finished encounters.
- Targets and tier assignments are provisional. No measured balance pass or completion-rate study has established their difficulty.
- Further visual/audio polish remains open; John accepted the current flow as a usable foundation.

## Validation and working practices

The final focused run passed eight PlayMode tests: five Addressables tests, the three-overlay navigation/layering test, and two owned-charm tests. It covers 443x912 and 540x960 portrait layouts, all 296 charm descriptions, mixed badge states, use counts, and card interaction. The Unity process exited successfully without the previous invalid-operation-handle exception. Earlier badge work passed nine EditMode status tests and three additional gameplay UI PlayMode tests. These are focused results, not a claim that the entire suite was rerun after every change.

Local reports and captures are under `Docs/work/charm-followup-validation/20261009-01` and `Docs/work/charm-status-validation/20261009-01`. These are ignored, reproducible evidence; a fresh clone will not include them. The regression source is committed.

Tests isolate profiles and run saves from normal gameplay data. Close only the Dice-Unity editor before an external Unity test/build process uses this project; other Unity projects may remain open. Run rendered PlayMode tests with graphics, and use `DICE_SPIRIT_CAPTURE_ROOT` for optional captures. Do not run two Unity processes against this project concurrently.

John's global working instructions are saved in `C:/Users/jsp73/.codex/AGENTS.md`. Preserve these critical preferences when continuing elsewhere:

- Propose nontrivial changes before editing unless John has explicitly directed implementation. Keep mechanics and presentation boundaries intact.
- Prefer GPT-6 Astra orchestration and GPT-6.1 Sol with high reasoning for bounded coding/test work. Delegation is authorized within approved work; the orchestrator reviews integration. Do not claim that a preference changes the active model.
- Never reset, restore, revert, clean, delete, or otherwise roll back workspace changes without John's explicit direction for that exact action. This includes newly written agent code, generated output, and test artifacts. All changes are user-owned.
- Do not create worktrees unless requested. Do not rename existing identifiers. Use ASCII comments and string literals. Always spell out checkpoint.
- Ignore disposable test output rather than committing it; leave existing files on disk. Preserve authored assets, provenance, and useful reference material.

## Durable reference material

- [First fox encounter and progression](outputs/dice-first-fox-encounter-proposal.md): current configuration, persistence behavior, tier rules, and clearly marked future proposals.
- [Engine architecture proposal](outputs/dice-engine-architecture-proposal.md): architectural intent; its initial project-status paragraphs are historical, not the current implementation state.
- [Reference game design](outputs/dice-of-kalma-design-reference.html) and [extracted design data](outputs/dice-of-kalma-design-data.json).
- [Modifier implementation](outputs/dice-modifier-implementation.md), [rewards](outputs/dice-reward-implementation.md), and [save/resume](outputs/dice-save-resume-implementation.md): implementation-era notes; consult current code for later changes.
- `outputs/spirit-house-concepts-v1`, `outputs/spirit-house-portrait-v1`, and `outputs/spirit-house-prototype-assets`: saved concepts, prompts, and asset provenance.
- `outputs/audio/fox-whoosh-v1`, `work/spirit-audio/README.md`, and `outputs/last-lantern-journey-art.json`: audio and doorway artwork records.

The implementation commits preceding this handoff are `e2653b2`, `521a812`, `200cb8f`, `0185ed8`, and `53f2026`. Source and binary assets use Git and Git LFS. Original chat uploads in temporary directories are not the durable asset archive; use the committed reference and concept files above.
