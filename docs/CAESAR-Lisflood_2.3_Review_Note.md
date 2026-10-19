# CAESAR-Lisflood 2.3a and 2.3b: Changes, Merge Implications and Platform Implications

**Date:** 2026-10-07
**Reviewed:** `Caesar_Lisflood_2_3a.cs` and `Caesar_Lisflood_2_3b.cs` (uploaded). These are the main source files only.
**Not reviewed:** the rest of the SourceForge source package ("CAESAR-Lisflood 2.3 - SOURCE for release.zip", released 2026-03-11), which presumably includes the project file and the other forms.
**Compared against:**

| Name used below | Version | Commit |
|---|---|---|
| CAESAR-Lisflood 2.0.1 | as imported to GitHub | `ec7de12` |
| 2.0h | Tom's 2024 additions | `dcf0b50` |
| `Temperature` head | | `8a11bac` |
| `oil` head | | `199adb4` |

**Method:**
- Whitespace-insensitive line and function comparison.
- Trial three-way merges, run offline and aborted. A temporary `upstream` branch held 2.3a and 2.3b on top of `ec7de12`.
- The simulated merge result was checked against the expected set of changes.
- Nothing was compiled, committed or pushed.

---

## 1. Summary

- **2.3a is a small release on top of a base GRI already has.** It is 2.0.1 plus Tom's 2.0h changes (already in all GRI branches since October 2024), plus **two genuinely new physics changes**:
  1. **A bug fix in the hydraulic friction term in `qroute()`.** `Math.Pow(hflow, (10 / 3))` used integer division, so it computed `hflow^3` instead of `hflow^(10/3)`. 2.3a corrects it to `(10.0 / 3.0)`, which matches the published Bates et al. (2010) formulation. **This changes every run in which water moves**, including all GRI branches, which still carry the bug.
  2. **Removal of a clamp on the slope term in the shear stress calculation in `erode()`.** This affects sediment runs only.
- **2.3b is 2.3a plus one experimental change** to how entrained sediment is shared between neighbouring cells in `erode()`. It adds a bed-slope term with weights 0.7 / 0.2 / 0.1 instead of 0.75 / 0.25. It identifies itself as "2.3a" internally, so the two builds cannot be told apart from the title bar.
- **The merge is easy.**
  - With `-Xignore-space-change`: **8 conflict regions**, all trivial.
  - Without it: 11 conflicts against `Temperature` or `oil`, or 12 after the oil merge, mostly indentation in `qroute()`.
  - The simulated result differs from `Temperature`+`oil` by exactly the intended lines.
- **The physics consequences are not small.**
  - With the friction fix, shallow flow (under 1 m) feels more friction than before: about 2.2 times more at 0.1 m and 4.6 times more at 0.01 m. Deep flow feels less.
  - Floodplain flow will be slower and channel flow slightly faster.
  - This affects water temperature (residence times and advective mixing), oil transport, tracers and sediment.
  - Every hydraulic baseline must be re-captured after merging. Still-water tests should be unaffected (§5.5).
- **Platform work:** no new obstacles for the CLI, .NET 10 or Linux. The release does confirm that upstream development continues in the single large file, distributed as zip files outside git. That makes the consolidation and "vendor branch" recommendations more important, and argues for a refactor that keeps upstream changes portable (§6).

---

## 2. Lineage: what 2.3 is built from

```
ec7de12  CAESAR-Lisflood 2.0.1 (14/04/2024), imported to GitHub Oct 2024
   |
   +-- dcf0b50  "Add Tom's additions from April" (2.0h, 17/9/2024) + oil framework
   |      |
   |      +-- ... GRI development ... --> master, oil, Temperature  (all identify as "2.0h")
   |
   +-- [SourceForge only] 2.3a (code dated 28/10/2025)  --> 2.3b
```

Line-by-line comparison, ignoring whitespace, shows:

- **Every non-oil, non-cosmetic change in GRI's 2.0h import is also in 2.3a.** That covers the `slide_5()` restructure, the `slide_3()` limits, the lateral erosion clamps, the `.asc` output names and the `hflow` cap. Tom's 2.0h work therefore reached both lines, and GRI is not missing any of Tom's earlier changes.
- **The only lines in 2.3a that are not in GRI** are the two physics changes in §3.1 and §3.2, plus the version labels.
- **2.3 has none of GRI's later work:** oil, TEMP_V1, WATERINIT_V1, the XML loader error report, the `isOilSimulation` guards, and the status-message queue in `erode()`.

So the two lines diverge only in one direction at present. That is the cheapest possible moment to consolidate.

---

## 3. What 2.3a adds

### 3.1 Friction exponent fix in `qroute()` (the important change)

**Where:** `qroute()`, the x-direction and y-direction flux updates. These are lines 10608 and 10660 of `Caesar_Lisflood_2_3a.cs`. The header comment calls them "some modifications inc in qroute 10.0/3.0".

```csharp
// 2.0.1, 2.0h and all GRI branches:
Math.Pow(hflow, (10 / 3))       // 10 / 3 is integer division = 3
// 2.3a and 2.3b:
Math.Pow(hflow, (10.0 / 3.0))   // = 3.333...
```

**What it means.** The flux update is the inertial formulation of Bates et al. (2010):

```
q_new = (q - g h dt S) / (1 + g h dt n^2 |q| / h^(10/3))
```

The friction part of the denominator simplifies to `g dt n^2 |q| / h^(7/3)`, which is the published form. With the integer-division bug it became `g dt n^2 |q| / h^2`. The bug therefore changed the depth dependence of friction, not just its magnitude.

**Size of the effect.** The friction term with the fix, divided by the friction term with the bug, is `h^(-1/3)`:

| Flow depth `h` (m) | 0.01 | 0.05 | 0.1 | 0.25 | 0.5 | 1 | 2 | 5 |
|---|---|---|---|---|---|---|---|---|
| Friction, fixed / buggy | 4.64 | 2.71 | 2.15 | 1.59 | 1.26 | 1.00 | 0.79 | 0.58 |

Shallow overland and floodplain flow has been under-damped, and so too fast. Flow deeper than 1 m has been over-damped, and so slightly too slow. The `hflow` used here is the flow depth between cells, capped at `DX/2` (§3.4).

**Status in GRI's code.** The bug is present in `Temperature` (lines 12371 and 12424) and in `oil` (lines 11108 and 11161). These are the only two places in the GRI code where an integer-only fraction appears inside `Math.Pow`, according to a pattern search. That search is not a full analysis (§6.3).

### 3.2 Shear stress slope term in `erode()`

**Where:** `erode()`, the shear stress (`Tau`) calculation, line 12417 of 2.3a.

```csharp
//if (slopetot > 0) slopetot = 0;        // clamp now commented out
tau = 1000 * ci * vel * vel * (1 + (1 * (slopetot / vel)));
```

`slopetot` is the velocity-weighted sum of the bed slopes in the directions water is flowing.

- **Before:** it was clamped to be no greater than zero, so slope could only *reduce* shear stress (on adverse slopes).
- **Now:** a downhill slope also *increases* shear stress, so there is more entrainment on steep downslope flow.

This affects sediment runs only, because `erode()` is skipped when "flow only" is set.

### 3.3 Labels

- The header comment reads: "28/10/25 Version 2.3a some modifications inc in qroute 10.0/3.0".
- `basetext` and the designer title read "CAESAR - Lisflood 2.3a (28/10/2025)".

### 3.4 Changes in 2.3a relative to 2.0.1 that GRI already has (from 2.0h)

These are listed for completeness. None of them needs merging work:

- The `hflow` cap in `qroute()`: `if (hflow > DX/2) hflow = DX/2; // contreversial line...`. GRI's copy is formatted `DX / 2`, which is why it produces a conflict (§5.3). The cap limits the depth used in the flux calculation to half the cell size, for example 2.5 m at 5 m resolution. Tom labels it "controversial", and it is worth discussing alongside the friction fix, because the two interact for deep channels on fine grids.
- `slide_5()`: the slope-failure loop now skips cells at or below the active-layer-over-bedrock level, rather than setting a very large threshold for them. It also tracks grain sizes with `slide_GS()` and clamps each moved amount to be non-negative.
- `slide_3()`: each moved amount is capped at `ERODEFACTOR` and clamped to be non-negative.
- Lateral erosion in `erode()`: `if (amt < 0) amt = 0;` in four directions.
- Output file extensions changed from `.txt` to `.asc` for water depth, elevation difference, elevation, d50, velocity and tracer grids.
- `edge` and `edge2` made `public static`.

### 3.5 Cosmetic changes

Re-indentation in `update_tracer_states()`, and trailing-whitespace edits in `stage_tidal_input()` and elsewhere. Using `-Xignore-space-change` in the merge keeps them from producing conflicts.

---

## 4. What 2.3b adds over 2.3a

**One change, in `erode()`,** where entrained sediment is shared out between the four neighbouring cells. This is lines 12675 to 12735 of 2.3b:

| Share of entrained sediment, driven by | 2.3a | 2.3b |
|---|---|---|
| Flow velocity in that direction | 0.75 | 0.70 |
| Lateral "edge" gradient | 0.25 | 0.20 |
| Bed elevation gradient (new) | none | 0.10 |

Each share is normalised over the wet neighbours that have the relevant gradient. The new term needs a new accumulator, `elevtot`, built in the first neighbour loop under the same conditions as it is used in the second, so there is no division by zero.

Observations:

- **It is marked as experimental** in the code ("maybe add in bedslope too?").
- **The version labels are unchanged,** so 2.3b still reports itself as 2.3a. Runs from the two builds cannot be distinguished by their title or header, which is a provenance risk if both are in use.
- **There is an accidental edit to a comment:** `//double gravity = 9.8;temptot2`. It is harmless.
- **The shares add up to 1 only where all three gradients are present.** Elsewhere they add to between 0.7 and 0.9. Sediment that is not sent stays in the cell, so mass is conserved, but the amount transported per step depends on how many of the three gradients exist. 2.3a has the same property (0.75 where there is no edge gradient). This is a question for Tom, not a bug.
- **It affects sediment runs only.**

**Recommendation:** merge 2.3a now, and hold 2.3b until Tom confirms whether it is the recommended release or a trial.

---

## 5. Merge implications for `Temperature` and `oil`

### 5.1 Approach: an `upstream` (vendor) branch

Tom's releases arrive as source zip files, not git commits. Record each one as a commit on a dedicated branch rooted at the 2.0.1 import. Git can then do proper three-way merges, now and for every future release:

```
git checkout -b upstream ec7de12
# copy the 2.3a release files over the working tree (main .cs at minimum; ideally the whole source package)
git commit -am "Upstream CAESAR-Lisflood 2.3a (SourceForge, code dated 28/10/2025)"
# repeat for 2.3b if wanted, as a second commit
git push -u origin upstream
```

Using the whole source package also captures any project-file or resource changes. Those have not been reviewed here.

### 5.2 Trial merge results

| Merge | Default conflict style | With `-Xignore-space-change` |
|---|---|---|
| 2.3a or 2.3b into `Temperature` (current) | 11 | not run |
| 2.3a or 2.3b into `oil` (current) | 11 | not run |
| 2.3a or 2.3b into `Temperature` **after** the oil merge (per the oil merge note) | 12 | **8** |

All conflicts are in `Caesar Lisflood 2.0.cs`. Recommended command, after the oil merge:

```
git checkout Temperature
git checkout -b merge-upstream-2.3a
git merge --no-ff -Xignore-space-change upstream
```

`-Xignore-space-change` lets git auto-merge the friction fix next to GRI's re-indented `qroute()` code and its `qx_prev` / `qy_prev` lines (needed by the oil code). Without it, those regions come out as conflicts that you then resolve by hand.

### 5.3 Conflict resolution (with `-Xignore-space-change`, after the oil merge)

| # | Location | Cue | Resolution |
|---|---|---|---|
| 1 | Header comments | GRI's oil and TEMP_V1 attribution lines against Tom's "28/10/25 Version 2.3a" line | **Keep both** |
| 2 | `basetext` | "2.0h (17/9/2024)" against "2.3a (28/10/2025)" | **A decision** (§5.8): choose a label that identifies the GRI line, for example `"CAESAR - Lisflood 2.3a-GRI (TEMP_V1, OIL_V1)"` |
| 3 | `save_data()` file names, cycle 0 | GRI's oil file name lines against blank lines | **GRI** (`HEAD`) |
| 4 | `save_data()` file names, cycle > 0 | GRI's oil file name lines against nothing | **GRI** |
| 5 | Blank lines before `qroute()` | Blank lines only | **GRI** (either is fine) |
| 6 | `qroute()`, x-direction `hflow` cap | `DX / 2` against `DX/2`: the same statement | **GRI** |
| 7 | `qroute()`, y-direction `hflow` cap | The same as 6 | **GRI** |
| 8 | End of file | A blank line | **Either** |

Without `-Xignore-space-change` there are four further regions in `qroute()` and `erode()`. For those:

- Keep GRI's side, including the `qx_prev[x, y] = qx[x, y]; // OIL_V1` and `qy_prev` lines.
- Change `(10 / 3)` to `(10.0 / 3.0)` in both flux updates.
- Comment out `if (slopetot > 0) slopetot = 0;`.

Take `Temperature`'s side of the `stage_tidal_input()` region; 2.3 changed only whitespace there.

### 5.4 Verified result

Applying §5.3 to the simulated `Temperature`+`oil` merge, a whitespace-insensitive diff against `Temperature`+`oil` shows exactly these changes:

- The 2.3a header comment line.
- The designer title "CAESAR Lisflood 2.3a". It auto-merges, and it is overwritten at runtime by `basetext`.
- `Math.Pow(hflow, (10.0 / 3.0))`, twice, in `qroute()`.
- `//if (slopetot > 0) slopetot = 0;` in `erode()`.
- **If 2.3b is merged:** the `elevtot` accumulator and the 0.7 / 0.2 / 0.1 weights in `erode()`, plus the comment typo.

Nothing in any TEMP_V1, WATERINIT_V1 or OIL_V1 code is touched. The CLI Phase 1 anchors are unaffected (§6.1).

### 5.5 Consequences for the temperature work

**Expected to be unaffected:**

- **Function-level verification of the energy balance** (both schemes, verified to machine precision against independent implementations). These are pure functions of temperature and forcing.
- **Still-water tests: Test A (single cell) and Test B (stepped-bed depth ladder).**
  - With a level water surface and dry walls, the flux stays exactly 0. The friction term is multiplied by `|q| = 0`, so the change has no effect.
  - **Prediction: both should reproduce their pre-merge outputs bitwise.** That is a useful check on the merge itself. If they don't, the domain was not in fact perfectly still.
  - Sediment is also inactive there, because there is no velocity and so no shear stress.

**Expected to change:**

- **Any run with flowing water:** B1 and B2 (`squaredem-temperature.xml`, stage input), B3 (Carlisle), the pond test, and all real-world runs.
  - Floodplain cells fill and drain more slowly, which changes how long the water sits and how heat is mixed into it.
  - The depth at which shallow cells sit (and so the forward-Euler instability band in §3d of the status document) may shift.
- **Re-baselining.** Capture new baselines after this merge. The relaxation acceptance tests and the 1-minute reference run should be generated on the post-2.3 hydraulics, so that they don't need redoing.

### 5.6 Consequences for the oil work

- **Oil is advected by the water fluxes `qx` and `qy`**, so oil results change wherever the water flows, which is essentially all real cases.
- **Order matters for the oil merge test.** Its oil-on equivalence test (merged build against `origin/oil`, expected bitwise identical) must be done **before** 2.3a is merged, because afterwards the hydraulics differ from Paola's branch by design.
- **Tell Paola.** Any oil calibration done on the old friction term (for example the change to `Cf`, or the diffusion settings) may need revisiting.

### 5.7 Recommended order (updates the oil merge note and CLI plan sequencing)

1. CLI Phase 0 baseline on the current `Temperature` head.
2. Oil merge, verified: oil-off cases bitwise unchanged, oil-on case O1 bitwise equal to `origin/oil`.
3. Create `commandline` from the merged `Temperature`. Do CLI Phases 1 and 2, and prove CLI and GUI bitwise identical. Merge back into `Temperature`.
4. Create the `upstream` branch with 2.3a, and 2.3b if wanted, as separate commits.
5. Merge 2.3a into `Temperature` as **its own commit**, the first physics change measured with the CLI harness:
   - Tests A and B are expected bitwise unchanged.
   - The flowing cases (B1 to B3, O1) are expected to change; quantify the changes with scripted runs.
   - Record the new baselines.
6. Then the relaxation reformulation, and so on, as planned.

Doing the friction fix after the CLI exists means its effect can be measured systematically: depth sweeps, resolution sweeps, and before/after comparisons on every test case. That supports a clear statement to Tom and Paola about what changed.

### 5.8 Decisions needed

1. **2.3a only, or 2.3b too?** Recommendation: 2.3a now; 2.3b after Tom confirms its status.
2. **The version label for the merged line.** The GRI branches currently report "2.0h", which is no longer accurate. A label carrying the upstream version and the GRI additions supports provenance: the CLI's planned output headers will record it.
3. **Whether to adopt the friction fix at all.** It is strictly a correction to the published formulation, so the recommendation is yes. But it changes results that may already have been reported or calibrated using GRI builds, so record which past runs used the uncorrected term.

---

## 6. Implications for the CLI, .NET 10, refactor and Linux

### 6.1 CLI

- **No change to the plan.**
  - 2.3 does not touch any Phase 1 anchor: the `Form1` declaration, the config-open handler, `main_loop`, the `BeginInvoke` in `erode()`, `button1_Click`, or the four message sites.
  - It adds no message boxes: 2.3a has 45 `MessageBox.Show` calls, the same as 2.0.1.
  - The nested `MessageBox` shim would cover Tom's code automatically after any future upstream merge.
- **2.3, like GRI's code, sets `tempStatusPanel.Text` directly from inside the `Parallel.For` in `erode()`.** This happens in two places, for the velocity-limit message. In headless mode no window exists, so these are harmless, field-only writes. In the GUI they are cross-thread writes, the same as before. This is unchanged by the merge.
- **The friction fix is a natural first use of the harness** (§5.7).

### 6.2 .NET 10

- **No new obstacles.** 2.3 uses the same `MainMenu` and `StatusBar` controls, the same unused `using System.Runtime.Remoting...` and `using System.Security.Policy` directives, and the same `gdi32` call.
- **Check the project file in the 2.3 source package** for its target framework and any new references. GRI's project targets .NET Framework 4.6.1, and we have not seen Tom's.

### 6.3 Refactor

This release is the most direct evidence so far for the merge-strategy concerns in the platform plan:

- **Upstream development continues** in the single large file, and is distributed as zip files outside git. Without a vendor branch, each release has to be compared by hand. With one, a release that touches a few lines merges in minutes, as here.
- **Tom's changes are typically small, local edits inside physics functions:** `qroute()`, `erode()`, `slide_3()`, `slide_5()`. After the refactor, such edits stay easy to port if the refactor **moves** functions into `CaesarModel` without renaming or restructuring their bodies. That should be an explicit rule for the refactor: move first, and improve later, in separate commits.
- **The best long-term outcome is still a single common base.** Either Tom adopts the refactored code and git, or Tom's releases are made from a branch in the shared repository. The current divergence is tiny (two physics changes, plus 2.3b's experiment) and runs one way only, so consolidating now costs very little.
- **The friction bug shows the value of the planned engineering infrastructure.** The bug sat in the most-used function of the model, and the compiler gives no warning for integer division inside a floating-point expression. The modernisation should include:
  - a static-analysis check for possible loss of fraction (integer division used where a floating-point result is expected), run once over the whole file and then kept in the build;
  - regression cases with flowing water whose expected behaviour is known independently (for example a uniform-flow channel with an analytical Manning depth). Such a case would have exposed this bug directly.

### 6.4 Linux

No change. The fix is pure arithmetic and is not affected by platform or runtime.

### 6.5 Suggested updates to the platform plan document before it goes to Tom

- **§2.1 (branches):** add the SourceForge 2.3 release and its relationship to the GRI line (§2 above).
- **§9 (merge strategy):** add the vendor-branch process (§5.1), and the refactor rule "move, don't rewrite".
- **§12 (questions):**
  - Question 2: narrow it to changes **since 2.3**.
  - Question 8 (licensing): partly answered, since the source carries a GNU GPL v3 notice. Redistributing builds or container images must comply with GPL v3, including making the corresponding source available. The question becomes one of attribution and naming.
  - Add the questions in §7 below.

---

## 7. Questions for Tom

1. **Is 2.3b a release, or a trial?** If it is a release, should its version label differ from 2.3a?
2. **The friction exponent.** Has the effect of the `10 / 3` correction on results been assessed, for example against earlier published runs? Does it change his recommended Manning's n values for typical applications?
3. **The `hflow` cap at `DX/2`** ("controversial line"). Is it intended to stay, and how does it interact with the corrected friction term on fine grids with deep channels?
4. **The shear stress slope term.** Is removing the `slopetot` clamp intended for all sediment runs, or is it tied to the 2.3b experiment?
5. **The 2.3b sediment shares.** Is it intended that the shares add to less than 1 where a gradient is missing, so that less sediment moves per step there?
6. **Future releases.** Would he be willing to make releases from a branch in the shared repository, or at least to tag the source zip contents, so that GRI's vendor branch can track them exactly?
