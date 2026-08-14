# 03 — Test project and the glyph's measured properties

**What to build:** `dotnet test` at the repo root, with no arguments, runs a green suite that would
catch the bugs `06` and `12` found by hand. That command working bare is the whole reason the
solution file exists.

The suite asserts **measured properties** — numbers read back out of a render that actually happened
— and never golden images. [§11.3](../../calendarweek-tray-v1/spec.md) records why a checked-in
reference PNG loses: a Windows font update breaks every golden with no bug present, and a broken
golden reports *that* something changed, never *what*.

Read §11.1 for the project shape and §11.2 for the assertions. The 53-week sweep is non-negotiable:
the `"00"` bug hit weeks 4, 14, 24, 34 and 40–49 specifically, so any sample can miss it.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] test project per §11.1, wired into the solution so bare `dotnet test` finds it
- [ ] the sweep is all 53 weeks × {16, 20, 24, 28, 32} px
- [ ] per (week, size): digit ink ≥ 1 px inside the page on left, right and bottom, and ≥ 1 px below the binding bar
- [ ] per (week, size): left and right gaps differ by ≤ 1 px, and the centring loop reports converged
- [ ] per size: no week's digit ink exceeds the reference label's at the same fitted size
- [ ] per size: bar height exact, and alpha **exactly 0** at both slot centres — no tolerance
- [ ] no golden images, no checked-in reference bitmaps
- [ ] restore needs no network — both package versions are already in the local NuGet cache
- [ ] the suite is green
