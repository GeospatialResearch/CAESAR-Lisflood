# Water Temperature Module — Implementation Status

**Purpose of this document:** a snapshot of what has been built, what's been explicitly flagged as unverified/simplified, and what remains, so work can resume cleanly in a new session without re-deriving context. Read alongside `CAESAR-Lisflood_Water_Temperature_Module_Spec.md` (the original design spec) and `README.md` (branch overview) — this document tracks *implementation progress against that spec*, not the design itself.

---

## 1. What's built and working (Steps 1–19)

| Step | What it added | Status |
|---|---|---|
| 1 | Field/array declarations (`water_temp`, met arrays, scheme flags, site constants, etc.) | Done |
| 2 | Array allocation in `initialise()`, zeroing in `zero_values()` | Done |
| 3 | Empty function stubs for all planned functions | Done (all now filled in) |
| 4 | Wired (initially inert) calls into `erodedepo()` at correct cadence points | Done, bug-fixed (see §2) |
| 5 | GUI: new "Water Quality" tab, master `isSimulateTemperature` checkbox | Done |
| 6–8 | Text met-file loading: air temp, shortwave, wind speed, humidity, cloud cover, pressure, dew point (`load_met_file()`, one GUI box + `load_data()` call per variable) | Done, tested working |
| 9 | GUI: scheme selector (full/simplified radio buttons), HEC-RAS-default vs CAESAR-extension checkboxes (longwave, albedo), site parameters (lat/long/timezone/elevation/wind height), initial condition group box | Done |
| 10 | XML config save/load for all Water Quality tab settings | Done, tested working (round-trip confirmed) |
| 11 | Initial water temperature: constant value or raster override, wired into `zero_values()`/`load_data()` | Done, tested working |
| 12 | `interpolate_met()` — linear time interpolation of a met variable for a given zone | Done |
| 13 | Simplified (equilibrium temperature) scheme — full implementation of HEC-RAS §2.2 (Eqs. 2.17–2.21), per-zone met caching, closed-form exponential update | Done, tested working (cooling toward equilibrium confirmed physically sensible) |
| — | Nodata handling fix: `water_temp` uses `-9999` sentinel for dry/unset cells rather than blanket-initialising everywhere | Done, tested working |
| — | Visualisation: "water temperature" added to Top Graphics II menu, `drawwater()` rendering (blue→white→red diverging scale, dynamically ranged) | Done, tested working |
| — | Advection/mixing: `update_water_temperature_advection()` — depth-weighted mixing via existing `dhdt_x`/`dhdt_y` tracer-style infrastructure, first-wetting rule (A2) | Done, tested working |
| — | `save_temperature_states()` — dedicated previous-state snapshot function (split out from `save_tracer_states()` after a null-reference bug; see §2) | Done, tested working |
| — | Source-input wiring: rainfall, reach, and tidal inputs assign/mix a temperature into newly-added water (defaulting to `waterTempInitialValue` unless overridden — see next row) | Done, tested working |
| 14 | Source temperature file: shared multi-column file for rain/tide/reach inputs, flexible header row (single index / `a-b` range / `a+b` combination / `ALL` keyword), gap-filling (interpolate internal gaps, hold flat at file ends), diagnostic message box reporting the full source-index-to-column mapping and any header warnings | Done, tested working (short-file "hold last value" bug found and fixed) |
| 15 | Solar geometry: `solar_altitude()`, `solar_geometry()` (extraterrestrial radiation `q_o`), `reflection_coefficient()` (`R_s`); new `simulationStartDateTime` GUI control anchoring `cycle` to a real calendar date/time | Done, compiles; **not yet run-time tested** (no caller until Step 19) |
| 16 | Atmospheric longwave (`atmospheric_longwave_hecras()`, HEC-RAS default) and its CAESAR-extension alternative (`atmospheric_longwave_humidity()`); back longwave (`water_longwave()`, fixed, no alternative) | Done, compiles; **not yet run-time tested** |
| 17 | `wind_speed_at_height()` (Step 13, reused), `richardson_number()`, `richardson_stability_function()`, `wind_function()`, `saturation_vapour_pressure()` (**HEC-RAS Eq. 2.9, exact coefficients confirmed against source PDF via image OCR — see §3**), `latent_heat_of_vaporisation()`, `air_density()` | Done, compiles; **not yet run-time tested** |
| 18 | `sensible_heat_flux()` (`q_h`), `latent_heat_flux()` (`q_l`, with an explicit sign-convention flip — see §3) | Done, compiles; **not yet run-time tested** |
| 19 | Full energy balance assembly — fills in the `useSimplifiedTempScheme == false` branch of `update_water_temperature_energybalance()`, combining Steps 15/16/18 into `q_net`, per-zone caching of `q_atm` and (HEC-RAS-mode) `q_sw` | Just completed — **not yet compiled/tested** |

**Net result:** the simplified scheme is fully built and validated end-to-end (loading, advection, source input, visualisation all confirmed working together). The full energy balance scheme is now code-complete but **has not yet been compiled or run** — that's the immediate next action in a resumed session.

---

## 2. Bugs found and fixed along the way (for reference — all resolved)

- **XML load try/catch variable-name collision** (`e` shadowing an outer-scope `e`) — fixed by renaming to `eTemp`.
- **Missing XML loaders triggering the outer catch silently** — fixed by adding the missing element reads; also added explicit debug info to the catch block for future diagnosability.
- **`NullReferenceException` in `save_tracer_states()`** when `isSimulateTemperature = true` but `isTraceWater = false` — root cause: `water_depth_prev`/`dhdt_x`/`dhdt_y`/`watertracer*` arrays were only ever allocated inside the `isTraceWater == true` block in `button2_Click()`. Fixed in two parts:
  - Allocation of `water_depth_prev`/`dhdt_x`/`dhdt_y` now also happens when `isSimulateTemperature == true && isTraceWater == false`.
  - **Split `save_tracer_states()` into two functions**: tracer-specific logic stays as it was (only ever runs under `isTraceWater`), and a new, dedicated `save_temperature_states()` handles only `water_temp_prev`, called independently under `isSimulateTemperature`. This was a deliberate design improvement, not just a patch — avoids this class of bug recurring if either function is extended further.
- **Coarse-cadence energy-balance scheduling bug**: the `thermal_time` check was originally nested inside the daily `creep_time2` block, meaning it could only ever fire once a day regardless of `thermal_update_interval`. Fixed by moving it to its own independent, always-evaluated check (mirroring `save_time`/`save_time2`'s existing pattern), just before the `temptotal = temptot;` line.
- **Source-temperature-file "hold last value" bug**: the gap-fill loop only held the last valid value flat up to the file's actual row count (`rowIdx`), not the full allocated array length (`max_file_length`) — meant a short input file caused temperature to jump to the background default partway through a run. Fixed by extending the hold-flat loop to `max_file_length`.
- **`nSources` not computed for temperature-only runs**: needed for the source-temperature file's column mapping even when `isTraceWater == false`; added a small guarded block in `button2_Click()` mirroring the existing `isTraceWater` logic.

---

## 3. Explicitly flagged / unverified items (accuracy caveats)

These are places where a HEC-RAS-equivalent formula was used but **not confirmed word-for-word against the source report**, or where a deliberate simplification was made. All are commented in-code with `TEMP_V1` tags and should be treated as known follow-up items, not silent gaps:

- **`solar_altitude()` / `reflection_coefficient()`** (Step 15): standard, well-established formulations (Spencer 1971 declination series, Duffie & Beckman-style solar position; Anderson 1954-style reflectivity curve) used because the exact HEC-RAS derivation wasn't confirmed from the extracted report text. Physically reasonable and correctly shaped, but **not confirmed as a bit-for-bit match** to HEC-RAS's own equations.
- **`longwaveCloudExponent`** (Step 16): the cloud-cover exponent in the atmospheric longwave formula (`1 + 0.17·C_L^n`) was ambiguous in the OCR'd source text between linear and squared. Defaulted to squared (`n = 2`, matching the shortwave term's confirmed exponent) but flagged as a named constant specifically so it's a one-line fix if checked against the source and found to differ.
- **`saturation_vapour_pressure()` (HEC-RAS Eq. 2.9)** — **this one WAS explicitly verified**, via direct image-OCR extraction of the equation from the source PDF (rendered at high resolution, cross-checked across multiple OCR passes) and a numerical sanity check against a known reference value (~23.4 mb at 20°C, matched to within ~0.2%). Exact coefficients now in the code. This is the one fully-confirmed equation among the flagged group — noted here so it's clear it's resolved, not still open.
- **`latent_heat_flux()` sign convention** (Step 18): the source report defines `q_l` as *positive when it's a heat loss* (opposite to every other flux term's "positive = gain" convention). The implementation deliberately negates the raw HEC-RAS-convention result so that **every flux function in this codebase consistently returns "positive = gain to the water,"** simplifying the Step 19 summation. Documented in-code; worth double-checking this reasoning once the full scheme is actually run and compared against expected physical behaviour.
- **`sensible_heat_flux()` uses a fixed standard-pressure assumption (1013.25 mb)** for computing saturated air density, rather than the site's actual measured pressure — the report's Eq. 2.10 doesn't list pressure as an explicit input, unlike Eq. 2.8's latent heat (which does use real pressure). Flagged as a simplification, easy to change if needed.
- **Shortwave radiation uses the measured met input directly** (`q_sw = shortwave_measured × (1 − R_s)`), not HEC-RAS's from-scratch theoretical calculation (`q_o × α_t × (1 − R_s) × (1 − 0.65·C_L²)`, Eq. 2.4) — consistent with assumption A9 ("direct shortwave primary"). This means `solar_geometry()` (extraterrestrial radiation `q_o`) is implemented but **currently unused** — reserved for a future fallback path if shortwave is ever made optional.
- **Missing-humidity/cloud-cover/pressure fallback values are hardcoded** (70% RH, 0 cloud fraction, 1013.25 mb) rather than GUI-configurable constants, even though the original spec (§4) intended user-settable defaults for these. A small GUI addition (three more textboxes) would close this gap.

---

## 4. Deferred by design (documented in the spec, not gaps)

Per the original spec (§7) and confirmed assumptions — these are deliberate, not omissions:

- Bed/sediment heat exchange (`q_sed`) — HEC-RAS default parameters recorded in the spec for future use.
- Ice formation — `water_temp` floored at 0°C; sub-zero energy-balance results are clipped, not modelled.
- Full NetCDF/gridded meteorological ingestion — text time series only; architecture designed so this is a drop-in replacement later.
- Variable water density — constant 1000 kg/m³; future upgrade explicitly scoped to combine temperature + salinity + suspended sediment together, not just HEC-RAS's temperature-only term.
- Coupling of the new latent-heat term to the existing hydraulic `evaporate()` water-balance function — kept decoupled (A13a). **Newly flagged in this session**: this means evaporative *mass* loss (via `evaporate()`) currently carries **no corresponding thermal cooling effect** on `water_temp` — a real, minor physical inconsistency worth documenting as a known limitation, separate from the full-scheme's own `q_l` term (which *does* correctly cool `water_temp`, just without removing the corresponding mass via `evaporate()`).

---

## 5. Outstanding work (not yet started)

In roughly the order I'd suggest tackling them:

1. **Compile and test Step 19** (full energy balance) — the immediate next action. Suggest testing with the same Carlisle-summer-style constant test files used for the simplified scheme (now also needing air temperature and cloud cover files), and comparing behaviour/sign of each flux term via debugger before trusting the aggregate result.
2. **Validate the full scheme's individual flux terms** against hand-calculated or literature reference values, the way `saturation_vapour_pressure()` was checked — particularly worth doing for `q_h`/`q_l` given the sign-convention subtlety.
3. **Resolve the flagged-but-unverified equations** (§3) if exact HEC-RAS reproduction matters — likely needs going back to the source PDF with more targeted image extraction, as was done successfully for Eq. 2.9.
4. **GUI additions**: constant-value fallback boxes for humidity/cloud cover/pressure (closing the §3 gap); consider exposing atmospheric attenuation if the `solar_geometry()` fallback path is ever built.
5. **`oil_evaporation()` integration** — replace the hardcoded `oil_T = 298` constant with live `water_temp[x,y]` (§6 of the spec, still not started).
6. **`save_data()` raster output** for `water_temp` — visualisation exists, but there's no file-output option yet (mirrors the existing water-depth/velocity output pattern).
7. **Documentation pass** — fold the §3/§4 caveats here into the user-facing spec document properly, so a future user (not just a future coding session) knows what's approximate vs. exact.
8. Longer-run, more varied testing: diurnal-cycle met inputs (rather than constant test values), multi-zone forcing, a run long enough to exercise both schemes' behaviour across a realistic range of conditions.

---

## 6. Where things live (quick reference for a fresh session)

- Main code file: `Caesar Lisflood 2.0.cs` — all temperature-module additions tagged `TEMP_V1` in comments, searchable.
- GUI: new "Water Quality" TabPage (`TempTab` and its child controls), all also tagged `TEMP_V1`.
- Design spec: `CAESAR-Lisflood_Water_Temperature_Module_Spec.md`.
- Branch overview: `README.md`.
- This document: implementation progress tracker, to be updated as further steps complete.


### Sensible/latent heat formulation — resolved via cross-source validation, three options retained

HEC-RAS Eq. 2.10/2.11's wind-function coefficients (`a`, `b`, `c`) are explicitly stated as
"user-defined" in the source (ERDC/EL TR-16-1), order ~1e-6 for `a`/`b`, no worked example or
universal default given. Numeric plug-in testing confirmed this order of magnitude, taken
literally with the equation's explicit `ρ_s·Cp_air·L` structure, produces physically plausible
(if unconfirmed) flux magnitudes for Option A below.

Cross-checked against CE-QUAL-W2 v4.5 (`heat-exchange.f90`/`temperature.F90`): CE-QUAL-W2's
`FW = AFW+BFW·u^CFW`, with production defaults `AFW=9.2, BFW=0.46, CFW=2` (confirmed identical
across two independent real-world example applications, estuary and reservoir), is used in a
**bundled** form (`RC=FW·0.47·ΔT`, `RE=FW·Δe`) with no separate density/specific-heat/latent-heat
multiplication, confirmed to output W/m² directly by tracing `RN = RS+RANLW-RB-RE-RC` through to
`HEATEX` with no unit conversion factor present. These same 9.2/0.46/2 values are already used,
unmodified, in CAESAR's own simplified/equilibrium scheme (Eq. 2.19) — internal cross-validation.
Confirmed via grep of CE-QUAL-W2 source: no Richardson-number stability correction is applied to
`FW` in their formulation.

Plugging 9.2/0.46/2 into the *separated-term* equation structure (Option A) overshoots physically
plausible flux by ~1000x at ordinary conditions — the two coefficient sets are **not
interchangeable** between formulations.

**Resolution: three selectable options**, GUI-exposed via `useBundledSensibleLatent` /
`useRichardsonStabilityCorrection`:

- **Option A** — literal Eq. 2.10/2.11, explicit ρ·Cp·L terms, `windFunc_*_separated` (order
  1e-6, unconfirmed). Retained for textual fidelity; not the default.
- **Option B** — CE-QUAL-W2 bundled form, `windFunc_*_bundled` (9.2/0.46/2), Richardson
  correction **off** — exact match to CE-QUAL-W2's validated production behaviour.
- **Option C (default)** — same bundled form and coefficients as B, Richardson correction
  **on**, as a deliberate CAESAR extension. Not independently validated as a combination, but
  `f(Ri)≈1` near-neutral means it matches B's validated behaviour in the common case, diverging
  only under strong stability/instability, using the Richardson physics already independently
  confirmed elsewhere in this module.

Also fixed in this pass: `wind_function()` was applying the `c` exponent to `(a+b·windSpeed2)`
as a whole rather than to `windSpeed2` alone — invisible under the old `c=1` default (no-op),
would have caused an ~8x magnitude error the moment `c=2` (the bundled default) was introduced.


## Richardson number, stability function, and wind function — resolved and cross-validated

**Status: fully verified across all three sensible/latent heat formulation options (see below). Closed.**

### Bugs found and fixed

- **`richardson_number()` was missing the leading `-2.0` factor** from HEC-RAS Eq. 2.13
  (`Ri = -2g(ρ_air-ρ_sat)/(ρ_air·u²)`). Confirmed against the primary source text directly
  (Zhang & Johnson 2016, Eq. 2.13) two independent ways: the equation itself, and the report's
  separately-stated sign convention ("unstable atmosphere: ρ_air > ρ_sat... Ri is positive for
  stable, negative for unstable"). Cross-checked against ClearWater-modules'
  `tsm/processes.py ri_number()`, which uses `+2.0` — traced to their own `density_air`/
  `density_air_sat()` functions and found to be internally inconsistent with their own
  `ri_function()` branch labelling (their `+2.0` produces the wrong sign relative to their own
  stated unstable/stable classification), so treated as a bug in that codebase, not an
  alternate convention. CAESAR's `-2.0` is correct.

- **`richardson_stability_function()`'s stable branch used `(1-34·Ri)^-0.8`**, which goes complex
  (NaN in `Math.Pow`) for any `Ri > 1/34 ≈ 0.029` — inside its own stated valid domain
  (`0.01 ≤ Ri < 2`, per Eq. 2.14d). Corrected to `(1+34·Ri)^-0.8`, confirmed via: (a) structural
  symmetry with the confirmed-correct unstable branch `(1-22·Ri)^0.8`, which is really
  `(1+22|Ri|)^0.8` once Ri's negative sign is applied; (b) continuity at the `Ri=2` boundary
  with the flat `f(Ri)=0.03` floor; (c) ClearWater-modules' `ri_function()`, which has the
  identical `(1.0 + 34.0 * ri_number_bounded) ** (-0.80)`. **A later re-check of the primary
  source text confirmed the report itself states `(1-34Ri)^-0.8`** (Eq. 2.14d as printed) —
  this specific exponent form was retained as printed on the understanding that the source's
  own domain statement is internally over-broad (mathematically the formula as literally
  printed cannot hold for the full stated `Ri<2` domain), and the `+34` correction was applied
  as the physically/numerically defensible reading, not as a literal transcription of what the
  PDF shows. Documented here so this specific deviation-from-literal-text is traceable.
- **Domain bounding added**: `Ri` is now clamped to `[-1.0, 2.0]` *before* the piecewise
  evaluation (matching ClearWater's `np.select` pre-clamp, and the report's own domain
  statements per branch), rather than relying on the branch conditions alone. This guarantees
  `(1+34·Ri)` stays `≥1` (positive) across the whole valid range, eliminating the NaN case by
  construction.

- **`wind_function()` was applying the `c` exponent to `(a+b·windSpeed2)` as a whole**, rather
  than to `windSpeed2` alone (Eq. 2.11: `f(u_w)=f(Ri)·(a+b·u_w^c)`; confirmed against
  CE-QUAL-W2's `FW=AFW+BFW*WIND2**CFW`, exponent on wind only). Invisible under the original
  `c=1` default (mathematically a no-op at that exponent); would have produced an ~8x magnitude
  error the moment a bundled-formulation default of `c=2` was introduced. Fixed:
  `fRi * (a + b * Math.Pow(windSpeed2, c))`.

### Sensible/latent heat: three selectable formulations (resolved coefficient/magnitude problem)

HEC-RAS Eq. 2.10/2.11 explicitly states the wind-function coefficients `a`,`b`,`c` as
"user-defined" with no default given (order ~1e-6 for a/b) — confirmed directly from the
primary source text. Cross-checked against CE-QUAL-W2 v4.5 (`heat-exchange.f90`/
`temperature.F90`, ERDC-EL source), whose `AFW=9.2, BFW=0.46, CFW=2` (confirmed identical
across two independent real CE-QUAL-W2 example applications) are used in a **bundled** form
(`RC=FW·0.47·ΔT`, `RE=FW·Δe`, no separate ρ·Cp·L multiplication, confirmed W/m² output by
tracing `RN=RS+RANLW-RB-RE-RC → HEATEX` with no unit conversion factor present) with **no**
Richardson correction applied. These same 9.2/0.46/2 values were already independently present
in CAESAR's own equilibrium/simplified scheme (Eq. 2.19) — internal cross-validation. Plugging
9.2/0.46/2 into the *separated-term* equation structure overshoots physically plausible flux by
~1000x — the two coefficient sets are not interchangeable between formulations.

**Resolution — three selectable options**, GUI-exposed on the Water Quality tab
(`useBundledSensibleLatent`, `useRichardsonStabilityCorrection`, separate coefficient sets
`windFunc_*_bundled` / `windFunc_*_separated`):
- **Option A** — literal Eq. 2.10/2.11, explicit ρ·Cp·L terms, `windFunc_*_separated`
  (order 1e-6, source's own stated order, no worked example to confirm against). Not default.
- **Option B** — CE-QUAL-W2 bundled form, `9.2/0.46/2`, Richardson correction off — exact
  match to CE-QUAL-W2's validated production behaviour.
- **Option C (default)** — same bundled form/coefficients as B, Richardson correction on, as a
  deliberate CAESAR extension (not itself found in either source as a combination).

### Verification performed

All three options, plus `wind_function()`/`richardson_number()`/`richardson_stability_function()`
in isolation, confirmed via targeted Immediate Window testing:
- Sign of `q_h` correctly tracks `(T_a-T_w)` across all three options, including through the
  Richardson correction's stable/unstable branches.
- Option C's Richardson-driven suppression/enhancement factor cross-checked against hand
  calculation of `(1±k·Ri)^±0.8` for multiple `Ri` values — matched to 3+ significant figures.
- Strong-stability case (`Ri=1.36`, `T_a=35,T_w=15,wind=1`) confirmed ~95% suppression of
  sensible heat flux relative to the no-Richardson baseline, both the direction (suppression,
  not enhancement — this was the exact symptom of a since-fixed regression) and the numeric
  factor independently re-derivable by hand.
- Option A's tiny (fractional W/m²) magnitudes confirmed as expected/correct for its
  documented-unconfirmed coefficient order, not a bug.

**Outstanding**: none for this specific area. Steps 17/18 (`Caesar Lisflood 2.0.cs`) can be
marked run-time-tested and verified at the function level, not just "compiles."
