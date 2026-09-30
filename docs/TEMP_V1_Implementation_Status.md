# Water Temperature Module — Implementation Status

**Purpose of this document:** a snapshot of what has been built, verified, flagged as unverified/simplified, and what remains, so work can resume cleanly in a new session without re-deriving context. Read alongside `CAESAR-Lisflood_Water_Temperature_Module_Spec.md` (design spec) and `README.md` (branch overview) — this document tracks *implementation progress against that spec*, not the design itself.

**Previous revision (retained for context):** an extended verification session moved Step 19 from "compiles, not yet run" to "core physics runtime-validated," found and fixed several real bugs along the way, and identified a severe bug that was unresolved at the end of that session and blocked full-scheme trust. See `TEMP_V1_Session_Summary.md` for the full narrative of how each item below was established, including source cross-checks and hand derivations. That bug is now resolved; see the revision note immediately below.

**Revision note (this update):** the pond-test runaway bug that was blocking at the end of the previous session has been **root-caused** to a mixing-weight error in `stage_tidal_input()`, found by static audit rather than further tracing. The same audit re-reviewed all core thermodynamic formulas and carried out a dedicated energy-conservation pass, producing a further set of smaller findings recorded in §3b. The audit also established, by numerical simulation of the discretisation, that the floodplain forward-Euler issue is a genuinely **distinct** problem that will survive the runaway fix, closing a question left open last session. See §2 for the root cause, §3b for the audit findings, and §5 for the revised work order.

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
  - The instability threshold is approximately `dt > 2·ρ·Cp·h / (dq_net/dT)`. With `dq_net/dT` around 20 to 25 W/m²/K near 15°C, a 60-minute interval becomes marginal at roughly 0.05 m depth and is violated well before the `water_depth_erosion_threshold` floor of 0.01 m is reached.

  **Suggested fix, unchanged and still not implemented**: reformulate the full scheme's per-step update as an exponential relaxation toward a local equilibrium temperature, the same unconditionally-stable technique already proven in CAESAR's own simplified scheme, rather than raw forward-Euler with an ad hoc floor.
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

1. **Confirm the §2 root cause, then verify the fix.** Temporary instrumentation in `stage_tidal_input()` logging `water_depth - (prev_depth + dhdt)` and the per-iteration change in `water_temp` at the input cell. Expected under the pre-fix code: residual equal to `-prev_depth` (about -8.0) and a temperature increase of about +21°C every iteration. Then re-run the pond test with the fix in place. Expected: the input cell holds at `sourceTemp_tidal`, neighbours hold at the background value, and with constant met forcing the domain relaxes toward a single equilibrium rather than diverging. Remove the temporary instrumentation once confirmed.
2. **Re-run the earlier validation cases as a regression check**, since the two defensive denominator changes touched the reach and catchment input paths that the 5-day channel validation exercised. The change is algebraically a no-op there, so any difference indicates something else.
3. **Implement the exponential-relaxation reformulation of the full scheme (§3).** Now confirmed to be a distinct, still-open problem rather than a symptom of the runaway, so it is the top remaining physics item. The technique is already implemented and proven in the simplified scheme in the same file, so this is transcription rather than new derivation. Re-verify the two-point hand-calculation trace afterwards.
4. **Resolve the `(1 − R_L)` question and the `longwaveCloudExponent` ambiguity together** against ERDC/EL TR-16-1 Eq. 2.6 (§3b item 5, §3 `longwaveCloudExponent` bullet). Both live in that one equation and both are currently unconfirmed.
5. **Add a `heatOut` accumulator alongside the existing `waterOut` tally** and, if cheap, the two permanent conservation residuals described at the end of §3c. Small, and the highest-leverage change available for catching this class of bug before it reaches a trace.
6. **GUI additions**: constant-value fallback boxes for humidity/cloud cover/pressure (§3, still open).
5. **`oil_evaporation()` integration** — replace the hardcoded `oil_T = 298` constant with live `water_temp[x,y]` (§6 of the spec, still not started).
6. **`save_data()` raster output** for `water_temp` — visualisation exists, no file-output option yet.
7. **Documentation pass** — fold the current §3/§4 caveats into the user-facing spec document (see the companion spec update alongside this document).
8. Longer-run, more varied testing: diurnal-cycle met inputs, multi-zone forcing, a run long enough to exercise both schemes across a realistic range of conditions. **Specifically add a test case per input path.** The §2 bug was reachable only via the stage/tidal input, and no prior test had exercised it; the reach path had been validated for five days and the catchment path incidentally. One short test per input type would have caught it immediately.
9. Consider the diffusive/dispersive heat exchange gap (§3b, conservation item 4) alongside the deferred bed heat exchange term, since between them they are the module's missing damping physics and both bear on the shallow-cell oscillation behaviour.

---

## 6. Where things live (quick reference for a fresh session)

- Main code file: `Caesar Lisflood 2.0.cs` — all temperature-module additions tagged `TEMP_V1` in comments, searchable.
- GUI: new "Water Quality" TabPage (`TempTab` and its child controls), all also tagged `TEMP_V1`.
- Design spec: `CAESAR-Lisflood_Water_Temperature_Module_Spec.md`.
- Branch overview: `README.md`.
- **`TEMP_V1_Session_Summary.md`** - full narrative account of the extended verification session that produced the previous revision: every bug found, every source cross-check performed, and the reasoning behind each fix. Note that its §4 records the pond-test bug as unresolved; that is now superseded by §2 of this document, which supersedes any conflicting statement in the session summary. The diagnostic trail it records is still worth reading for context on what was ruled out and why.
- This document: implementation progress tracker, to be updated as further steps complete.
- **Process note**: development happens across more than one machine. A prior regression this session was traced to an unpushed commit, not a logic bug — commit and push before switching machines to avoid re-diagnosing an already-fixed issue.
