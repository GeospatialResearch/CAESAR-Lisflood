# Water Temperature Module — Implementation Status

**Purpose of this document:** a snapshot of what has been built, verified, flagged as unverified/simplified, and what remains, so work can resume cleanly in a new session without re-deriving context. Read alongside `CAESAR-Lisflood_Water_Temperature_Module_Spec.md` (design spec) and `README.md` (branch overview) — this document tracks *implementation progress against that spec*, not the design itself.

**This revision reflects an extended verification session that moved Step 19 from "compiles, not yet run" to "core physics runtime-validated," found and fixed several real bugs along the way, and identified a severe, still-unresolved bug that currently blocks full-scheme trust.** See `TEMP_V1_Session_Summary.md` for the full narrative of how each item below was established, including source cross-checks and hand derivations.

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
| 13 | Simplified (equilibrium temperature) scheme — full implementation of HEC-RAS §2.2 (Eqs. 2.17–2.21), per-zone met caching, closed-form exponential update | Done. **Confirmed genuinely unconditionally stable** — this property is now the basis of the recommended fix for the full scheme's shallow-cell issue (§3). **However, this scheme also exhibits the pond-test runaway bug (§3) — the bug is not specific to the full scheme.** |
| — | Nodata handling fix: `water_temp` uses `-9999` sentinel for dry/unset cells rather than blanket-initialising everywhere | Done, tested working |
| — | Visualisation: "water temperature" added to Top Graphics II menu, `drawwater()` rendering (blue→white→red diverging scale, dynamically ranged) | Done, tested working |
| — | Advection/mixing: `update_water_temperature_advection()` — depth-weighted mixing via existing `dhdt_x`/`dhdt_y` tracer-style infrastructure, first-wetting rule (A2) | Done, working for well-established/steady inflow conditions. **A serious bug in this function's dependency on `water_depth_prev` was found and fixed this session — see §2. This is very likely the same function implicated in the still-open pond-test bug (§3).** |
| — | `save_temperature_states()` — dedicated previous-state snapshot function (split out from `save_tracer_states()` after a null-reference bug; see §2) | Done. **Extended this session** to also maintain `water_depth_prev` when the tracer subsystem is disabled — see §2. |
| — | Source-input wiring: rainfall, reach, and tidal inputs assign/mix a temperature into newly-added water (defaulting to `waterTempInitialValue` unless overridden — see next row) | Done, tested working for the reach/catchment/tidal input functions specifically checked this session. **Not yet separately confirmed for the constant water-level input mechanism used in the pond test — see §3.** |
| 14 | Source temperature file: shared multi-column file for rain/tide/reach inputs, flexible header row (single index / `a-b` range / `a+b` combination / `ALL` keyword), gap-filling (interpolate internal gaps, hold flat at file ends), diagnostic message box reporting the full source-index-to-column mapping and any header warnings | Done, tested working (short-file "hold last value" bug found and fixed) |
| 15 | Solar geometry: `solar_altitude()`, `solar_geometry()` (extraterrestrial radiation `q_o`), `reflection_coefficient()` (`R_s`); new `simulationStartDateTime` GUI control anchoring `cycle` to a real calendar date/time | **Runtime-confirmed working this session** (previously "compiles, not yet run-time tested" — see §3 for how this was upgraded from flagged to confirmed) |
| 16 | Atmospheric longwave (`atmospheric_longwave_hecras()`, HEC-RAS default) and its CAESAR-extension alternative (`atmospheric_longwave_humidity()`); back longwave (`water_longwave()`, fixed, no alternative) | `atmospheric_longwave_hecras()` and `water_longwave()` **runtime-confirmed this session** via multi-day trace and cross-source constant checks (§3). Humidity-aware alternative not yet separately exercised. |
| 17 | `wind_speed_at_height()` (Step 13, reused), `richardson_number()`, `richardson_stability_function()`, `wind_function()`, `saturation_vapour_pressure()` (HEC-RAS Eq. 2.9, confirmed against source), `latent_heat_of_vaporisation()`, `air_density()` | **All confirmed correct this session, after finding and fixing real bugs in three of them — see §2 and §3.** `air_density()` rewritten to the mixing-ratio form. |
| 18 | `sensible_heat_flux()` (`q_h`), `latent_heat_flux()` (`q_l`, with an explicit sign-convention flip — see §3) | **Substantially reworked this session.** Both functions now dispatch between two formulations (literal HEC-RAS separated-term vs. CE-QUAL-W2-derived bundled form) — see §3 for the full architecture and reasoning. Both confirmed correct in isolation via cross-source validation and hand derivation. |
| 19 | Full energy balance assembly — fills in the `useSimplifiedTempScheme == false` branch of `update_water_temperature_energybalance()`, combining Steps 15/16/18 into `q_net`, per-zone caching of `q_atm` and (HEC-RAS-mode) `q_sw` | Compiled and **runtime-validated at two stable test points over multi-day runs** (residual against hand-calculated ΔT under 1%). **However, a severe, unresolved runaway-temperature bug was found via a separate controlled test — see §3, this is the current blocker.** |

**Net result:** the simplified scheme's own equations are validated end-to-end for well-behaved, steady-inflow conditions. The full scheme's individual flux terms and their assembly are now genuinely runtime-verified, not just compiled, and several real bugs were found and fixed in the process. **Both schemes, however, currently fail catastrophically under a specific controlled test condition (§3) — this is the single most important open item and blocks calling either scheme "done."**

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
  - **Outstanding follow-up from this fix, not yet done**: add a documentation comment at `save_tracer_states()` noting that the temperature module now depends on it (indirectly, via the shared array and the `save_temperature_states()` guard) so a future refactor of the tracer subsystem doesn't silently reintroduce this exact bug. A matching comment at `save_temperature_states()` explaining the guard is also still needed.

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

- **UNRESOLVED, BLOCKING: severe runaway temperature bug.** A controlled "pond" test (7×7 cell closed domain, single constant water-level input, all met inputs constant, tracers off) revealed that **both** the full and simplified schemes produce catastrophic runaway water temperatures — thousands of degrees in trace data, over 100,000°C observed via the point inspector — in **both** shallow (~0.5 m) and deep (4–8 m) test cells. Deep cells reach the same catastrophic outcome, merely delayed by a few cycles, which rules out a purely shallow-cell/small-heat-capacity explanation on its own. The `water_depth_prev` fix above (§2) measurably improved pre-blowup behaviour but did **not** resolve this. The most important unexplained clue: after that fix, two separate simulation runs (different traced cells) both show corruption beginning at the **exact same** model cycle time (`1320.008316` minutes, ~22 hours), strongly suggesting a shared global trigger rather than independent per-cell instability. The most obvious hypothesis for this (an input file running out of rows without holding its last value) was tested directly and **ruled out** — all relevant input files far exceed the simulation length. Root cause not yet identified. See §5 (Outstanding work) for candidate next steps, and `TEMP_V1_Session_Summary.md` for the full diagnostic trail.
- **Floodplain forward-Euler / no-subcycling issue** (secondary to the item above, root cause identified, fix designed but not implemented). Floodplain cells were separately observed oscillating between under 5°C and over 18°C within hours. Root cause: `update_water_temperature_energybalance()`'s full scheme uses a plain explicit forward-Euler step over the full `thermal_update_interval`, with a floor at 0°C but no ceiling and no subcycling. At shallow depths (~0.1–0.5 m), a single hour of ordinary diurnal forcing produces a 1–2.5°C step; several such steps accumulate into 20°C+ swings across a half-day, and the floor clamp produces the exact-zero readings observed. **Suggested fix, not yet implemented**: reformulate the full scheme's per-step update as an exponential relaxation toward a local equilibrium temperature, the same unconditionally-stable technique already proven in CAESAR's own simplified scheme, rather than raw forward-Euler with an ad hoc floor. It is not yet known whether this remains a distinct issue once the item above is resolved, or was substantially the same underlying mechanism.
- **Missing-humidity/cloud-cover/pressure fallback values are still hardcoded** (70% RH, 0 cloud fraction, 1013.25 mb) rather than GUI-configurable constants, as originally flagged. **Not yet addressed** — a concrete fix (three new fields, GUI textboxes, XML save/load, anchor points) was drafted at the start of this session but implementation was superseded by the physics-verification work and has not been confirmed done.
- **`longwaveCloudExponent`** (Step 16): still using the defaulted-to-squared (`n=2`) assumption for the cloud-cover exponent in the atmospheric longwave formula, per the original ambiguity note. Not revisited this session.
- Humidity-aware atmospheric longwave alternative (`atmospheric_longwave_humidity()`) — not separately exercised or validated this session; only the HEC-RAS-default path was tested.
- Source-input temperature assignment for the constant water-level ("stage") input mechanism specifically has not been separately confirmed to correctly assign a background temperature to newly-added water, unlike the reach/catchment/tidal functions already checked. This was raised as a plausible contributing hypothesis for the pond-test bug and has not yet been ruled in or out.

---

## 4. Deferred by design (documented in the spec, not gaps)

Per the original spec (§7) and confirmed assumptions — these are deliberate, not omissions. Unchanged this session:

- Bed/sediment heat exchange (`q_sed`) — HEC-RAS default parameters recorded in the spec for future use.
- Ice formation — `water_temp` floored at 0°C; sub-zero energy-balance results are clipped, not modelled. **Note**: this floor is directly implicated in the floodplain forward-Euler issue above (§3) as the mechanism producing exact-zero readings; the deferred-ice decision itself is unaffected, but its interaction with the stability issue is now better understood.
- Full NetCDF/gridded meteorological ingestion — text time series only; architecture designed so this is a drop-in replacement later.
- Variable water density — constant 1000 kg/m³; future upgrade explicitly scoped to combine temperature + salinity + suspended sediment together, not just HEC-RAS's temperature-only term.
- Coupling of the new latent-heat term to the existing hydraulic `evaporate()` water-balance function — kept decoupled (A13a). Evaporative *mass* loss currently carries no corresponding thermal cooling effect on `water_temp`, a known minor physical inconsistency separate from the full scheme's own `q_l` term.

---

## 5. Outstanding work (not yet started)

In priority order:

1. **Resolve the pond-test runaway bug (§3) — this blocks everything else.** Candidate next steps, untried at end of session:
   - Directly instrument `dhdt_sumIn` and `dhdt_sumInTemp` at the debug cell around cycle 1300–1340 (not currently exposed at the existing debug print location) to observe the mechanism directly.
   - Check whether cycle ~1320 corresponds to when the flood front from the pond test's initial filling area first reaches the domain's closed edge cells — a possible untested edge/boundary-neighbour handling bug in the temperature advection code.
   - Rule out any unrelated periodic event (output interval, UI refresh, an unrelated counter) coincident with that specific cycle count.
   - Separately confirm whether the constant water-level input mechanism assigns a correct background temperature to newly-added water.
2. **Once resolved**: re-run the pond test to confirm bounded, physically sensible behaviour, then re-examine whether the floodplain forward-Euler/subcycling issue (§3) needs its own dedicated fix or was substantially explained by the bug above.
3. Add the two outstanding documentation comments flagged in §2 (`save_tracer_states()`/`save_temperature_states()` shared-dependency notes).
4. **GUI additions**: constant-value fallback boxes for humidity/cloud cover/pressure (§3, still open).
5. **`oil_evaporation()` integration** — replace the hardcoded `oil_T = 298` constant with live `water_temp[x,y]` (§6 of the spec, still not started).
6. **`save_data()` raster output** for `water_temp` — visualisation exists, no file-output option yet.
7. **Documentation pass** — fold the current §3/§4 caveats into the user-facing spec document (see the companion spec update alongside this document).
8. Longer-run, more varied testing once the blocking bug is resolved: diurnal-cycle met inputs, multi-zone forcing, a run long enough to exercise both schemes across a realistic range of conditions, including the domain-edge and boundary-condition scenarios the pond test has newly highlighted as under-tested.

---

## 6. Where things live (quick reference for a fresh session)

- Main code file: `Caesar Lisflood 2.0.cs` — all temperature-module additions tagged `TEMP_V1` in comments, searchable.
- GUI: new "Water Quality" TabPage (`TempTab` and its child controls), all also tagged `TEMP_V1`.
- Design spec: `CAESAR-Lisflood_Water_Temperature_Module_Spec.md`.
- Branch overview: `README.md`.
- **`TEMP_V1_Session_Summary.md`** — full narrative account of the extended verification session that produced this revision: every bug found, every source cross-check performed, the reasoning behind each fix, and the full diagnostic trail on the still-open pond-test bug. Read this if the summarised version above isn't enough detail to proceed.
- This document: implementation progress tracker, to be updated as further steps complete.
- **Process note**: development happens across more than one machine. A prior regression this session was traced to an unpushed commit, not a logic bug — commit and push before switching machines to avoid re-diagnosing an already-fixed issue.
