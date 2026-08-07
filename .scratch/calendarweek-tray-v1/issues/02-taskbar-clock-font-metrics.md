# 02 — What font does the Windows 11 taskbar clock actually use?

Type: research
Status: resolved

## Question

The brief requires the glyph to use "the already used microsoft font in the time & date part of the taskbar". Establish precisely what that is, so `06` prototypes against the real thing rather than an approximation.

Answer these:

1. **Which named font instance?** `Segoe UI Variable Small`, `Text`, or `Display`? Microsoft's optical-size guidance assigns each to a size band — find the band boundaries and which one covers taskbar clock text. Note that the tray glyph will render at roughly 16–32px, which may land in a *different* band than the clock itself, in which case the honest answer is "the clock uses X, but Y is correct for our size".
2. **Which weight?** Regular, Semilight, or Semibold. The GDI+ family list exposes all three as separate families (`Segoe UI Variable Small Semibold`, etc.).
3. **What point/pixel size**, at 100% scaling, and how it scales with DPI.
4. **How does the shell pick it?** Is it `SystemParametersInfo(SPI_GETNONCLIENTMETRICS)` → `lfCaptionFont`/`lfMessageFont`, a hard-coded XAML resource in the shell, or something else? This matters: if it comes from a system metric, the applet can *read* it at runtime and stay correct when the user changes text scaling — far better than hard-coding a family name.
5. **Does GDI+ render the variable-font named instances faithfully?** `SegUIVar.ttf` is a single variable font; GDI+ predates variable fonts and resolves named instances through the registry. Confirm the rendered output actually differs between `Small` and `Display` rather than silently falling back to one default instance. **This is the highest-value part of this ticket** — if GDI+ collapses them, the whole "match the taskbar font" requirement needs rethinking, and `06` must know before it starts.

## Method

Primary sources first: Microsoft's Segoe UI Variable / Windows 11 typography documentation, and the Win32 font and non-client-metrics API docs.

Then verify empirically on this machine, since that beats any documentation for question 5 — render the same string in each named instance at the same size via GDI+ and compare the bitmaps. Identical output across `Small`/`Text`/`Display` proves collapse.

## Notes

Environment facts already known — do not re-derive:

- `C:\Windows\Fonts\SegUIVar.ttf`, registered as `Segoe UI Variable (TrueType)`.
- GDI+ enumerates 12 `Segoe UI Variable *` families: `Display`, `Small`, `Text`, each × Regular/Light/Semilight/Semibold. Note the family names are **truncated at 31 characters** in the enumeration (`Segoe UI Variable Display Semib`), which is a GDI limitation worth accounting for when constructing a `Font` by name.

Record findings in this file under `## Answer`.

## Answer

Verified on this machine: Windows 11, 2560×1600 primary at **150 % scaling** (144 dpi), taskbar 72 physical px tall (= 48 epx), accessibility `TextScaleFactor` unset (100 %). Empirical work used PowerShell + `System.Drawing`; scripts were throwaway and are not in the repo.

**Headline: GDI+ does *not* collapse the named instances — the "match the taskbar font" requirement survives.** But it *does* freeze the optical-size axis, and the bare family name `"Segoe UI Variable"` silently falls back to Microsoft Sans Serif. Both are design-relevant and are covered under (5).

### Ground truth from the font file itself

Parsed the `fvar` table of `C:\Windows\Fonts\SegUIVar.ttf` directly (empirical, highest confidence — this is the font binary, not documentation):

- Two axes. `wght`: min 300, default 400, max 700. `opsz`: **min 5, default 10.5, max 36**.
- 15 named instances = 3 optical sizes × 5 weights:

| Optical name | `opsz` | Weights present |
|---|---|---|
| Small | **8** | Light 300, Semilight 350, Regular 400, Semibold 600, Bold 700 |
| Text | **10.5** (axis default) | same |
| Display | **36** | same |

The `opsz` axis is denominated in **points**, not pixels. That single fact reconciles Microsoft's type ramp with the axis values (see (1)) and is the key to picking the right instance.

GDI+ exposes those 15 instances as 12 families because it folds `Bold <optical>` into the corresponding Regular family as the `Bold` *style* — 3 optical × {Light, Semilight, Regular(+Bold style), Semibold}. The registry holds only one entry, `Segoe UI Variable (TrueType)=SegUIVar.ttf`; the 12 families are synthesised from the font's `fvar`/`STAT` tables, not registered individually.

---

### 1. Which named font instance?

**The taskbar clock uses `Segoe UI Variable Small`. `Small` is also the correct choice for our 16–32 px tray glyph.** Conveniently, the two answers agree — the "the clock uses X but Y is right for us" caveat the ticket anticipated does not arise.

*Empirically established (high confidence): the size.* Screen-captured the clock region and measured the ink. Digit band of `13:37` spans 13 physical rows; `07/08/2026` spans 15 rows including the slash overshoot; the two lines' digit tops are 24 physical px apart. Rendering candidates through GDI+ and measuring identically, the match lands unambiguously on **18 physical px em** (13/40 and 15/92 rendered vs 13/41 and 15/92 measured). 18 physical px ÷ 1.5 = **12 epx**, and the 24 px line pitch = **16 epx**. That is exactly the WinUI **Caption** ramp entry, 12/16 epx.

*Doc-derived (medium-high confidence): the optical variant.* Microsoft's Windows 11 type ramp assigns an optical variant per ramp entry, and Caption is the only entry assigned `Small`:

| Ramp entry | Optical variant | Size / line height |
|---|---|---|
| Caption | **Small** | 12/16 epx |
| Body | Text | 14/20 epx |
| Body Strong | Text Semibold | 14/20 epx |
| Body Large | Text | 18/24 epx |
| Subtitle | Display Semibold | 20/28 epx |
| Title | Display Semibold | 28/36 epx |
| Title Large | Display Semibold | 40/52 epx |
| Display | Display Semibold | 68/92 epx |

The arithmetic corroborates it. epx → pt is ×0.75, and `opsz` is in points:

- Body 14 epx = **10.5 pt** = `Text`'s `opsz` of **10.5**, exactly. This is not a coincidence and it confirms the points reading of the axis.
- Caption 12 epx = **9 pt**. Distance to `Small` (8) is 1.0; to `Text` (10.5) is 1.5. `Small` wins, matching the documented mapping.

A further corroboration: all 12 families share identical vertical metrics (`EmHeight` 2048, `CellAscent` 2210, `CellDescent` 514, `LineSpacing` 2724). 2724/2048 = 1.330; at 12 epx that is 15.96 ≈ **16 epx** — i.e. Caption's documented 16 epx line height is simply the font's natural line spacing at 12 epx. The measured 24 physical px line pitch is that same number at 150 %.

**Honest gap:** I could *not* confirm the optical variant from the screenshot pixels. A shape-match (bounding-box-aligned mean-absolute-error against the captured `07/08/2026`) ranked `Text` 0.08, `Segoe UI`/`Display`/`Small Semilight` 0.09, `Small` 0.10 — a spread well inside the noise introduced by DirectWrite-vs-GDI+ rasterisation differences. At 12 epx the three optical variants are simply too close to separate from a 92 px-wide screenshot. So the variant rests on Microsoft's published mapping plus the `opsz` arithmetic, not on direct measurement. If `06` finds `Text` looks better in a tray icon, there is no measurement here that contradicts it.

**For our tray glyph.** `GetSystemMetricsForDpi(SM_CXSMICON)` returns 16 / 20 / 24 / 32 px at 96 / 120 / 144 / 192 dpi — i.e. the tray icon is **16 epx** logical at every DPI. Whatever physical em we draw at (roughly 12–18 physical px), the *apparent* size stays around 9–11 pt, which is the `Small`/`Text` boundary region. `Small` (`opsz` 8) is the legibility-optimised end and matches the clock, so it is the right default. This holds at every DPI because the apparent size is DPI-invariant.

### 2. Which weight?

**Regular, `wght` 400** — GDI+ family name `Segoe UI Variable Small` (the Regular instance is the base family, with no weight suffix). Doc-derived: the type ramp lists Caption as Regular, and the typography guidance says "use regular weight for most text, use Semibold for titles". The screenshot is consistent with Regular but, as in (1), 12 epx is too small to discriminate Regular from Semilight by measurement.

Two empirical notes for `06`:

- The weight axis renders faithfully. Ink coverage of glyph `a` at 200 em, within the `Small` optical size: Light 3381 → Semilight 3992 → Regular 4542 → Semibold 5510 → Bold 6545. Monotonic, and the outlines differ (max deviation 11–12 units per 1000 em between adjacent weights, 43.8 between Light and Semibold).
- **`FontStyle.Bold` on the Regular family gives the genuine `wght` 700 instance, not synthetic emboldening** — that is the 6545 figure above, and it sits correctly on the weight ramp. Applying `FontStyle.Bold` to `Segoe UI Variable Small Semibol` is a silent no-op (identical outline and ink to its Regular, 5510) because that family has no bolder partner. Applying it to `…Small Light` *does* produce synthetic smear (ink 5309, and the bounding height changes from 107.42 to 108.59 — the tell-tale of GDI faux bold). `FontFamily.IsStyleAvailable` is not a reliable guide here: it reports `Italic` available on all three optical families even though the font contains no italic at all.

### 3. Point / pixel size and DPI scaling

- **12 epx = 9 pt at 100 % scaling**, 16 epx line height.
- Physical pixels = `12 × dpi / 96`: **12 px @ 96 dpi, 15 px @ 120 dpi, 18 px @ 144 dpi, 24 px @ 192 dpi.** The 18 px figure at 144 dpi was measured directly off the screen, so the scaling law is empirically anchored at one point and linear by construction.
- Accessibility text scaling (`HKCU\Software\Microsoft\Accessibility\TextScaleFactor`, 100–225) multiplies on top of DPI. Unset on this machine, so its effect on the taskbar clock was **not** verified.

### 4. How does the shell pick it?

**Not through `SystemParametersInfo`. The applet cannot read the taskbar font from a system metric.** This is an empirically settled negative.

Called `SystemParametersInfoW(SPI_GETNONCLIENTMETRICS)` and `SystemParametersInfoForDpi` at 96/144/192 dpi. Every one of the five `LOGFONT`s — `lfCaptionFont`, `lfSmCaptionFont`, `lfMenuFont`, `lfStatusFont`, `lfMessageFont` — returns face name **`Segoe UI`**, weight 400. Not `Segoe UI Variable`, not any optical instance. The non-client metrics are a legacy Win32 compatibility surface and were never updated for the Windows 11 shell.

The Windows 11 taskbar is XAML/WinUI. It gets its font from the type-ramp theme resources, where the family is the bare variable family `"Segoe UI Variable"` and DirectWrite applies the `opsz` axis automatically to match the requested font size. Microsoft documents this: "when this font or another variable font with an optical axis is used, the optical size will automatically match the requested font-size", and the WinUI `BaseRichTextBlockStyle` in the published theme resources literally sets `FontFamily="Segoe UI Variable"`. There is no public API that reports "the font the taskbar clock is using"; UWPSpy-style visual-tree inspection of `Taskbar.View.dll` is the only way to read it out, and that is not something a shipping applet can do.

**But the size *is* readable, and that is the useful half.** `lfMessageFont.lfHeight` came back as **−12 / −18 / −24** at 96 / 144 / 192 dpi — that is 9 pt, i.e. exactly the 12 epx Caption size, tracking DPI linearly. So the recommended pattern for the applet is:

- Take the **pixel size** from `SystemParametersInfoForDpi(SPI_GETNONCLIENTMETRICS, dpi).lfMessageFont.lfHeight` (negate it; it is already in physical pixels for the requested DPI). This stays correct across DPI changes and — worth testing in `06` — plausibly across accessibility text scaling too.
- Take the **family name** from a hard-coded `"Segoe UI Variable Small"`, because the metric's `lfFaceName` is wrong for Windows 11. Guard it with the fallback check in (5) and fall back to `"Segoe UI"` if the family is absent (e.g. Windows 10).

Using `SystemParametersInfoForDpi` rather than the plain call avoids depending on the process's DPI-awareness mode. `GetSystemMetricsForDpi(SM_CXSMICON, dpi)` gives the matching icon size.

### 5. Does GDI+ render the variable-font named instances faithfully?

**Yes. GDI+ resolves the named instances correctly and renders three genuinely distinct optical designs. No collapse.** Three independent lines of evidence, all empirical on this machine:

**(a) Glyph outlines — resolution-independent, the strongest test.** Extracted outlines via `GraphicsPath.AddString` and normalised to a 1000-unit em. Max per-point deviation:

| Glyph | Small↔Text | Small↔Display | Text↔Display |
|---|---|---|---|
| `a` | 20.1 | 54.2 | 40.9 |
| `2` | 14.2 | 55.4 | 57.6 |
| `K` | 17.6 | 22.5 | 8.3 |
| `W` | 13.2 | 29.7 | 30.5 |
| `3` | 11.2 | 72.3 | 71.4 |

Deviations of 1–7 % of the em. Point counts are identical across the three (70 for `a`, 77 for `2`, …) — exactly what interpolated instances of one variable glyph look like — while `Segoe UI` has entirely different point counts (50, 96, 19, 34), confirming these really are Segoe UI Variable instances and not a fallback.

**(b) Rasterised pixel diff.** Rendered `KW32 Hamburgefonstiv` at 12/16/20/32 px under three `TextRenderingHint` values and byte-compared the 32bpp buffers. Every pair differs at every size and every hint, by thousands of bytes (e.g. at 20 px `AntiAliasGridFit`: Small↔Text 4644, Small↔Display 5109, Text↔Display 3588 differing bytes). Identical output would have proved collapse; nothing was identical.

**(c) Advance widths.** `MeasureString` on the same string differs consistently by instance, in the expected direction — `Small` widest, `Display` narrowest (at 9 pt: 170.2 / 164.6 / 163.1).

The visual check at 90 px is unambiguous: `Small` is open and wide-countered, `Display` is tight and refined.

**Three corollaries that DO affect the design:**

1. **GDI+ freezes `opsz` at the named instance's value — it does not vary optically with size.** Normalised outlines of the same family at em 12 and em 400 are identical to within 0.001 units per 1000 em. So unlike XAML/DirectWrite, which auto-matches `opsz` to the requested size, GDI+ hands you a static snapshot at `opsz` 8, 10.5 or 36. **We must pick the instance to suit our size ourselves; nothing does it for us.** This is why (1) had to do the points arithmetic rather than just naming the variable family.
2. **`new Font("Segoe UI Variable", …)` falls back to `Microsoft Sans Serif`.** Verified — the bare family name that XAML uses does not exist as a GDI family. Anything that names the family the way the shell's XAML does will silently render in the wrong font. An optical suffix is mandatory.
3. **The 31-character truncation is benign.** Passing the *full* untruncated name works for all 12 families, because GDI truncates the request to `LF_FACESIZE − 1` = 31 chars exactly as it truncated the registered name. Verified: `"Segoe UI Variable Display Semilight"` (35 chars) resolves to `Segoe UI Variable Display Semil`, and so on for every family. So `06` may use readable full names.

**Fallback-detection recipe for `06`,** since GDI+ never throws on an unknown family — it silently substitutes:

```
var font = new Font("Segoe UI Variable Small", px, GraphicsUnit.Pixel);
if (!font.Name.StartsWith("Segoe UI Variable")) { /* substituted — fall back to "Segoe UI" */ }
```

`Font.Name` reports the *resolved* family (`Microsoft Sans Serif` on failure) while `Font.OriginalFontName` echoes what was requested, so comparing the two, or prefix-testing `Name`, reliably detects substitution.

### Confidence summary

| Finding | Basis | Confidence |
|---|---|---|
| GDI+ does not collapse the named instances | Empirical, 3 methods | Very high |
| GDI+ freezes `opsz` per instance | Empirical, outline comparison | Very high |
| `"Segoe UI Variable"` falls back to MS Sans Serif | Empirical | Very high |
| `opsz` values 8 / 10.5 / 36, axis in points | Empirical, `fvar` parse | Very high |
| Clock is 12 epx / 16 epx line height | Empirical, screen measurement | High |
| `SPI_GETNONCLIENTMETRICS` returns legacy `Segoe UI` | Empirical | Very high |
| Clock's optical variant is `Small` | Doc + `opsz` arithmetic; **not** measurable at 12 epx | Medium-high |
| Clock's weight is Regular 400 | Doc; not measurable at 12 epx | Medium-high |
| Behaviour under accessibility text scaling | **Not tested** | — |

### Sources

- [Typography in Windows — Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography) — type ramp, optical/weight axes, 8–36 pt optical scaling, automatic `opsz`
- [XAML theme resources — Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources) — `CaptionTextBlockStyle` etc., `FontFamily="Segoe UI Variable"`
- [NONCLIENTMETRICS structure — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-nonclientmetricsa)
- [SystemParametersInfoForDpi — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfofordpi)
- `C:\Windows\Fonts\SegUIVar.ttf` — `fvar` table parsed directly
