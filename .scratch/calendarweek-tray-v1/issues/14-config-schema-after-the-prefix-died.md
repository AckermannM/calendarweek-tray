# 14 — What survives of the config schema now that the glyph carries no text?

Type: grilling
Status: resolved
Blocked by: 06, 07

## Question

Graduated by `07`, which fixed the renderer's input signature as `(int week, int sizePx, Color ink)`
— **no text of any kind**.

`01`/Q5 specified four config keys. `06` then removed the prefix from the glyph entirely: the form
is a calendar page with a bare number, and `KW` appears nowhere. Three of the four keys were written
for a glyph that no longer exists.

| key | `01`'s intent | state after `06`/`07` |
| --- | --- | --- |
| `language` (`de`\|`en`) | prefix **and** menu/tooltip strings | **half dead** — still governs menu and tooltip, no longer the glyph |
| `label` | overrides the prefix outright | **orphaned** — there is no prefix to override |
| `layout` (`single`\|`stacked`) | which of two glyph forms | **orphaned** — both forms lost in `06` |
| `theme` (`auto`\|`light`\|`dark`) | theme override | **intact**, and `07` depends on it |

`13` has since confirmed `theme` keeps exactly these three values and needs **no fourth** for high
contrast: high contrast is detected via `SystemInformation.HighContrast` and **overrides all three**,
including an explicit `light`/`dark`. So this key is settled — the only question left below is
whether `language` and `label` survive.

### The decisions

1. **Delete `layout`.** This was open pending `12`, on the chance a reduced 16 px variant would give
   the key a second form to select between. **`12` decided one form at every size**, so no second
   form exists, none is coming, and the key has nothing left to name. Confirm the deletion rather
   than re-litigate it.
2. **Delete `label`, or repurpose it?** `01`/Q13 leaned on `label` as the escape hatch that
   permanently closes "please add language X". If it dies, that argument dies with it — and the
   out-of-scope entry for further locales is resting on it. Is there anywhere left in the glyph a
   custom string *could* go, or does the escape hatch now have to be something else?
3. **Does `language` still earn its place**, or should the menu and tooltip follow the OS UI
   culture with no key at all?
4. **What is the resulting file?** `01`/Q18 (first-found-wins) and the never-writes rule stand
   regardless; this is only about which keys exist.

The README documents path and defaults, so whatever survives here is also a documentation change.

## Outcome

The final `config.json` schema for v1, precise enough for `08` to encode, plus whatever amendment
the map's **Out of scope** entry on further locales needs if `label` does not survive.

Use `/grilling` and `/domain-modeling` — the glossary in `CONTEXT.md` defines **Label**, **Prefix**
and **Layout**, and at least two of those definitions are now describing something that does not
exist.

## Answer

**Two keys, both optional, both defaulting to `auto`.** `01`'s four became two: `layout` and `label`
are deleted, `language` survives with a new OS-derived default, `theme` was already settled by `13`.

```json
{
  "language": "auto",
  "theme": "auto"
}
```

| key | values | default | governs |
| --- | --- | --- | --- |
| `language` | `auto` \| `de` \| `en` | `auto` | menu items and tooltip only. `auto` = `de` when `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName` is `"de"`, else `en` |
| `theme` | `auto` \| `light` \| `dark` | `auto` | glyph ink; overridden outright by high contrast (`13`) |

Nothing in the file reaches the renderer, whose signature stays `(week, sizePx, ink)` as `07` fixed
it. Both keys are modelled as **enums**, matched **case-insensitively**; an unrecognised key *or*
value routes into `01`/Q19's existing malformed-config path.

### `layout` — deleted, as expected

Confirmed without re-litigation. `06` killed both forms the key named and `12` decided one form at
every size, so there is no second form for it to select between and none is coming.

### `label` — deleted, and the escape-hatch argument did not survive with it

The interesting part of this ticket. `label` had exactly one coherent repurpose left — a tooltip
**word** override (`"KW"` → `KW 32 · 3.–9. August 2026`), since `06` closed all six glyph routes for
`KW` and left nowhere for a custom string to go.

It was rejected on a point that also **invalidates the reason it was originally kept**: `01`/Q13
leaned on `label` as the escape hatch that permanently closes "please add language X", and that
worked *because `label` changed the always-visible glyph*. A key that changes only what you see **on
hover** cannot be an escape hatch for anything. So keeping it would not have preserved Q13's
argument, only made it look preserved. It also composes badly with the `language` key it would now
sit beside: `"label": "Semaine"` yields `Semaine 32 · 3.–9. August 2026`, half-French and strictly
worse than plain English.

**Consequence: v1 ships no locale escape hatch of any kind.** A French user gets English strings and
cannot change them. Accepted — see the re-based out-of-scope entry below.

### `language` — survives, on an argument that inverts the usual minimalism test

The standing preference is "minimal above all else", and the obvious move was to drop the key and
follow `CultureInfo.CurrentUICulture`. It was kept because **dropping it deletes no code**: both
string tables and both date-range formatters ship either way — `CurrentUICulture` does not write
German for you, it only *chooses*. Minimalism's usual payoff is not on offer here, so the trade was
"remove the choice, keep every line that makes the choice possible" against "one key, two values".

What decided it is the case that would break: a German user on an en-US corporate laptop, handed
"Calendar week 32" by a tool whose entire reason to exist is *Kalenderwoche*. That is not an edge
case for this applet, it is the core audience — and since `06` the tooltip is the only place the
word appears anywhere in the product, so the OS's opinion is the wrong authority over it. The OS
still supplies the **default**; the key exists to overrule it.

`language` takes an explicit `"auto"` rather than treating absence as auto, mirroring `theme`. In a
two-key file, one key that can spell its own default and one that cannot is a wart the README then
has to explain, and `"auto"` gives the user something to revert to instead of deleting a line.

### Unknown keys and unknown values are both loud

`UnmappedMemberHandling.Disallow` for keys, enums for values. One enum setting plus two enum types
converts the schema's worst failure mode — `"them": "dark"` or `"theme": "drak"` silently doing
nothing forever — into the diagnostic channel `01`/Q23 already built. It does **not** weaken
`01`/Q19: that rule routes malformed config to defaults-plus-balloon rather than a failure to start,
so a stray key or a typo'd value degrades to "runs with defaults and says so", never to a crash.
Warning on keys while shrugging at values would have been the odd position, since they are the same
mistake.

Two notes for `08`. Use the generic **`JsonStringEnumConverter<T>`**, which is source-generator- and
trim-compatible — `04` established the source generator is mandatory, reflection-based
`System.Text.Json` having been the sole failure of 22 trimming checks. And the forward-compatibility
cost is real and belongs in the spec: a config file written for a future version with a third key
trips `Disallow` on v1. In an applet with no updater and no config-migration story, being told is
better than being ignored.

### `config.json` still earns its place

Asked deliberately, because two keys that both default to `auto` in a file the applet never writes
invites "why have a file at all". Kept for two reasons. Command-line arguments — the only other
channel — are **dead by measurement, not preference**: `09` fixed the `Run` value as a quoted path
with **no arguments**, so anything passed on the command line evaporates at the next login, making
the file the only configuration channel that survives a reboot. And both survivors are the sole exit
from a mis-read the applet cannot detect for itself: `13` proved `SystemUsesLightTheme` is
readable-but-wrong under high contrast and blind to registry-poking theme switchers, and no signal
anywhere distinguishes "German user on en-US Windows" from "English user".

`01`/Q4 and Q18 (resolution order, first-found-wins) and the never-writes rule are untouched.

### Map amendment required

The **Out of scope** entry on further locales currently reads *"the `label` override makes arbitrary
prefixes possible without new locales, which closes this permanently"* — a sentence that becomes
false the moment `label` is deleted. Re-based on a **stronger** argument: `06` made the glyph
locale-free in every language on earth, so the always-visible artifact needs no localisation at all,
and what remains translated is a hover tooltip and a two-item context menu that a user must
deliberately go looking for. Adding a locale buys translated strings for the two things nobody looks
at, at the price of a per-release translation obligation in a language the maintainer cannot review
— the same **recurring-obligation** category `10` refused winget for.

### Documentation and glossary

`README.md`'s *Configuration* section is currently a stub that lists no keys, so nothing there is
false today; `08` fills it, and uninstall step 4's forward-reference to that section stays valid.
`CONTEXT.md` was updated in this session: **Prefix**, **Label** and **Layout** deleted (folded into
**Glyph** as an `_Avoid_` list so the dead terms remain findable), **Glyph** corrected — it still
described `KW01` — **Kalenderwoche** narrowed to the word rather than the abbreviation, and
**Tooltip** added, because it is now the only surface where the applet uses language at all.

No ADR: nothing has shipped and no config file exists on any machine, so this fails the
hard-to-reverse test.
