# Merge Note: `oil` into `Temperature`

**Date:** 2026-10-07
**Branches (GitHub state at time of writing):** `Temperature` at `8a11bac`, `oil` at `199adb4`
**Merge base:** `62df859` (26 March 2025)
**Basis of this note:** a trial merge, run offline and aborted. Nothing was committed or pushed.

---

## 1. Summary

- `oil` carries one substantive commit not in `Temperature`: `199adb4` (Paola, 23 July 2026). It rewrites the oil routing, input, trigger, evaporation and volume balance code. It touches only `Caesar Lisflood 2.0.cs`.
- Merging gives **28 conflict regions**, all in that file, using git's default conflict style. With `merge.conflictstyle=diff3` set, git reports 31, because it splits some regions differently.
- **Recommended resolution:**
  - Take Paola's version in every oil-owned function.
  - Keep all of `Temperature`'s TEMP_V1 code.
  - Keep two `Temperature` safeguards that `oil` lacks: the `isOilSimulation` guards on the oil outputs in `save_data`, and the XML loader's error report.
  - Make one decision on a line git merges silently without a conflict (§6).
- **Offline checks of the recommended resolution:**
  - The resolved oil-owned functions are identical to `oil`'s (whitespace-normalised), except the §6 line.
  - No removed oil symbol is still referenced.
  - Nothing is declared twice.
  - It has **not** been compiled, because no Windows C# compiler was available here. A Visual Studio build is the first check (§7).
- **Effort:** about an hour to resolve, plus the test runs in §7.

## 2. Why this merge is awkward

The two branches hold **two versions of the same oil work**, not two independent features:

- `639b5a2` (your commit of 14 July 2026, "updated oil spill module pending addition of temperature") is the commit `Temperature` was branched from. It applied an intermediate version of the oil updates.
- `199adb4` was built from the older oil state, not from `639b5a2`, and contains Paola's later version of the same work.

The merge base is therefore March 2025, and git sees both branches rewriting the same oil functions. Two consequences follow:

- **Most conflicts are between an older and a newer version of Paola's code.** The newer one should win.
- **Where `639b5a2` changed a line that Paola's file left unchanged, git silently keeps `639b5a2`'s version.** An offline check found exactly one such line affecting behaviour (§6). The other is the attribution comment at the top of the file, which should be kept.

`Temperature` has made **no edits** to the oil-owned functions since branching. Every conflict inside them can therefore be resolved by taking `oil`'s side.

## 3. Prerequisites and sequencing

1. **Confirm with Paola** that `199adb4` supersedes all the oil changes in `639b5a2`, and go through the questions in §9. The resolution below assumes it does.
2. **Commit and push everything first** on every machine. This analysis reflects GitHub only, so unpushed local commits on either branch would change it.
3. **Sequence it with the CLI work** so that one baseline serves both:
   1. Capture the CLI Phase 0 GUI baseline (B1 to B3, all with oil off) on the current `Temperature` head, **before** creating `commandline`.
   2. Do this merge on a `merge-oil` branch and verify it (§7). B1 to B3 must be bitwise unchanged.
   3. Merge `merge-oil` into `Temperature`, then create `commandline` from the merged head. Re-run one baseline case to confirm the hashes still match.

   This changes the order in `CLI_Phase0_Phase1_Instructions.md` §3.2: baseline before branching, and `commandline` created after the oil merge. The Phase 1 anchors survive this merge unchanged. All the "Find" strings are still present exactly once, and the file still has 65 `MessageBox.Show` calls.

## 4. Procedure

```
git checkout Temperature
git pull
git fetch origin
git checkout -b merge-oil
git merge --no-ff origin/oil
```

Resolve the conflicts as set out in §5, working top to bottom. Line numbers are those in the conflicted file at the current GitHub heads, and they shift as you resolve, so use the "Cue" column to recognise each region. Then make the §6 decision and run the §7 checks before committing.

The default conflict style matches the numbering below. Setting `merge.conflictstyle=diff3` shows the common ancestor too, which is useful context, but the region count and boundaries then differ from this table.

## 5. Conflict-by-conflict resolution

"Temperature" is the `HEAD` side, between `<<<<<<< HEAD` and `=======`. "oil" is the `origin/oil` side, between `=======` and `>>>>>>> origin/oil`.

| # | Approx. line | Location | Cue | Resolution | Notes |
|---|---|---|---|---|---|
| 1 | 357 | Field declarations | Temperature: old oil field list, then the whole TEMP_V1 field block. oil: new oil field list plus new fields | **Combine:** oil's side first, then Temperature's TEMP_V1 block, from `// TEMP_V1 - water temperature module` down to the `windFunc_..._separated` line. Drop Temperature's old oil field lines | The old fields (`cinematic_v`, `error_edge`, `error_edge_sum`, `Vout_1` to `Vout_4`) are not referenced anywhere after the merge. oil's side declares `bool oil_spill_ended` here, which is why conflict 4 removes the other copy |
| 2 | 6992 | `erodedepo()` | Temperature: `if (isOilSimulation == true) { Vout_total = 0; ...` edge-removal loops. oil: empty | **oil** (delete the block) | The edge removal has moved into `oil_global_volume_balance()`, which is only called when oil is on, so `Temperature`'s guard is preserved in effect |
| 3 | 7042 | `erodedepo()` | Blank lines only | **oil** | Cosmetic |
| 4 | 7711 | Just above `oil_spill_input()` | Temperature: `bool oil_spill_ended = false;` | **oil** (blank) | Keeping both copies is a duplicate-definition compile error |
| 5 | 7721 | `oil_spill_input()` | Temperature: `iteration_t += local_time_factor;` and the old loop. oil: `if (oil_spillt <= 1)` | **oil** | New input logic |
| 6 | 7739 | `oil_spill_input()` | Temperature: `spill_end`, `if (oil_spill_ended = true)`. oil: three lines setting `oil_in_sum_source` | **oil** | Also removes the old assignment-instead-of-test bug |
| 7 | 10073 | `save_data()` file names | Temperature: `massbalance.txt`. oil: `oilvolumebalance.txt` plus a `froude.asc` line | **oil** | `froude.asc` is an unfinished hook (§10); it is harmless because nothing calls it |
| 8 | 10097 | `save_data()` file names | Temperature: a commented `massbalance` line | **oil** | Cosmetic |
| 9 | 10526 | `save_data()`, typeflags 8 and 9 | Temperature: `if (typeflag == 8 && isOilSimulation == true)` with the old body. oil: `if (typeflag == 8)` with the new header-writing body | **oil's side, with `Temperature`'s guards restored on both conditions** (text below) | The guards came in with TEMP_V1 (`5cf0ae8`). Without them, ticking "oil concentration" in the save menu with oil off writes from an unallocated `oil_concentration` array |
| 10 | 12742 | `oil_evaporation()` | Temperature: the per-cell `Parallel.For`. oil: four-line domain-level calculation | **oil** | Also removes a race on the shared `iteration_t` |
| 11 | 12804 | `oilroute()`, x direction | Temperature: old `oil_Dx`. oil: `oil_depth_init = Math.Max(...)`, `advection`, `diffusion` | **oil** | |
| 12 | 12820 | `oilroute()`, x direction | Temperature: old `oil_hx` update with sign forcing. oil: `//ADE` and the new `oil_hx` update | **oil** | |
| 13 | 12841 | `oilroute()`, y direction | Temperature: old y-direction routing. oil: the tail of a commented-out `/*else ... */` block | **oil** | Taking oil is correct even though its side looks like only a comment: the new y-direction routing arrives in the auto-merged text that follows. The §7 function check confirms it |
| 14 | 12925 | `oil_update` signature | `oil_update()` against `oil_update(double local_time_factor)` | **oil** | The call in `erodedepo()` auto-merges to the new signature |
| 15 | 12963 | `oil_update()`, concentration | Temperature: `.../ 1000; // g/L`. oil: mass-fraction form | **oil** | A unit change (§8, §9) |
| 16 | 12983 | `oil_global_volume_balance()` | Temperature: `oil_evap_t = 0;` and comments. oil: `double oil_evap_t = 0;` | **oil** | |
| 17 | 13000 | `oil_global_volume_balance()` | Temperature: a `//Vout += ...` comment | **oil** | Cosmetic |
| 18 | 13006 | `oil_global_volume_balance()` | Temperature: `oil_in_sum = oil_totalv - Vout_total - oil_evap_sum;` | **oil** | The new balance comes later in the function |
| 19 | 14098 | `initialise_oil_simulation()` | Temperature: `oil_density = 0.839` ... `vel_wind = 1`. oil: `0.864` ... `vel_wind = 10` | **oil** | These changed defaults change oil results (§8) |
| 20 | 14125 | `initialise_oil_simulation()` | Temperature: a commented `oil_transf_coeff` line | **oil** | Cosmetic |
| 21 | 14142 | `initialise_oil_simulation()` | oil: `Vout_border = new ...`, `down_scan_oil = new ...` | **oil** | Essential: without these allocations, `scan_area()` and the volume balance throw `NullReferenceException` |
| 22 | 15629 | `drawwater()`, oil depth | Threshold `0.00001` against `0.0000001` | **oil** | Display only |
| 23 | 15672 | `drawwater()`, `GetViridisColor` | Temperature: the two `normalizedValue` lines. oil: empty | **oil** (empty) | The same two lines follow straight after the region, so keeping Temperature's side declares `normalizedValue` twice, which is a compile error |
| 24 | 15692 | `drawwater()`, oil concentration | Thresholds | **oil** | Display only |
| 25 | 15701 | `drawwater()`, oil concentration | `GetViridisColor(..., 0.00001, ...)` against `0.0000001` | **oil** | Display only |
| 26 | 18273 | `button1_Click()` | Comment text only | **oil** | Cosmetic |
| 27 | 19640 | `menuItemConfigFileOpen_Click()`, end of the XML loader | Temperature: `catch (Exception eXML) { MessageBox.Show("Error with loading XML file." ...` oil: `catch { }` | **Temperature** | From TEMP_V1 commit `0ecc261`. oil's bare `catch { }` silently swallows XML load failures. It also matters for the CLI, which relies on this message to fail a bad configuration |
| 28 | 21280 | End of file | A blank line | **oil** (either is fine) | Cosmetic |

**Conflict 9, resolved text.** Take oil's side, then change its two condition lines:

```csharp
            if (typeflag == 8 && isOilSimulation == true)  //OIL_V1_PDF
```

```csharp
            if (typeflag == 9 && isOilSimulation == true)  //OIL_V1_PDF
```

Leave the bodies exactly as oil has them.

## 6. Silent auto-merge needing a decision: `oil_area()`

This is not a conflict, so git applies it without asking:

| Version | Line in `oil_area()` |
|---|---|
| Merge base | `if (oil_depth[x, y] > 0)` |
| `639b5a2` (in `Temperature`) | `if (oil_depth[x, y] > 0.00001)` |
| `199adb4` (Paola) | `if (oil_depth[x, y] > 0)` |
| Merge result | `if (oil_depth[x, y] > 0.00001)`, because only `Temperature` changed it from the base |

`oil_area()` counts the cells that make up the slick area `oil_S`, which feeds the evaporation exposure. The threshold therefore affects evaporated volume, not just display.

**Recommendation:** adopt Paola's `> 0`, since hers is the version she has been testing and it matches the `> 0` tests elsewhere in her code, unless she says the `0.00001` threshold was intended. Edit in `oil_area()`:

```csharp
                    if (oil_depth[x, y] > 0)
```

This choice also determines the outcome of the oil-on equivalence test in §7, step 5.

## 7. Verification

1. **No markers remain:**

   ```
   findstr /n /r "^<<<<<<< ^======= ^>>>>>>>" "Caesar Lisflood 2.0.cs"
   ```

   This should find nothing.

2. **Build** in Visual Studio. There should be no new errors.

3. **Review the change against `Temperature`:**

   ```
   git diff 8a11bac -- "Caesar Lisflood 2.0.cs"
   ```

   This should contain only:
   - the oil changes in §8;
   - the menu label "oil volume balance" (in `InitializeComponent`) and the matching save-option text (in `menuItemConfigFileSave_Click`);
   - the removal of the `erodedepo()` edge block (conflict 2);
   - the removal of the duplicate `oil_spill_ended` (conflict 4).

   It should contain nothing in any TEMP_V1 or WATERINIT_V1 code.

4. **Non-oil runs are bitwise unchanged.** Run B1 to B3 in the GUI, following the Phase 0 procedure, and compare the manifests with the pre-merge baseline. They must be identical. All the new oil code is gated on `isOilSimulation`, including the extra scan in `scan_area()`.

5. **Oil-on equivalence against Paola's branch.** Use `testing/debug.xml` (case O1). It has oil on, a depth trigger at 1.5 m, a 3 x 3 source and 1000 m³ over 600 s. Reduce `maxrunduration` to a short run, and leave temperature off. Then:
   1. Build `origin/oil` itself (`git checkout origin/oil`, build) and run O1, using the Phase 0 procedure and the same machine and configuration. Hash the outputs.
   2. Build `merge-oil` and run O1 the same way.

   Interpreting the result:
   - With the §6 line set to `> 0`, the oil code in the merge is exactly Paola's, so the two runs are **expected to be bitwise identical**.
   - Any difference means a `Temperature`-side change to shared code (the hydraulics, the loaders, or the initialisation) is active with temperature off. It must be traced before accepting the merge.
   - If `0.00001` is kept instead, a difference is expected, and this test measures its effect.

6. **Oil-on physical sanity (O1, merged build):**
   - The volume balance closes: oil in the domain + cumulative out + evaporated + negative-depth error = total released.
   - No negative oil depths remain.
   - The release stops at exactly the requested total.

7. **Commit:**

   ```
   git commit -m "Merge oil (199adb4) into Temperature: Paola's oil routing, input, trigger, evaporation and volume balance; Temperature safeguards retained (save_data oil guards, XML load error report); oil_area threshold per Paola"
   ```

   Then merge `merge-oil` into `Temperature` and push.

## 8. Behaviour changes arriving with this merge (oil runs only)

**Routing:**
- Fluxes are now rates, so results no longer depend on cell size.
- The local diffusion coefficient is computed from the deeper of the two adjacent cells.
- Fluxes are gated by `hflow_threshold`.
- A qroute-style limiter caps how much oil can leave a cell in one step.
- Routing runs only over an oil-specific scan list.

**Input and trigger:**
- A duration of 1 or less means an instantaneous release; otherwise the release is continuous with an exact total.
- `oil_n_cells` now counts source cells inclusively. O1's 3 x 3 source was previously counted as 4 cells.
- The trigger fires on depth (start time 0, depth > 0) **or** on time (depth 0, time > 0), and never fires when both are set.

**Evaporation:**
- Now a domain-level calculation.
- The exposure term uses the timestep in seconds, where it previously divided by 60.
- `Ev` is clamped to the range 0 to 1.

**Outputs:**
- `massbalance.txt` becomes `oilvolumebalance.txt`, with a header row and new columns.
- Oil concentration is now a mass fraction, where it was previously labelled g/L.

**Changed defaults:**

| Parameter | Old | New |
|---|---|---|
| `oil_density` | 0.839 | 0.864 |
| `oil_T` | 298 | 298.15 |
| `vel_wind` | 1 | 10 m/s |
| `Cf` | 0.02 | 0.05 |

**Unchanged:** the GUI controls and the XML configuration schema. Old configurations load as before. The renamed save-option text is harmless, because save options are read by position.

## 9. Questions for Paola

1. Does `199adb4` supersede all the oil changes in `639b5a2`?
2. `oil_area()`: should the threshold be `> 0` or `> 0.00001` (§6)?
3. Trigger: is "never triggers when both start time and depth are set" intended, or should one take precedence? The GUI labels ("Earliest start time (s)" and "Start at critical depth (m)") describe the old combined behaviour, where the spill starts once the time has passed **and** the depth is reached. They would need rewording if the two are now alternatives.
4. Evaporation exposure: is the change from `local_time_factor / 60` to `local_time_factor` a deliberate units correction? Which source equation, and which units for `oil_transf_coeff`, does it follow?
5. Concentration: is the change from g/L to mass fraction intended, and should the output header or documentation state the unit?
6. The `froude` array and the `froude.asc` output name: what is the intended Froude output, and should it be completed or removed?
7. Does she have reference results for `testing/debug.xml`, or another oil case, from her build?

## 10. Follow-ups after the merge

- **TEMP_V1 item 7 (live water temperature in oil evaporation)** should now be designed against the new `oil_evaporation()`. Because that function now computes a single domain-level `Ev`, the temperature input needs a representative value, for example the mean `water_temp` over the slick weighted by slick area. That is a small design decision to settle with Paola.
- **Unused fields:** `iteration_t` is now declared but unused, and `oil_in_sum_source_prev` is unused. Remove them in a later tidy-up commit, not in the merge.
- **The `froude` hook:** complete or remove it, depending on question 6.
- **Status document:** record the merge commit, the §6 decision, and the O1 result.
