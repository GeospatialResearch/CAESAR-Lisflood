# Water Temperature Module — Implementation Status

**Purpose of this document:** a snapshot of what has been built, verified, flagged as unverified/simplified, and what remains, so work can resume cleanly in a new session without re-deriving context. Read alongside `CAESAR-Lisflood_Water_Temperature_Module_Spec.md` (design spec) and `README.md` (branch overview) — this document tracks *implementation progress against that spec*, not the design itself.

**Previous revision (retained for context):** an extended verification session moved Step 19 from "compiles, not yet run" to "core physics runtime-validated," found and fixed several real bugs along the way, and identified a severe bug that was unresolved at the end of that session and blocked full-scheme trust. See `TEMP_V1_Session_Summary.md` for the full narrative of how each item below was established, including source cross-checks and hand derivations. That bug is now resolved; see the revision note immediately below.

**Revision note (this update):** this revision covers a long working session that closed the module's headline defect and then systematically validated what remained. In summary:

1. **The pond-test runaway is root-caused and fixed.** A mixing-weight error in `stage_tidal_input()` (§2). Found by static audit, not by further tracing. Neither energy balance scheme nor the advection routine was implicated; the advection routine has been positively cleared.
2. **A full formula re-review and energy-conservation audit** produced nine further findings, seven now fixed (§3b).
3. **Both schemes are now verified to machine precision** against independent reimplementations (§3d). The historical "0.22 to 0.60% residual" is explained and eliminated.
4. **The two-clock problem is identified and bounded** (§2, clock control). CAESAR's landscape-evolution time acceleration was silently mis-weighting surface heat exchange against advective mixing.
5. **An initial water depth raster has been added** (`WATERINIT_V1`), enabling pond and lake initial conditions, faster spin-up, and temperature runs with no water input at all.
6. **The floodplain forward-Euler instability has been reproduced deterministically** at predicted depths, with a new finding: it biases the time-mean temperature cold by up to 3 C, it does not merely add noise (§3, §3d).
7. **Bed heat exchange is promoted from "deferred by design" to a priority item** (§3e). Empirical evidence now shows the missing bed thermal inertia is the structural cause of the shallow-water failure mode, not a second-order refinement.

See §5 for the revised work order.

---

## 1. What's built and working (Steps 1–19)

| Step | What it added | Status |
|---|---|---|
| 1 | Field/array declarations (`water_temp`, met arrays, scheme flags, site constants, etc.) | Done |
| 2 | Array allocation in `initialise()`, zeroing in `zero_values()` | Done |
| 3 | Empty function stubs for all planned functions | Done (all now filled in) |
| 4 | Wired (initially inert) calls into `erodedepo()` at correct cadence points | Done, bug-fixed (see §2) |
| 5 | GUI: new "Water Quality" tab, master `isSimulateTemperature` checkbox | Done |
| 6–8 | Text met-file loading: air temp, shortwave, wind speed, humidity, cloud cover, pressure, dew point (`load_met_file()`, one GUI box + `load_data()` call per variable) | Done, tested working. **Gap-fill bug fixed this session** — see §2. |
| 9 | GUI: scheme selector (full/simplified radio buttons), HEC-RAS-default vs CAESAR-extension checkboxes (longwave, albedo), site parameters (lat/long/timezone/elevation/wind height), initial condition group box | Done |
| 9a | **New this session:** GUI group box for sensible/latent heat formulation selection (Option A/B/C radio buttons, Richardson-correction checkbox, bundled/separated coefficient textboxes) | Done, tested working |
| 10 | XML config save/load for all Water Quality tab settings | Done, tested working (round-trip confirmed). **Extended this session** for the new formulation-selection fields. |
| 11 | Initial water temperature: constant value or raster override, wired into `zero_values()`/`load_data()` | Done, tested working |
| 12 | `interpolate_met()` — linear time interpolation of a met variable for a given zone | Done |
| 13 | Simplified (equilibrium temperature) scheme — full implementation of HEC-RAS §2.2 (Eqs. 2.17–2.21), per-zone met caching, closed-form exponential update | Done. **Confirmed genuinely unconditionally stable** — this property is now the basis of the recommended fix for the full scheme's shallow-cell issue (§3). This scheme also exhibited the pond-test runaway, which is now explained: the root cause sits upstream of both schemes, in `stage_tidal_input()` (§2), so scheme choice was never relevant to it. Two smaller defects specific to this scheme were found by audit and are listed in §3b (missing nodata guard, raw rather than net shortwave). |
| — | Nodata handling fix: `water_temp` uses `-9999` sentinel for dry/unset cells rather than blanket-initialising everywhere | Done, tested working |
| — | Visualisation: "water temperature" added to Top Graphics II menu, `drawwater()` rendering (blue→white→red diverging scale, dynamically ranged) | Done, tested working |
| — | Advection/mixing: `update_water_temperature_advection()` — depth-weighted mixing via existing `dhdt_x`/`dhdt_y` tracer-style infrastructure, first-wetting rule (A2) | Done. A serious bug in this function's dependency on `water_depth_prev` was found and fixed (see §2). **This function has now been cleared as the cause of the pond-test runaway.** A static audit proved that the depth-weighted branch is an exact convex combination, and therefore unconditionally bounded by the minimum and maximum of the participating temperatures with no timestep or depth restriction, provided the mass-budget identity `water_depth == water_depth_prev + dhdt_sumIn + dhdt_sumOut` holds. That identity was verified to hold algebraically against `depth_update()`, and the call order confirms nothing writes `water_depth` between `save_temperature_states()` and `depth_update()`. The runaway was traced instead to `stage_tidal_input()` (§2). Remaining known gap: no diffusive or dispersive term, so two adjacent cells with zero net flux never exchange heat at all (§3b). |
| — | `save_temperature_states()` — dedicated previous-state snapshot function (split out from `save_tracer_states()` after a null-reference bug; see §2) | Done. **Extended this session** to also maintain `water_depth_prev` when the tracer subsystem is disabled — see §2. |
| — | Source-input wiring: rainfall, reach, and tidal inputs assign/mix a temperature into newly-added water (defaulting to `waterTempInitialValue` unless overridden — see next row) | `catchment_water_input_and_hydrology()` (rain) and `reach_water_and_sediment_input()` (reach/point) confirmed correct: both increment `water_depth` and blend with that same increment, so the mixing weights sum correctly. **`stage_tidal_input()` was found to be wrong and is the root cause of the pond-test runaway — see §2.** Fixed. All three now build the blend denominator from their own numerator terms rather than reading `water_depth[x,y]` independently. |
| 14 | Source temperature file: shared multi-column file for rain/tide/reach inputs, flexible header row (single index / `a-b` range / `a+b` combination / `ALL` keyword), gap-filling (interpolate internal gaps, hold flat at file ends), diagnostic message box reporting the full source-index-to-column mapping and any header warnings | Done, tested working (short-file "hold last value" bug found and fixed) |
| 15 | Solar geometry: `solar_altitude()`, `solar_geometry()` (extraterrestrial radiation `q_o`), `reflection_coefficient()` (`R_s`); new `simulationStartDateTime` GUI control anchoring `cycle` to a real calendar date/time | **Runtime-confirmed working this session** (previously "compiles, not yet run-time tested" — see §3 for how this was upgraded from flagged to confirmed) |
| 16 | Atmospheric longwave (`atmospheric_longwave_hecras()`, HEC-RAS default) and its CAESAR-extension alternative (`atmospheric_longwave_humidity()`); back longwave (`water_longwave()`, fixed, no alternative) | `atmospheric_longwave_hecras()` and `water_longwave()` **runtime-confirmed this session** via multi-day trace and cross-source constant checks (§3). Humidity-aware alternative not yet separately exercised. |
| 17 | `wind_speed_at_height()` (Step 13, reused), `richardson_number()`, `richardson_stability_function()`, `wind_function()`, `saturation_vapour_pressure()` (HEC-RAS Eq. 2.9, confirmed against source), `latent_heat_of_vaporisation()`, `air_density()` | **All confirmed correct this session, after finding and fixing real bugs in three of them — see §2 and §3.** `air_density()` rewritten to the mixing-ratio form. |
| 18 | `sensible_heat_flux()` (`q_h`), `latent_heat_flux()` (`q_l`, with an explicit sign-convention flip — see §3) | **Substantially reworked this session.** Both functions now dispatch between two formulations (literal HEC-RAS separated-term vs. CE-QUAL-W2-derived bundled form) — see §3 for the full architecture and reasoning. Both confirmed correct in isolation via cross-source validation and hand derivation. |
| 19 | Full energy balance assembly — fills in the `useSimplifiedTempScheme == false` branch of `update_water_temperature_energybalance()`, combining Steps 15/16/18 into `q_net`, per-zone caching of `q_atm` and (HEC-RAS-mode) `q_sw` | Compiled and **runtime-validated at two stable test points over multi-day runs** (residual against hand-calculated ΔT under 1%). The runaway-temperature bug previously recorded here as the blocker has been root-caused to `stage_tidal_input()` (§2) and is not a defect in this assembly. Audit findings affecting this branch specifically: the `q_atm` term may be missing a longwave reflection factor, and the forward-Euler integration remains a distinct, unresolved stability problem (§3, §3b). |

**Net result:** the simplified scheme's own equations are validated end-to-end for well-behaved, steady-inflow conditions. The full scheme's individual flux terms and their assembly are now genuinely runtime-verified, not just compiled, and several real bugs were found and fixed in the process. **The catastrophic pond-test failure that previously blocked both schemes is now root-caused and fixed in a single input routine (§2), and neither energy balance scheme nor the advection routine was implicated.** The remaining barrier to calling either scheme "done" is the forward-Euler stability problem in the full scheme (§3), plus the smaller audit items in §3b, none of which are blocking in the same sense.

---

## 2. Bugs found and fixed along the way (for reference — all resolved unless noted)

*Carried over from before this session:*

- **XML load try/catch variable-name collision** (`e` shadowing an outer-scope `e`) — fixed by renaming to `eTemp`.
- **Missing XML loaders triggering the outer catch silently** — fixed by adding the missing element reads; also added explicit debug info to the catch block for future diagnosability.
- **`NullReferenceException` in `save_tracer_states()`** when `isSimulateTemperature = true` but `isTraceWater = false` — fixed by splitting into `save_tracer_states()` (tracer-only) and a dedicated `save_temperature_states()`.
- **Coarse-cadence energy-balance scheduling bug**: `thermal_time` was originally nested inside the daily `creep_time2` block. Fixed by giving it its own independent, always-evaluated check.
- **Source-temperature-file "hold last value" bug**: gap-fill only held flat to the file's actual row count, not the full allocated array length. Fixed.
- **`nSources` not computed for temperature-only runs**: fixed with a small guarded block mirroring the existing `isTraceWater` logic.

*Found and fixed this session:*

- **`richardson_number()` missing leading `-2.0` factor** from HEC-RAS Eq. 2.13. Confirmed against the primary source text (both the equation itself and its separately stated sign convention: unstable = `ρ_air > ρ_sat` = `Ri` negative). Cross-checked against ClearWater-modules `tsm/processes.py` (whose `+2.0` was found to be internally inconsistent with its own branch-labelling logic — a bug in that codebase, not a valid alternate convention). Fixed to `-2.0`.
- **`richardson_stability_function()` stable-branch NaN**: `(1-34·Ri)^-0.8` goes complex for any `Ri > 1/34 ≈ 0.029`, inside its own stated valid domain (`0.01 ≤ Ri < 2`). Corrected to `(1+34·Ri)^-0.8`, confirmed via structural symmetry with the (correct) unstable branch, boundary continuity at `Ri=2`, and an exact match to ClearWater-modules. **Domain bounding added**: `Ri` is now clamped to `[-1.0, 2.0]` before piecewise evaluation, guaranteeing the stable branch's base stays positive.
- **`wind_function()` exponent bug**: `c` was applied to the whole sum `(a+b·windSpeed2)` rather than to `windSpeed2` alone. Invisible under the old `c=1` default (a no-op at that exponent), but would have caused an ~8x magnitude error once `c=2` (the new bundled default) was introduced. Fixed.
- **Cross-machine regression** (not a logic bug): the two Richardson fixes above briefly appeared reverted mid-session. Diagnosed as an unpushed-commit issue from switching development machines, not a code defect. Resolved; flagged in §6 as a recurring process risk given multi-machine development.
- **`load_met_file()` gap-fill bug**: no hold-last-value logic once a met file's rows ran out (unlike `load_source_temp_file()`, which already had this). Any met variable would silently sit at the C# array default of `0.0` beyond the file's actual length, with no warning. Fixed, mirroring the source-temperature file's existing gap-fill logic.
- **`water_depth_prev` tracer-coupling bug — the most serious bug found this session.** `water_depth_prev[x,y]` was only ever populated inside `save_tracer_states()`, meaning it remained permanently `0` whenever the tracer subsystem (`isTraceWater`) was disabled. Because `update_water_temperature_advection()` reads `water_depth_prev[x,y]` to compute `depth_after_outflows`, and that value was always `0`, the branch intended only for genuinely newly-emptied cells (`depth_after_outflows <= 0.0 || water_depth_prev[x,y] <= 0.0 || ...`) fired **unconditionally, every iteration, for every wet cell** — discarding each cell's own prior temperature entirely and replacing it purely with the inflow-weighted average `dhdt_sumInTemp`. The intended depth-weighted blending branch never executed at all under these conditions. **Fixed** by extending `save_temperature_states()` to also assign `water_depth_prev[x,y] = water_depth[x,y]` when `isTraceWater == false` (shared array, one active writer depending on the flag, not a duplicate array). Ordering relative to `erodedepo()`'s sequence (water inputs → save-states → `qroute()`/`depth_update()` → advection) confirmed correct. **This fix measurably improved but did not resolve the pond-test runaway bug described in §3 — that bug is still open.**
  - **Follow-up now complete**: the documentation comments at `save_tracer_states()` and `save_temperature_states()` noting the shared `water_depth_prev` dependency and explaining the guard are both present in the current file. This item is closed.

*Found and fixed in the audit pass following that session:*

- **`stage_tidal_input()` mixing-weight error. This is the root cause of the pond-test runaway bug and supersedes the "unresolved, blocking" entry previously carried in §3.**

  The base hydraulics in this function compute `dhdt = (input - elev[x, y])` and then **overwrite** `water_depth[x, y] = dhdt`. Despite its name, `dhdt` here holds the *total new depth*, not an increment. (The base code flags this line itself with `CHECK THIS IS RIGHT`.) This is unlike `reach_water_and_sediment_input()` and `catchment_water_input_and_hydrology()`, where the equivalent variable genuinely is an increment and `water_depth` is incremented rather than assigned.

  The TEMP_V1 temperature block copied the neighbouring `watertracer` blending idiom verbatim and blended with `dhdt`:

  `water_temp[x,y] = ((water_temp[x,y] * prev_depth) + (sourceTemp_tidal * dhdt)) / water_depth[x,y];`

  Because `water_depth[x,y] == dhdt` by construction at that point, the two mixing weights sum to `prev_depth + water_depth` rather than `water_depth`. The update is therefore **not a convex combination**. It reduces algebraically to an affine map applied once per hydraulic iteration:

  `T_new = T_src + T_old * (prev_depth / water_depth)`

  In the pond test the stage input cell holds 8 m of water at a constant level, so once the initialisation sloshing damps out `prev_depth == water_depth` exactly and the multiplier is exactly 1. The map becomes `T_new = T_src + T_old`: **unbounded arithmetic growth at one source temperature (21 C) per hydraulic iteration, with no fixed point and no saturation.** Reaching 100,000 C takes roughly 4,760 iterations.

  This accounts for every recorded symptom:
  - Over 100,000 C observed "at deep (over 8 m) locations": the 8 m cell *is* the stage input cell. It is the heat source, not a victim of it.
  - Failure in **both** schemes: the fault is in an input routine, upstream of and independent of both energy balance branches.
  - Deep cells as badly affected as shallow, delayed by a few cycles: heat originates at the deepest cell in the domain and spreads by advection. Depth-dependent heat capacity is irrelevant to the mechanism.
  - Corruption at the **same** model cycle (`1320.008316` min) in two separate runs tracing different cells: **there is no triggering event.** With the pond at a uniform level there is almost no flux, so the input cell cooks in isolation while zero-flux neighbours correctly hold their temperature. Only the residual friction-decayed `qx` trickle advects heat outward, carrying `T_donor * dhdt_sumIn / depth`, which is negligible per iteration until `T_donor` is itself enormous. A traced neighbour therefore shows nothing for many hours and then appears to corrupt abruptly, at a time fixed entirely by deterministic dynamics. The correct inference from "identical time in two runs" is *deterministic dynamics*, not *shared external trigger*, since a deterministic model reproduces everything at the same cycle.
  - Unaffected by the `water_depth_prev` fix, and not an input file running out of rows: both correctly ruled out last session.
  - Missed by the earlier 5-day validation: that run used **reach** inputs, where the increment idiom is correct. The pond test was the first to exercise a stage/tidal input, the only one of the three input paths that overwrites depth.

  **Fix applied**: the temperature block now derives the added depth as `added_depth = water_depth[x,y] - prev_depth`, blends with that, skips the update entirely when `added_depth <= 0` (a steady or falling stage adds no water, and temperature is intensive so removing water changes nothing), and builds the denominator as `(prev_depth + added_depth)` rather than reading `water_depth[x,y]` independently, so the weight-sum identity is algebraic and cannot be broken by a future change to the hydraulics line above. The base hydraulics line itself was deliberately **not** changed: the `CHECK THIS IS RIGHT` quirk is load-bearing for the `watertracer` logic immediately below it.

  **Same defensive change applied to the other two input routines** even though both were already correct, because both computed the denominator by reading `water_depth[x,y]` rather than from their own terms and were therefore one hydraulics edit away from the identical failure: `reach_water_and_sediment_input()` now divides by `(prev_depth + dhdt)`, and `catchment_water_input_and_hydrology()` by `(prev_depth + water_add_amt)`.

  **Standing lesson worth carrying forward**: when copying an idiom from code that carries a caveat comment, the caveat is a **precondition to satisfy**, not a quirk to preserve. The TEMP_V1 block inherited the base code's `CHECK THIS IS RIGHT` warning as a descriptive note and proceeded.

---

*Also implemented and verified this session (details of each in §3b unless noted):*

- **`air_density()` divide-by-zero guard** restored. Vapour pressure now clamped to 99% of total pressure.
- **Simplified scheme now uses net shortwave**, via a new shared `net_shortwave(x, y, shortwaveIn, Rs)` helper called by **both** schemes. Solar geometry (`get_day_and_hour`, `solar_altitude`, `reflection_coefficient`) was hoisted out of the full-scheme branch so `Rs` is available to both. This also removed a pre-existing defect in the full-scheme debug trace, which logged raw incoming shortwave rather than the absorbed value whenever `useHecRasAlbedo` was false, so the logged `q_net` did not match the applied `q_net`.
- **Simplified scheme `-9999` nodata guard** added, matching the full scheme.
- **`K_T` guard** changed from `0.0001` to a floor of `5.0 W/m2/K`. The old epsilon would have produced `T_eq = T_d + 10^4·q_sw` if it ever fired.
- **`diffusivityRatio` removed from the bundled sensible-heat term** and scoped by comment to the separated Eq. 2.10 form only. It has no GUI control and is always 1.0, so this was a no-op numerically, but it removed a latent double-count.
- **Thermal scheduler now uses actual elapsed time.** `thermalStepMinutes = cycle - lastThermalCycle` replaces the nominal `thermal_update_interval`, and `thermal_time` catches up with a `while` loop rather than a single increment. **This mattered more than expected**: with a fixed 3600 s, single-step errors during the filling transient reached +13.1%, -30.4% and +13.1% at cycles 137.6, 183.6 and 252.6 min. This is the most likely explanation for the 4.7% single-row residual previously recorded at "a rapidly-filling shallow cell", which had been attributed to depth sampling. Treat that anomaly as resolved.

- **Model clock control when temperature is active (the "two-clock problem").** CAESAR runs two clocks: `time_factor` (the model clock, `cycle += time_factor/60`, ratcheted up 1.5x per call inside `erode()` up to `max_time_step`) and `local_time_factor` (the step actually integrated by `qroute()`/`depth_update()`, capped at the Courant limit). The gap between them is CAESAR's landscape-evolution acceleration.

  That acceleration is valid for geomorphology, which does nothing while water is still. **It is not valid for temperature**: `update_water_temperature_advection()` moves heat using `dhdt_*` built from `local_time_factor`, while `update_water_temperature_energybalance()` applies surface fluxes over `cycle`-derived elapsed time, and `interpolate_met()` / `get_day_and_hour()` also run on `cycle`. Any gap silently mis-weights surface forcing against advective mixing by exactly the acceleration factor. Measured gap in a typical configuration: `time_factor` at 3600 s against `local_time_factor` at 0.5 s, a factor of about 7000.

  The problem was also masked. The only brake on the ratchet is the `input_output_difference > in_out_difference` test, and `waterinput` is written **only** by `catchment_water_input_and_hydrology()` and `reach_water_and_sediment_input()`. `stage_tidal_input()` never touches it. So a run with no input, or with a stage-only input and a closed domain, has `input_output_difference` exactly 0.0 and the brake never engages at all.

  **Fix**: a bounds block inserted in `erodedepo()` after the `input_output_difference` test and before `local_time_factor = time_factor;`, active only when `isSimulateTemperature == true`:
  - **(a)** the clock may never outrun the requested thermal cadence: `time_factor` capped at `thermal_update_interval * 60`. Without this, `thermal_update_interval` is silently overridden by `max_time_step` and the energy balance simply fires once per iteration at whatever the clock says. Verified: setting a 15-minute interval now actually produces 15-minute steps (489 iterations against 133 for 60 minutes over the same 5 days).
  - **(b)** the clock may only run ahead of the hydraulic step while the water is demonstrably at rest. A new `maxflux` reduction in `depth_update()` (same pattern as the existing `maxdepth` reduction) tracks the largest `|qx|` or `|qy|` in the domain. If `maxflux * time_factor / DX` exceeds 1e-6 m, `time_factor` is pinned to the Courant limit.

  This preserves the acceleration where it is safe. A still-water 5-day run completes in 133 iterations rather than roughly 812,000. The 1e-6 m threshold is empirically well placed: a verified still-water test showed residual floating-point-level flux of order 1e-17 m2/s, nine orders below the criterion, correctly ignored.

  **Note for the record:** `stage_tidal_input()` not updating `waterinput` is a base-model gap that makes the timestep brake blind to stage-driven inflow. Deliberately **not** fixed, because it would change the timestep sequence and therefore every result in every stage-driven run. Logged as a separate decision.

- **`WATERINIT_V1`: initial water depth raster.** New "Initial depth file" control on the Files tab, loaded in `load_data()` after the grain index file and before the initial temperature raster block, with XML save/load as a named `InitialDepthFile` element (named rather than a positional `Filenames` triple, so pre-existing config files still load). Values are depths in metres; zero, negative and -9999 are treated as "no initial water". The loader also primes `maxdepth` and calls `scan_area()` so the first iteration sees the water, since `maxdepth` is otherwise only recomputed inside `depth_update()` and `scan_area()` does not run until `counter` reaches 5. The two hardcoded `TEMP_V1 DEBUG TESTING` depth-seeding lines after `load_data()` have been removed.

  **Bug this exposed and fixed:** the Step 11 loop that assigns `waterTempInitialValue` to initially-wet cells carried the comment "water_depth already loaded above". `water_depth` was never loaded from a file anywhere in `load_data()`, so every cell was dry at load time and that loop had **always been dead code**. Worse, it sat inside `if (TempTab_textBox_initialraster.Text != "null")`, so a pond loaded from the new depth raster with no temperature raster would have started with every cell at `-9999` and stayed there permanently (the advection function preserves the sentinel with no inflow, and both schemes skip nodata cells). The loop has been hoisted out one level so it runs whenever `isSimulateTemperature` is true, and its depth test relaxed from `> water_depth_erosion_threshold` to `> 0.0` so that any cell starting with water starts with a temperature.

---

## 3. Explicitly flagged / unverified items (accuracy caveats)

*Resolved this session (moved out of "unverified" — kept here for the record):*

- **`solar_altitude()` / `reflection_coefficient()`** — previously flagged as "compiles, not confirmed as a bit-for-bit match to HEC-RAS." **Now runtime-confirmed**: a 5-day trace at two test points showed a correctly repeating diurnal on/off pattern with sunrise/sunset at consistent times, and — more decisively — a slow, monotonic decline in each day's shortwave peak across five consecutive days starting exactly at the summer solstice, which is precisely the signature of correct day-of-year tracking and could not be produced by a bug. Not yet confirmed as a literal equation-for-equation match to HEC-RAS's own derivation, but confirmed physically correct.
- **`saturation_vapour_pressure()` (HEC-RAS Eq. 2.9)** — remains confirmed (verified in a prior session via source PDF extraction and a numerical sanity check). Reconfirmed indirectly this session via its use in the now-validated `water_longwave()`/`richardson_number()` chain.
- **`latent_heat_flux()` sign convention** — the deliberate "positive = gain" convention across all flux terms is confirmed correct and consistent with the validated `q_net = q_sw + q_atm − q_b + q_h + q_l` assembly (note: **this differs from the raw HEC-RAS Eq. 2.1 sign convention**, which is `q_net = q_sw + q_atm − q_b − q_h − q_l`; CAESAR's implementation deliberately negates `q_h`/`q_l` internally so every flux function returns "positive = gain," and the assembly line reflects that consistently — this is documented behaviour, not a discrepancy).
- **`sensible_heat_flux()` fixed-pressure simplification** — resolved as part of the wind-function rework below: the separated-term option now takes `pressureMb` as a parameter and uses the site's actual pressure, matching `latent_heat_flux()`'s existing behaviour.

*New architecture this session — sensible/latent heat formulation and wind function:*

The single biggest piece of work this session. HEC-RAS's own source (Zhang & Johnson 2016) states its wind-function coefficients `a`, `b`, `c` (Eq. 2.11) as explicitly **user-defined**, with no default given (order 1e-6 for `a`/`b`). Cross-checking against CE-QUAL-W2 v4.5 source (`heat-exchange.f90`/`temperature.F90`) found a very different-looking coefficient set (`AFW=9.2, BFW=0.46, CFW=2`, confirmed identical across two independent real CE-QUAL-W2 applications) used in a **bundled** formulation — no separate density/specific-heat/latent-heat multiplication, confirmed to output W/m² directly by tracing CE-QUAL-W2's own flux assembly through to its final temperature update with no unit conversion factor present. These same `9.2/0.46/2` values are already present, independently, in CAESAR's own already-validated simplified scheme (Eq. 2.19) — a strong internal cross-check. Plugging `9.2/0.46/2` into the *literal separated-term* equation structure overshoots physically plausible flux by roughly 1000x, confirming the two coefficient sets and the two equation structures are genuinely not interchangeable.

**Resolution: three selectable formulations**, GUI-exposed (`useBundledSensibleLatent`, `useRichardsonStabilityCorrection`, separate coefficient sets `windFunc_*_bundled`/`windFunc_*_separated`), XML save/load complete:

- **Option A** — literal HEC-RAS Eq. 2.10/2.11, explicit `ρ_s·Cp_air·L` terms, `windFunc_*_separated` (order 1e-6, the source's own stated order, no worked example available to confirm against). Not the default. Retained for textual fidelity to the printed equation, with the caveat above documented at the point of use.
- **Option B** — CE-QUAL-W2 bundled form, `9.2/0.46/2`, Richardson stability correction **off** (matching CE-QUAL-W2's own behaviour exactly, which was confirmed via source inspection to apply no such correction to its wind function).
- **Option C (default)** — same bundled form and coefficients as B, Richardson correction **on**, as a deliberate CAESAR extension. Not independently validated as a *combination* in either source, but `f(Ri) ≈ 1` near neutral conditions, so it matches B's validated behaviour in the common case and only diverges under strong stability/instability, using Richardson physics independently confirmed elsewhere in this module.

All three options, plus the underlying `wind_function()`/`richardson_number()`/`richardson_stability_function()` functions, were cross-validated numerically in the Immediate Window across multiple test pairs, with signs and magnitudes matching physical expectation and, in several cases, matching independent hand derivation to 3+ significant figures (including a strong-stability test case where the Richardson suppression factor matched a hand calculation of `(1+34·Ri)^-0.8` almost exactly).

*Still open / unresolved:*

- **RESOLVED (was: UNRESOLVED, BLOCKING): severe runaway temperature bug.** Root-caused to a mixing-weight error in `stage_tidal_input()` and fixed. Full account in §2. Retained here as a pointer only, since this was the headline open item in the previous revision. Note for the record that the "shared global trigger at a fixed cycle time" reading of the evidence was wrong: the mechanism is a deterministic ramp with a long time constant, not an event, and a deterministic model reproduces a ramp at the same cycle in every run.

- **Floodplain forward-Euler / no-subcycling issue. Now confirmed to be a genuinely DISTINCT problem, not a symptom of the runaway above.** Floodplain cells were observed oscillating between under 5°C and over 18°C within hours. Root cause: `update_water_temperature_energybalance()`'s full scheme uses a plain explicit forward-Euler step over the full `thermal_update_interval`, with a floor at 0°C but no ceiling and no subcycling. At shallow depths (~0.1–0.5 m), a single hour of ordinary diurnal forcing produces a 1–2.5°C step; several such steps accumulate into 20°C+ swings across a half-day, and the floor clamp produces the exact-zero readings observed.

  **The open question from last session (is this its own issue, or the same mechanism?) is now answered.** The audit pass simulated the exact discretisation the code implements (the real flux formulas, a diurnal forcing cycle, the 0°C floor) across a range of depths. Results: at 0.5 m the scheme is well behaved, swinging roughly 12 to 19°C. At 0.1 m it swings roughly 6 to 31°C, at 0.05 m roughly 2 to 36°C, and at 0.01 m it becomes an erratic sawtooth repeatedly hitting the 0°C floor. This reproduces the observed floodplain behaviour closely and independently of the stage-input bug, so the fix is still required. Two further conclusions from the same simulation:
  - The scheme is **oscillatory but ultimately self-limiting**, because `q_b` scales as `T^4` and the latent term scales with the saturation vapour pressure, giving a strong negative feedback. It reached about 37°C at worst. **It is therefore not capable of producing the 10^5 °C runaway on its own**, which is what first made the two problems separable.
  - The instability threshold is `dt > 2·ρ·Cp·h / K` where `K = -dq_net/dT`. **Superseded figures**: that first estimate used `K` = 20 to 25 W/m2/K at 2 m/s wind. Measured at 4 m/s in the configuration actually tested, `K` = 52.4 W/m2/K at equilibrium, giving a critical depth of **0.0225 m** at a 60-minute interval and 0.0056 m at 15 minutes. Across 1 to 15 m/s wind the critical depth ranges 0.017 to 0.062 m, so with a 0.01 m erosion threshold there is always an unstable band immediately above the threshold, and it widens with wind. See §3 above for the further finding that `K` varies by a factor of ten across a large excursion, so the *local* critical depth at the hot end of a swing is higher still.

  **Now reproduced deterministically and quantitatively (see §3d for the full ladder test).** A stepped-bed test domain with a level water surface and depths from 5 m down to 0.005 m produced instability at exactly the predicted depths. At a 60-minute interval the 0.015 m and 0.012 m columns ended at **numerically exact 0 C**. Step-by-step audit of the 0.012 m column showed a sawtooth between roughly 2 and 22.5 C, with every individual flux physically sensible and no Richardson clamp reached. The zeros are the `if (T_new < 0) T_new = 0;` floor catching a downswing: the floor clips to exactly zero regardless of how far below zero the step went, which is why two independently oscillating cells read identically 0.0 on the same step. Re-running at a 15-minute interval eliminated the zeros entirely and both columns landed within 0.2 C of the instantaneous equilibrium.

  **New finding: the instability biases the mean, it does not merely add noise.** Final-day means across the ladder: 14.97 C at 0.5 m, 14.55 at 0.05 m, 13.40 at 0.015 m, 11.76 at 0.012 m. That is a progressive **cold bias of up to 3.2 C** which survives any time-averaging a user might apply. The mechanism is Jensen's inequality: the loss terms are convex in temperature (back radiation as `T^4`, evaporation following the near-exponential saturation vapour pressure), so `q_net(T)` is concave and the average net flux under an oscillating temperature is below the flux at the average temperature. The system must therefore settle below the true equilibrium. This reclassifies the issue from "ugly artefact" to "quantitative error in the reported mean".

  **Second new finding: `K` is strongly temperature-dependent, which makes the instability self-amplifying.** Measured across the excursion: 6.4 W/m2/K at 0 C, 12.2 at 9 C, 25.9 at 14 C, 67.7 at 22.6 C. A factor of ten, driven by the `T^4` term and more strongly by the Richardson stability function (hot water under cooler air gives unstable stratification, `f(Ri)` = 1.56 and `FW` = 25.9; cold water gives stable stratification, `f(Ri)` = 0.36 and `FW` = 6.0, a 4.3x difference in turbulent exchange). So the local critical depth at the hot end of a swing is 0.029 m rather than the 0.0225 m implied by the equilibrium value: once a cell overshoots hot it becomes *more* unstable. This is also the concrete mechanism behind the cold bias.

  **Fix, specified and still not implemented**: reformulate the full scheme's per-step update as an exponential relaxation toward a local equilibrium temperature, the same unconditionally-stable form already proven in CAESAR's own simplified scheme (HEC-RAS Eqs. 2.17 to 2.21), rather than raw forward Euler with an ad hoc floor:

  ```
  K     = -dq_net/dT  evaluated at T_w
  T_eq  = T_w + q_net(T_w)/K
  T_new = T_eq + (T_w - T_eq) * exp(-K*dt/(rho*Cp*h))
  ```

  Bounded between `T_w` and `T_eq` for any `dt`, so no stability limit and no depth below which the scheme misbehaves. Reduces exactly to forward Euler as `dt -> 0`. **Use a numerical derivative for `K`**, `-(q_net(T_w+0.25) - q_net(T_w-0.25))/0.5`, rather than an analytic one: `FW` depends on `T_w` through the Richardson number and that dependence is the factor-of-ten variation above, which the numerical form captures for free. Two extra flux-stack evaluations per cell per thermal step, negligible at an hourly cadence. Keep the 0 C floor, but it should then almost never fire; if it does it indicates `T_eq` below zero, i.e. genuine freezing conditions rather than a numerical artefact.

  **Acceptance criterion (exact, from §3d):** after the change, the **60-minute** run of the stepped-bed ladder must reproduce the 15-minute numbers recorded in §3d to within a few hundredths of a degree.
- **Missing-humidity/cloud-cover/pressure fallback values are still hardcoded** (70% RH, 0 cloud fraction, 1013.25 mb) rather than GUI-configurable constants, as originally flagged. **Not yet addressed** — a concrete fix (three new fields, GUI textboxes, XML save/load, anchor points) was drafted at the start of this session but implementation was superseded by the physics-verification work and has not been confirmed done.
- **`longwaveCloudExponent`** (Step 16): still using the defaulted-to-squared (`n=2`) assumption for the cloud-cover exponent in the atmospheric longwave formula, per the original ambiguity note. Not revisited this session.
- Humidity-aware atmospheric longwave alternative (`atmospheric_longwave_humidity()`) — not separately exercised or validated this session; only the HEC-RAS-default path was tested.
- ~~Source-input temperature assignment for the constant water-level ("stage") input mechanism specifically has not been separately confirmed.~~ **Resolved: this was the root cause. See §2.** Recording the outcome here because the hypothesis was correctly identified and logged last session but not followed up; it was the right lead.

---

## 3b. Formula re-review and energy-conservation audit (new this revision)

A dedicated pass over every thermodynamic function and over the module's energy budget as a whole. Separate from §3 because these are audit findings rather than items carried from implementation.

### Confirmed correct, no action needed

- `water_longwave()`: `0.97·σ·Tw^4`. Matches HEC-RAS Eq. 2.7. Emissivity and constant both standard.
- `saturation_vapour_pressure()`: the Kelvin-form Lowe polynomial was re-evaluated independently at several temperatures. 6.104 mb at 0°C, 12.26 at 10°C, 23.36 at 20°C, 42.41 at 30°C. Agrees with published saturation vapour pressure to better than 0.1% across the normal range. Confirmed good. (Note it is a 6th-order polynomial fitted over roughly -50 to +50°C and produces increasingly meaningless values outside that, which matters only when `T_w` is already corrupt.)
- `solar_altitude()`, `solar_geometry()`: Spencer declination series, equation of time, and Earth-sun distance correction all correctly transcribed.
- `atmospheric_longwave_humidity()`: Idso (1981) clear-sky emissivity correctly transcribed.
- `richardson_number()`, `richardson_stability_function()`, `wind_function()`: consistent with the corrections recorded in §2 last session.
- **`update_water_temperature_advection()`: proved sound.** See the §1 table entry. The depth-weighted branch is an exact convex combination given the mass-budget identity, and that identity was verified algebraically and by call-order inspection.

### Findings, in descending severity

1. **`air_density()` is missing its divide-by-zero guard. Fixed.** The mixing ratio is `0.622·e/(P − e)`. The session summary records this function as having been ported from ClearWater-modules "including the `mixing_ratio_air()` helper, with a divide-by-zero guard for `P == e`", but that guard was not present in the C# version. It matters because `richardson_number()` calls `air_density(pressureMb, es_water, Tw_K)`, and `es_water` exceeds 1013 mb once `T_w` passes about 100°C, at which point the denominator goes negative and `rho_sat`, `Ri`, the stability function and both turbulent flux terms all become meaningless. This is a **secondary amplifier**: it only engages once something else has already driven a cell past 100°C, as the §2 bug did. Guarded by clamping the vapour pressure to 99% of total pressure.

2. **Simplified scheme applies no albedo to shortwave. Fixed.** The `useSimplifiedTempScheme` branch used raw `shortwave_zone[zone]` in `T_eq = T_d + q_sw/K_T`, while the full scheme applies either the HEC-RAS `R_s` or the CAESAR albedo extension. HEC-RAS Eq. 2.21 takes **net** shortwave. The simplified scheme therefore overestimated absorbed shortwave by roughly 3 to 8%, and the two schemes were not directly comparable to each other. Preferred fix (recorded here because it has a refactor implication): extract the full scheme's existing `q_sw` derivation into a shared helper called from both branches, which removes the whole class of divergence rather than patching one instance. Re-verify the full scheme's 1% hand-calculation match afterwards.

3. **Simplified scheme was missing the `-9999` nodata guard. Fixed.** The full scheme tests `water_depth > threshold && water_temp != -9999`; the simplified scheme tested depth only. A cell can hold water above threshold and still carry the sentinel, because the first branch of `update_water_temperature_advection()` explicitly preserves it when `dhdt_sumIn == 0.0 && water_temp_prev == -9999`. Traced through the arithmetic: with `T_w = -9999` the scheme drives `K_T` to order 1e5, collapses the decay term to zero, and silently snaps the cell to the dewpoint. It self-heals rather than diverging, so this was never the runaway, but it converts a nodata sentinel into a physical temperature with no warning.

4. **`if (K_T <= 0) K_T = 0.0001;` was a dangerous guard. Fixed.** If it ever fires, `T_eq = T_d + q_sw/1e-4`, which for 200 W/m² is 2,000,000°C. Eq. 2.18's own physical minimum is about 11.6 W/m²/K at `T_w = 0°C` (since `beta` bottoms out at about 0.30 and `f(u_w7)` has a floor of 9.2), so the guard can only engage on an already-corrupted `T_w`. Replaced with a floor of 5.0 W/m²/K, which sits below the formula's own minimum and therefore never engages in normal operation.

5. **`atmospheric_longwave_hecras()` may be missing the longwave reflection coefficient. FLAGGED, NOT CHANGED.** The commonly stated form is `q_atm = 0.937e-5·σ·Ta^6·(1 + 0.17·C^2)·(1 − R_L)` with `R_L` about 0.03. The code has no `(1 − R_L)` factor. On a term of order 300 W/m² that is a permanent bias of about +9 W/m² into the water. **Deliberately not changed pending a direct read of ERDC/EL TR-16-1 Eq. 2.6**, ideally in the same pass that resolves the existing `longwaveCloudExponent` ambiguity, since both live in that one equation. Per project rules this is an AI-sourced figure and is not to be adopted without primary-source confirmation.

6. **Thermal scheduler applies a nominal interval of flux over an actual interval that may differ. Fixed.** `if (cycle > thermal_time) { update(); thermal_time += thermal_update_interval; }` used a fixed `dt_seconds = thermal_update_interval * 60` regardless of elapsed model time. If `cycle` ever advances by more than one interval in a single hydraulic iteration, `thermal_time` falls behind and the scheme fires on consecutive iterations, each applying a full interval of flux over a fraction of one. Fixed by tracking the actual elapsed time (`thermalStepMinutes = cycle - lastThermalCycle`) and by catching `thermal_time` up with a `while` loop rather than a single increment. Numerically negligible in normal running (the actual step is 60.00x minutes) but it closes a latent energy-creation path. **Re-verify the hand-calculation trace after this change**, since it alters the denominator the 1% match was measured against.

7. **`diffusivityRatio` is applied inconsistently between the two bundled terms. Fixed.** `sensible_heat_flux_bundled()` returned `diffusivityRatio · FW · 0.47 · (Ta − Tw)` while `latent_heat_flux_bundled()` applied no such factor. In CE-QUAL-W2 the Bowen constant 0.47 already embeds the thermal-to-vapour diffusivity ratio, so multiplying again double counts. The default `diffusivityRatio = 1.0` made this inert, but it meant the documented claim that Option B is an exact CE-QUAL-W2 match held only at the default, and the two bundled terms responded inconsistently if the value was changed. Factor removed from the bundled sensible term, GUI control disabled when the bundled option is selected (same pattern as the existing `TempTab_checkBox_richardsoncorrection.Enabled` line).

8. **`reflection_coefficient()`'s 0.03 floor is dead code. Noted, not changed.** `1.18·alt^-0.77` reaches 0.035 at 90° solar altitude, so the effective minimum is 0.035 and the stated floor never binds. Harmless, but the comment describes behaviour the code does not have. Related minor risk in the same function: `if (altitudeDeg <= 0) return 1.0` zeroes `q_sw` whenever the computed solar altitude is negative, so a misconfigured `siteTimeZone` or `siteLongitude` would silently discard real measured radiation near sunrise and sunset.

9. **Anderson's clear-sky albedo curve is being applied to measured global shortwave. Noted, not changed.** HEC-RAS applies `R_s` to computed clear-sky `q_o` (Eq. 2.5 feeding Eq. 2.4). Applying a clear-sky-fitted reflectivity curve to a pyranometer series that already contains cloud attenuation is a mild inconsistency rather than an error, but it should carry a comment so a future reader does not assume HEC-RAS equivalence.

### Energy-conservation findings

The module's conservation properties reduce to one invariant. The advection update is exactly conservative **if and only if** `water_depth == water_depth_prev + dhdt_sumIn + dhdt_sumOut` holds per cell. When it does, the update is a convex combination and is unconditionally bounded. When it does not, the temperature is multiplied by (implied mass)/(actual depth) once per hydraulic iteration, which at tens of thousands of iterations per day is the only mechanism in the module capable of reaching 10^5 °C. The §2 bug is exactly a violation of this invariant, at one cell.

Remaining conservation violations, ranked, after the §2 fix:

1. **`if (T_new < 0) T_new = 0;` in both schemes** is a one-way energy source with no matching ceiling. Every time a cell would cool below 0°C, energy is created from nothing and advection then distributes it. Bounded in magnitude (see §3, where simulation shows the full scheme self-limits around 37°C) but a systematic positive bias in exactly the shallow cells where the discretisation is already marginal. Tied to the deferred-ice decision (§4), so not independently fixable, but should be documented as a known bias rather than a neutral clamp.

2. **`if (depth < water_depth_erosion_threshold) depth = water_depth_erosion_threshold;`** in both schemes divides `q_net·dt` by a heat capacity larger than the cell actually has, which destroys energy relative to the true thin film. A necessary stability hack, but it means the module has no closed energy budget at shallow depths.

3. **No heat-out accounting anywhere.** The main loop tallies removed water into `waterOut` but there is no corresponding heat tally, so a global heat budget cannot currently be closed even in principle. Worth adding a `heatOut` accumulator alongside it: the single most useful diagnostic for this class of bug, and the one whose absence let the §2 bug run undetected. See §3c.

4. **No diffusive or dispersive heat exchange.** Heat moves only with advective mass flux, so two adjacent cells with zero net flux never exchange heat at all. A design gap rather than a bug, but it removes the main physical damping mechanism that would otherwise smooth the shallow-cell oscillations, and it is worth considering alongside the deferred bed heat exchange term (§4), which is the other missing damping term.

5. **Evaporative mass loss carries no thermal effect** (already recorded in §4 as decoupled by design, A13a). Restated here because it is a genuine one-way energy inconsistency, not merely a missing feature.

### Test-design note

The pond test domain is genuinely closed: its edge cells sit above the maximum water level, so they stay dry and the main loop's unconditional edge-drain block (which clamps `water_depth` at `x=1`, `x=xmax`, `y=1`, `y=ymax` to `water_depth_erosion_threshold`) never fires. Worth knowing for **other** test designs, though: that block is not conditional on any "closed boundary" setting, so a small domain whose edge cells do get wet will leak at every edge regardless of intent, and in a 7x7 domain 24 of 49 cells are edge cells.

---

## 3c. Why this class of bug evaded the verification method (process note)

Recorded because the same blind spot will recur on the water-quality and pollution modules that TEMP_V1 is foundational for.

1. **The verification method was formula-shaped; the bug was invariant-shaped.** Every function checked in the Immediate Window is a pure function of its arguments, hence hand-verifiable in isolation, and all of them were verified correctly. The temperature *update* is not a pure function: its correctness depends on a relationship between arrays written by different routines at different points in the iteration (`water_depth_prev` from `save_temperature_states()`, `dhdt_*` from `depth_update()`, `water_depth` from whichever routine touched it last). No amount of per-function testing can observe that relationship. Skeleton-first, verify-in-isolation remains the right method for the physics and is structurally blind to this fault class.

2. **Temperature is an intensive variable carried by idioms written for constrained ones.** All three input-routine temperature blocks were copied from the adjacent `watertracer` logic. A water-source tracer is a *proportion* subject to a normalisation constraint, and a weight-sum error there shows up as `tracersum` drifting from 1.0 (there is a commented-out assertion for exactly that in `update_tracer_states()`). The solute tracer hit the same failure mode once already and carries the fix comment "Reworked algorithm due to very large values accumulating, caused by division by very small depths." Temperature has no normalisation constraint and no assertion, so the identical arithmetic error violates nothing the codebase knows how to check. **The idiom was transplanted; the safety property that made it correct was not.**

3. **The property lost was convexity, and losing it is invisible over short horizons.** `T_new = (w1·T_old + w2·T_in)/h` is unconditionally bounded if and only if `w1 + w2 == h`. Fetching the denominator independently of the numerator's terms turns that identity from algebra into an assumption about program state. The result is an affine map with a multiplier near 1, which is indistinguishable from a correct one over the handful of steps that hand-verification checks and diverges over thousands.

4. **The sharp onset time actively misled the search.** "Two independent runs corrupt at the same cycle" reads as strong evidence for a shared external trigger, which is why the file-length and flood-front hypotheses were the natural next moves. But deterministic dynamics reproduce everything at the same cycle. An affine recurrence with a long time constant produces a sharp, reproducible, cell-independent onset with no event at all.

5. **The debug instrumentation sat downstream of the fault and was therefore self-confirming.** The trace logs `q_sw, q_atm, q_b, q_h, q_l, q_net, Ri, T_w, T_w_before`, and every flux term is computed *from* `T_w`. With `T_w` corrupted upstream the trace stays perfectly internally consistent and `dT = q_net·dt/(ρ·Cp·h)` keeps matching hand calculation to within 1%, which is exactly what was observed and exactly why it was reassuring. **A trace that recomputes its own inputs from the corrupted state cannot detect corruption of that state.** The instrumentation that would have caught this is a conservation residual, not a flux recomputation.

**Concrete practice change**: add cheap, permanent conservation residuals rather than richer flux traces. Two candidates, both one line each: the per-cell advection residual `water_depth - (water_depth_prev + dhdt_sumIn + dhdt_sumOut)`, and a per-input-routine residual `water_depth - (prev_depth + added_depth)`. The second one, had it existed, would have printed `-8.0` on the first iteration of the pond test.

---

## 3d. Verification results (new this revision)

Every result below comes from an **independent reimplementation** of the relevant scheme, written from the C# source and run against the model's own output. This is the project's established validation strategy and it now covers both schemes end to end.

### Full energy balance: exact

Four debug traces (shallow and deep cells, `useHecRasAlbedo` true and false) were checked by recomputing `q_net` at `T_w_before` for every row and comparing `q_net·dt/(ρ·Cp·h)` against the actual `T_w - T_w_before`.

**Agreement is exact to machine precision, below 0.01 ppm, on every row of every file.** Supporting checks: `q_atm` matches to all 15 significant figures; backing the wind speed out of `q_h` and `Ri` independently on every row returns 4.00000 m/s with zero spread.

### Simplified scheme: exact

The relaxation identity `T_w = T_eq + (T_w_before - T_eq)·decay` holds to a maximum error of **9.9e-14 C** across both traced cells.

### The historical 0.22 to 0.60% residual is explained and eliminated

Both exactness results required recomputing the diagnostics at `T_w_before`, not at the post-update `water_temp`. The debug blocks originally derived their fluxes from the post-update value, which for the full scheme perturbs `q_net` slightly and for the simplified scheme prevents the identity closing at all (max error 6.0e-2 C rather than 1e-13).

**This is almost certainly the entire explanation for the residuals recorded in earlier sessions.** They were not physics error and not depth sampling. They were the trace evaluating its own fluxes one step too late. All three debug blocks now derive their intermediates from `T_w_before` and log the post-update temperature separately, which makes every trace self-checking in a spreadsheet with no reimplementation required. This is the practical form of the §3c lesson about self-confirming instrumentation.

### Thermal-interval convergence: first order, confirmed

The same single-cell 9 m pond run at 60 and 15-minute thermal intervals, corrected to a common end time:

| | value |
|---|---|
| 60-min, adjusted to common end time | 18.8227311 |
| 15-min, as run | 18.8110347 |
| Richardson extrapolation, dt -> 0 | **18.8071359** |
| truncation error at 60 min | +0.01560 C |
| truncation error at 15 min | +0.00390 C |
| **ratio** | **4.00** |

A 4x reduction in `dt` gives a 4.00x reduction in error: exact first-order convergence, as explicit forward Euler should deliver. **Keep this as a standing regression check.** When the relaxation reformulation lands, the 60-minute error should collapse by orders of magnitude rather than by 4x, because the relaxation form is exact for piecewise-constant forcing.

### Stepped-bed depth ladder (the primary new test)

**Design.** A 12x12 domain, outer ring dry at elevation 11, interior a level 10 m water surface over a stepped bed so that depth varies column by column with **zero flow anywhere**. Depths 5, 2, 1, 0.5, 0.25, 0.1, 0.05, 0.015, 0.012, 0.005 m. Forcing constant: shortwave 200 W/m2, air 16 C, wind 4 m/s, RH 70 (fallback), 1013.25 mb, no cloud, 50 N, longitude 270, start 2000-06-21 00:00, HEC-RAS albedo and longwave, 5-day run. This gives Test A and Test B in one run and requires no sills or isolated basins.

**Result at a 60-minute interval** (`independent sim` = forward Euler reimplementation):

| depth | model | independent sim | diff |
|---|---|---|---|
| 5 m | 17.72763 | 17.73467 | +0.007 |
| 2 m | 16.26914 | 16.26482 | -0.004 |
| 1 m | 15.63207 | 15.61156 | -0.021 |
| 0.5 m | 15.78848 | 15.75378 | -0.035 |
| 0.25 m | 16.43102 | 16.41957 | -0.011 |
| 0.1 m | 17.13602 | 17.32929 | +0.193 |
| 0.05 m | 16.94802 | 17.25860 | +0.311 |
| 0.015 m | **0.00000** | unstable | see §3 |
| 0.012 m | **0.00000** | unstable | see §3 |
| 0.005 m | 20.9999999990759 | n/a | below threshold, no energy balance |

**Result at a 15-minute interval** (mean absolute error **0.011 C** across all nine depths):

| depth | model | independent sim | diff |
|---|---|---|---|
| 5 m | 17.77165 | 17.77278 | +0.0011 |
| 2 m | 16.33052 | 16.33109 | +0.0006 |
| 1 m | 15.68057 | 15.67927 | -0.0013 |
| 0.5 m | 15.79279 | 15.78917 | -0.0036 |
| 0.25 m | 16.40154 | 16.39753 | -0.0040 |
| 0.1 m | 17.30530 | 17.31058 | +0.0053 |
| 0.05 m | 17.39017 | 17.42278 | +0.0326 |
| 0.015 m | 17.24328 | 17.26705 | +0.0238 |
| 0.012 m | 17.21468 | 17.24121 | +0.0265 |
| 0.005 m | 20.9999999668647 | n/a | below threshold |

**Interpretation.**

- The zeros vanish entirely at 15 minutes. The three shallowest active cells read 17.390, 17.243 and 17.215, within 0.18 C of each other, against an instantaneous equilibrium of 17.177 C at the final step. They are all tracking the equilibrium, which is what cells with time constants of 66, 20 and 16 minutes must do. **Nothing changed but the integration step**, which confirms the zeros had no physical content.
- The **0.005 m insulator behaviour is confirmed exactly**: no energy balance (0.005 is not greater than the 0.01 threshold), so the cell holds its initial 21 C to within 3e-8 C. The residual is advection mixing a trace of the cold neighbour's water in at the floating-point level, and its direction is correctly downward.
- **"Final temperature" is the wrong metric for this test.** It conflates three regimes: 5 m and 2 m (time constants 111 h and 44 h) have not equilibrated and read warm; 1 m and 0.5 m (22 h, 11 h) sit near the daily-mean equilibrium and read coldest; 0.25 m and below track the *instantaneous* equilibrium and are caught wherever the diurnal cycle happens to be. Record **final-day mean and diurnal amplitude** instead. Predicted values for the current configuration:

| depth | tau (h) | final-day mean | diurnal amplitude | lambda·dt at 60 min |
|---|---|---|---|---|
| 5 | 111 | 17.85 | 0.41 | 0.01 |
| 2 | 44.4 | 16.23 | 0.59 | 0.02 |
| 1 | 22.2 | 15.29 | 0.97 | 0.05 |
| 0.5 | 11.1 | 14.97 | 1.77 | 0.09 |
| 0.25 | 5.55 | 14.94 | 3.32 | 0.18 |
| 0.1 | 2.22 | 14.82 | 6.33 | 0.45 |
| 0.05 | 1.11 | 14.55 | 8.01 | 0.90 |
| 0.015 | 0.33 | 13.40 | 15.93 | 3.00 |
| 0.012 | 0.27 | 11.76 | 22.57 | 3.76 |

  The final-day mean converges to about 14.8 to 15.0 for everything from 0.5 m down, which is the real "all depths reach the same equilibrium" check, and the amplitude scales cleanly with 1/h until the instability takes over. The true equilibrium range under this forcing is 8.47 C, so a diurnal swing of that order at the shallow end is **physically correct** and should not be "fixed" away.

### Run-duration overshoot: a methodological trap

An initial comparison of the bed diagnostic (below) gave a mean error of 0.086 C and could not be accounted for. Running 121 steps instead of 120 reduced it to 0.0024 C. The model's 60-minute runs end at about 5.0417 days rather than 5.0, because the clock overshoots the target end time by up to one step.

At the deep end this is irrelevant. At the shallow end it dominates, because those cells track the instantaneous equilibrium and the equilibrium moves fast (`q_sw` falls from 176.9 to 174.6 W/m2 in the last 15 minutes alone). **Every ladder comparison carries a variable few-tenths offset at exactly the depths under investigation unless the end time is controlled.** See §7 for the recommended fix.

---

## 3e. Bed heat exchange: promoted from deferred to priority (new this revision)

Previously listed in §4 as "deferred by design". The ladder test shows this is not a second-order refinement but **the structural cause of the shallow-water failure mode**.

### The argument

The model has no floor on thermal inertia. `dT/dt = q_net/(rho·Cp·h)` grows without bound as `h` goes to zero. The physical system does have a floor, because a thin film sits on a substrate that participates in the diurnal heat cycle.

Diurnal damping depth `sqrt(2·kappa/omega)` with standard substrate properties:

| substrate | damping depth | heat capacity | water equivalent |
|---|---|---|---|
| wet sand | 0.117 m | 2.93e5 J/m2/K | 0.070 m |
| saturated silt/clay | 0.128 m | 3.85e5 J/m2/K | 0.092 m |
| drier substrate | 0.091 m | 1.82e5 J/m2/K | 0.043 m |

A 12 mm film has a heat capacity of 5.0e4 J/m2/K. The bed beneath it contributes **four to eight times that**, so a real 12 mm film behaves thermally like roughly 80 mm of water.

### Empirical confirmation

A temporary diagnostic (`depth = water_depth[x,y] + 0.070`, applied in the energy balance denominator only, **reverted afterwards**) was run at a **60-minute** interval, the interval at which the unmodified model produces zeros:

| depth | 15 min, no bed | 60 min, +0.07 m bed | difference |
|---|---|---|---|
| 5 m | 17.77165 | 17.75405 | 0.018 |
| 2 m | 16.33052 | 16.32020 | 0.010 |
| 1 m | 15.68057 | 15.65985 | 0.021 |
| 0.5 m | 15.79279 | 15.70993 | 0.083 |
| 0.25 m | 16.40154 | 16.16896 | 0.233 |
| 0.1 m | 17.30530 | 16.83104 | 0.474 |
| 0.05 m | 17.39017 | 17.08107 | 0.309 |
| 0.015 m | 17.24328 | 17.13817 | 0.105 |
| 0.012 m | 17.21468 | 17.13330 | 0.081 |

**No zeros, no oscillation, at any depth, at a 4x coarser timestep, purely from adding physically realistic thermal inertia.** The independent reimplementation of the diagnostic agrees to a mean of 0.0024 C and a maximum of 0.0058 C.

With the bed included, `lambda·dt` at a 60-minute interval falls from 3.76 to 0.55 at 0.012 m and from 3.00 to 0.53 at 0.015 m. **Plain forward Euler becomes stable at every depth in the ladder.**

### Consequences

- The bed **materially changes results up to roughly 0.5 m** (0.23 C at 0.25 m, 0.47 C at 0.1 m), not just at the problem depths. In a flood simulation that is most of the floodplain and the whole advancing wetting front.
- The existing `if (depth < water_depth_erosion_threshold) depth = water_depth_erosion_threshold;` clamp in both schemes is an accidental, crude proxy for this term, flooring the heat capacity at 0.01 m of water when the physical floor is 0.07 to 0.09 m water-equivalent, roughly seven to nine times too small. **Once the bed is implemented this clamp becomes redundant and should be removed**, and the sub-threshold insulator behaviour goes with it, because those cells could then be included safely.

### Design questions to settle before coding

- A new **persistent bed temperature array** that must exist for **dry** cells too, since the bed is there whether or not water covers it. Needs an initial condition (constant, raster, or initialised to annual mean air temperature) and a decision about behaviour under CAESAR's erosion and deposition.
- Lumped single layer with a conduction coefficient and thickness, or a discretised profile. **The +0.07 m diagnostic is a lumped-and-instantaneously-coupled approximation** that forces the bed to sit at the water temperature at all times. That overestimates the coupling and removes the phase lag, so the numbers above bracket the effect rather than predict it.
- **Coefficients from the primary source only.** The damping depths and substrate heat capacities above are a first-principles order-of-magnitude argument, not values read from HEC-RAS. HEC-RAS Chapter 2 carries a bed conduction term and CE-QUAL-W2 has sediment heat exchange with its own coefficient and sediment temperature; both need consulting before anything is coded. Per the project rule on AI-sourced figures, the numbers in this section are **diagnostic evidence, not implementation values**.
- GUI additions (conduction coefficient, bed thickness, initial bed temperature, enable checkbox) plus XML persistence, following the `WATERINIT_V1` pattern.
- Suggested tag: `BEDHEAT_V1`, to stay searchable alongside `TEMP_V1` and `WATERINIT_V1`.

---

## 4. Deferred by design (documented in the spec, not gaps)

Per the original spec (§7) and confirmed assumptions — these are deliberate, not omissions.

- ~~Bed/sediment heat exchange (`q_sed`)~~ — **no longer deferred. Promoted to a priority item: see §3e.** Empirical evidence now shows the missing bed thermal inertia is the structural cause of the shallow-water failure mode and materially changes results at all depths below about 0.5 m, so this is no longer a defensible simplification for a model whose wetted area is dominated by shallow cells.
- Ice formation — `water_temp` floored at 0°C; sub-zero energy-balance results are clipped, not modelled. **Note**: this floor is directly implicated in the floodplain forward-Euler issue above (§3) as the mechanism producing exact-zero readings; the deferred-ice decision itself is unaffected, but its interaction with the stability issue is now better understood.
- Full NetCDF/gridded meteorological ingestion — text time series only; architecture designed so this is a drop-in replacement later.
- Variable water density — constant 1000 kg/m³; future upgrade explicitly scoped to combine temperature + salinity + suspended sediment together, not just HEC-RAS's temperature-only term.
- Coupling of the new latent-heat term to the existing hydraulic `evaporate()` water-balance function — kept decoupled (A13a). Evaporative *mass* loss currently carries no corresponding thermal cooling effect on `water_temp`, a known minor physical inconsistency separate from the full scheme's own `q_l` term.

---

## 5. Outstanding work

Items 1 and 2 of the previous revision (confirm and verify the stage-input fix; regression-check the reach and catchment paths) are **complete**. In priority order:

1. **Implement the exponential-relaxation reformulation of the full scheme.** Specification and acceptance criterion in §3, evidence in §3d. Top remaining item: self-contained, transcription rather than new derivation, and it has an exact pass/fail test (the 60-minute ladder run must reproduce the 15-minute numbers). Do this **before** the bed work, because the bed should not be attempted while the integrator can still overshoot.

2. **Implement bed heat exchange (`BEDHEAT_V1`).** §3e. Now the largest outstanding physics gap. Settle the design questions listed there first, and get the coefficients from the primary source before coding.

3. **Test infrastructure (§7).** Fixed-cycle output is small, immediately useful, and removes a known source of error in every ladder comparison. Headless batch running is a larger piece worth scoping properly.

4. **Resolve the `(1 - R_L)` question and the `longwaveCloudExponent` ambiguity together** against ERDC/EL TR-16-1 Eq. 2.6 (§3b item 5). Both live in that one equation and both are currently unconfirmed. Worth doing in the same trip as the bed conduction term in §3e.

5. **Add a `heatOut` accumulator alongside the existing `waterOut` tally**, plus the two permanent conservation residuals described at the end of §3c and the advective boundedness assertion in §7. Small, and the highest-leverage change available for catching the §2 class of bug before it reaches a trace.

6. **GUI additions**: constant-value fallback boxes for humidity, cloud cover and pressure (§3, still open).

7. **`oil_evaporation()` integration** — replace the hardcoded `oil_T = 298` constant with live `water_temp[x,y]` (§6 of the spec, still not started).

8. **`save_data()` raster output** for `water_temp` — visualisation exists, no file-output option yet.

9. **Documentation pass** — fold the current §3, §3b and §4 caveats into the user-facing spec document.

10. **Consider the diffusive/dispersive heat exchange gap** (§3b, conservation item 4). With the bed term promoted, this is the remaining piece of missing damping physics: two adjacent cells with zero net flux currently never exchange heat at all, which the ladder test makes visible as a large unphysical horizontal temperature gradient.

11. **Longer-run, more varied testing**: diurnal-cycle met inputs (note `TempMetTimeStep` is currently 3600, interpreted as *minutes*, so a 5-day run draws on about three met rows stretched across it), multi-zone forcing, and a run long enough for the deepest cell to equilibrate (the 5 m column has a 111-hour time constant, so equilibrium tests need roughly 14 days). **Add a test case per input path**: the §2 bug was reachable only via the stage/tidal input and no prior test had exercised it.

---

## 7. Test suite and infrastructure (new this revision)

### Tests now built and passing

- **Single-cell relaxation (Test A).** 10x10 DEM, one cell at elevation 0 surrounded by 10 m walls, stage held at 9 m, no flow. Effectively a standalone 1-D column model. Completes in 133 iterations at a 60-minute thermal interval. Used for the convergence check in §3d.
- **Stepped-bed depth ladder (Test B).** §3d. The primary diagnostic for the shallow-water problem and the before/after benchmark for the reformulation.

**Design rules learned the hard way, worth keeping:**

- **A level water surface over a stepped bed is the right way to get varying depth with zero flow.** No sills, no isolated basins, physically natural, and it gives Tests A and B in one run.
- **`qroute()` imposes a forced edge slope** of plus or minus `edgeslope` at `x <= 2`, `x == xmax`, `y <= 2` and `y == ymax`, overriding the actual water surface. Any test domain must either keep its active cells at `x >= 3`, `x <= xmax-1`, `y >= 3`, `y <= ymax-1`, or wall them with dry high ground so `hflow` is zero at the boundary. The ladder uses the latter.
- **A raster row that is short of `ncols` fails silently.** The loader assigns while `xcounter <= xmax` and stops when the line runs out; unassigned cells keep the 0 left by `zero_values()`. In the ladder's first attempt this punched six elevation-0 holes through the wall, which produced a permanent leak and pinned the timestep. Recommend adding a per-row count check with a warning to both raster loaders.
- **Depth values exactly equal to `water_depth_erosion_threshold` are excluded from the energy balance** (the test is strictly greater-than). A 0.01 m column against a 0.01 m threshold reads as an insulator, not as the intended unstable test case.
- Verify test rasters arithmetically: `elev + depth` was confirmed to give exactly 10.0 in IEEE double precision for all ten ladder columns, offset 0.000e+00, ruling out floating-point surface tilt as a source of spurious flow.

### Tests specified but not yet built

- **Zero-forcing conservation.** Closed domain, `thermal_update_interval` longer than the run so the energy balance never fires, a **non-level** initial depth raster so the water sloshes and settles, and a non-uniform initial temperature raster. The assertion is exact: **the volume-weighted mean temperature must be conserved to machine precision for all time.** This is the permanent regression guard against any reintroduction of the §2 weight-sum bug, and it also exercises the new clock bound (b) engaging during the sloshing and releasing once it settles. Needs the `heatOut` / volume-weighted-mean accumulator from §5 item 5.
- **Advective boundedness assertion.** A debug-mode check inside `update_water_temperature_advection()` that the new temperature lies within the range of the participating temperatures. This is a mathematical guarantee of the convex-combination property, not a tolerance, so any violation is a bug by definition. Would have caught the §2 bug on the first iteration rather than after 22 hours of model time.
- **Bitwise repeatability.** Run the same configuration twice and diff the output. Not about temperature physics: `depth_update()` has several cells writing the same `dhdt_y` entries from different rows under `Parallel.For`, which is benign only while the written values are genuinely identical. The cheapest available guard against that assumption quietly becoming false.
- **Two-source mixing.** Two reach inputs at known, different discharges and temperatures merging into one channel; the downstream temperature must be the discharge-weighted mean. Tests the advection weighting quantitatively rather than only for boundedness.

### Infrastructure needed

- **Fixed-cycle output.** Currently sampled manually at the end of a run, which introduced a real error (see the run-duration trap in §3d). Recommended implementation is a third bound in the same block as the clock bounds (a) and (b):

  ```csharp
  // (c) land exactly on the next sample time and on the run end, rather than overshooting
  double nextTarget = Math.Min(nextSampleCycle, maxcycle * 60.0);
  if (cycle + time_factor / 60.0 > nextTarget) time_factor = (nextTarget - cycle) * 60.0;
  ```

  This gives exact-cycle sampling **and** makes every run end at exactly the requested duration, removing the variable few-tenths offset that currently contaminates shallow-cell comparisons.

- **Report final-day mean and diurnal amplitude**, not the final instantaneous temperature, for any depth-ladder comparison. See §3d for why the instantaneous value conflates three separate regimes.

- **Headless batch running.** Most of the machinery exists: every parameter is read from a control, the XML loader populates all of them, and a WinForms control object works without the form being shown. A command-line entry point could construct the form, call the existing XML loader on a supplied path, and invoke the run method directly. The real blockers are the `MessageBox.Show` calls throughout the loaders, which would hang a batch run on any warning, and UI refresh calls inside the main loop. A `Notify(string)` helper that writes to stderr in quiet mode and shows a box otherwise would handle the first. Worth scoping as its own piece of work.

---

## 8. Where things live (quick reference for a fresh session)

- Main code file: `Caesar Lisflood 2.0.cs`. Change tags, all searchable in comments:
  - `TEMP_V1` — the water temperature module itself, including the clock-control block in `erodedepo()` and the `maxflux` reduction in `depth_update()`.
  - `WATERINIT_V1` — the initial water depth raster (control declaration, designer block, `load_data()` loader, XML read/write).
  - `BEDHEAT_V1` — reserved for the bed heat exchange work (§3e), not yet used.
- GUI: "Water Quality" TabPage (`TempTab` and its child controls) tagged `TEMP_V1`; "Initial depth file" on the Files tab, below the grain index file, tagged `WATERINIT_V1`.
- Test assets: `squaredem_small.asc` (12x12 stepped-bed DEM, dry wall at elevation 11), `squaredem_small_depthstart.asc` (matching initial depth raster giving a level 10 m surface), `squaredem-temperature.xml` (configuration). See §7 for the design rules these encode.
- Design spec: `CAESAR-Lisflood_Water_Temperature_Module_Spec.md`.
- Branch overview: `README.md`.
- **`TEMP_V1_Session_Summary.md`** - full narrative account of the extended verification session that produced the previous revision: every bug found, every source cross-check performed, and the reasoning behind each fix. Note that its §4 records the pond-test bug as unresolved; that is now superseded by §2 of this document, which supersedes any conflicting statement in the session summary. The diagnostic trail it records is still worth reading for context on what was ruled out and why.
- This document: implementation progress tracker, to be updated as further steps complete. Sections are numbered 1, 2, 3, 3b, 3c, 3d, 3e, 4, 5, 7, 8 for historical continuity; there is no section 6.
- **Process note**: development happens across more than one machine. A prior regression this session was traced to an unpushed commit, not a logic bug — commit and push before switching machines to avoid re-diagnosing an already-fixed issue.
