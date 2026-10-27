# Code standards

These are defaults, not absolutes. Each one says what to prefer, why, and what justifies doing otherwise. Taking an exception is allowed; taking one silently is not. When code departs from a default, a short comment at that spot says why, and anything debatable is raised with John before it is written.

The architecture boundaries in [the handoff](PROJECT-HANDOFF.md) ("Architecture and coding boundaries") still apply; this list is how code inside those boundaries is written.

## Structure and ownership

- **A thing owns its own behaviour.** Prefer telling a component what happened ("stage started", "hand scored") over reaching into its fields to set labels, panels and sizes from outside. Why: callers that drive another object's internals duplicate its knowledge and break when it changes. Exception: plain data holders with no behaviour of their own.
- **Shared code is generic.** Shared encounter, scoring and presentation code does not branch on a particular encounter, host or rule. What is unique to one encounter lives with that encounter's content or in a class that encounter's content references. Why: adding an encounter must not mean editing shared code.
- **Rules bring their own presentation.** A rule's visuals, audio, timing and host reaction are supplied by that rule's presentation assets and presenters, not written into the shared playback. Why: the same as above; the next rule should plug in without touching playback.
- **Presentation does not recompute mechanics.** If presentation needs a relationship between events (which hand a disturbance precedes, which charm granted a resource), the engine states it on the event. Prefer that over searching the event list. Exception: purely visual ordering the engine has no reason to know.
- **One responsibility per class.** Do not let a controller or view collect unrelated jobs; do not extract classes without a useful boundary either.

## Behaviour that varies by kind

- **Prefer polymorphism.** When behaviour differs by kind (rule, condition, effect, interaction, event presentation), each kind carries its behaviour behind an interface or abstract base, and callers dispatch through it. Why: a new kind is a new class, not an edit to every switch that lists kinds.
- **A switch is fine when** the set of cases is closed and local and adding a case does not ripple elsewhere: mapping a fixed enum to a value inside the class that owns that enum, parsing, or a small exhaustive state machine. It is not fine when adding a kind would require editing it.
- **No duplicated branches.** Two near-identical methods, or a branch that repeats another with one condition flipped, are merged.

## References and identity

- **Prefer direct asset references between content.** Why: they cannot silently break, survive renames and show up in the editor.
- **Ids are right when** the value must live outside the asset graph (saves, analytics, network), or a reference deliberately crosses a loading boundary. Saves store ids and resolve them through loaded content.
- **Never duplicate an identity by name.** If two things must agree (a template and the condition it describes), one references the other; they do not each carry a copy of the same name.

## Content, text and values

- **Player-facing text comes from content or the skin**, not string literals in code. Why: wording is design, and it varies by skin and language. Exception: developer diagnostics and log messages.
- **Tunable values are authored or named.** Durations, strengths and thresholds that someone might tune live on an asset or as a named constant with its meaning, not as bare numbers inline. A value repeated in several places is defined once.
- **Content drives loading.** What is loaded follows from what the game content references. Presentation does not decide what mechanical content exists.

## Dependencies, statics and failure

- **Prefer explicit dependencies.** Pass what a class needs, or reference it, rather than finding it through global state. Statics are fine for pure functions, constants and stateless helpers. A static that holds state or hides a lookup needs a stated reason.
- **Fail loudly on missing or invalid content**, at load or author time, with a message naming what is wrong. A fallback is fine when it is designed behaviour (a defeat sound that falls back to the hit sound by design); it is not fine when it hides content that should exist.

## Changing code

- **Do the whole change.** When a generic mechanism is added, the specific code it covers moves onto it in the same change. Old and new routes do not live side by side. The smallest change that makes the new case work is not the goal if it leaves the old pattern in place.
- **Remove what a change leaves unused**: helpers, fields, usings, assets, docs that describe the old way.
- **Comments say why, not history.** No "now", "previously", "no longer", "removed X".

## Tests

- A unit test feeds fabricated true and false input to the code under test and checks the result. Each mechanism is tested once; no game launch, no loops over authored encounters.
- Shared tests never pin, exclude or count specific encounters. A test of something unique to one encounter lives in that encounter's own suite.
- Content validation belongs in an editor tool or at author or load time, never in a test.
- No layout or text-fit tests; layout is inspected once when it is done. No timing-dependent assertions. No reflection into private code.
- Art-generation checks and captures are [Explicit] tools, never part of a suite run as a whole.
- Never make a test pass by hard-coding or weakening it. A PlayMode test exists only for an end-to-end flow John has approved.

## Commits

- One logical change per commit, with a message that says what changed and why. Unrelated work is never bundled into one commit.
