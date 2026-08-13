# 14 — What survives of the config schema now that the glyph carries no text?

Type: grilling
Status: open
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
