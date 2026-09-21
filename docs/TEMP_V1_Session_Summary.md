# TEMP_V1 Verification Session Summary

This document summarises a single extended verification session on the CAESAR-Lisflood
water temperature module (TEMP_V1), starting from Step 19 (full HEC-RAS energy balance
assembly, compiled but not yet runtime-tested) through to an unresolved, blocking bug
found via a controlled "pond" test. It is intended to let a fresh conversation pick up
without re-deriving everything from scratch.

## 1. Confirmed fixes (implemented, verified, closed)

### 1.1 Richardson number and stability function

- `richardson_number()` was missing the leading `-2.0` factor from HEC-RAS Eq. 2.13.
  Confirmed against the primary source text directly (the equation itself, and its
  separately stated sign convention: unstable atmosphere has rho_air > rho_sat and
  Ri negative; stable has rho_air < rho_sat and Ri positive). Cross-checked against
  ClearWater-modules `tsm/processes.py ri_number()`, which uses `+2.0` but was found to
  be internally inconsistent with its own `ri_function()` branch labelling, i.e. a bug
  in that codebase, not an alternate valid convention.
- `richardson_stability_function()`'s stable branch used `(1-34*Ri)^-0.8`, which goes
  complex (NaN) for any Ri greater than about 0.029, inside its own stated valid domain
  (0.01 <= Ri < 2). Corrected to `(1+34*Ri)^-0.8`. Confirmed via: structural symmetry
  with the unstable branch, continuity at the Ri=2 boundary with the flat 0.03 floor,
  and an exact match to ClearWater-modules' `ri_function()`, which has the identical
  `(1.0 + 34.0 * ri_number_bounded) ** (-0.80)`.
- Domain bounding added: Ri is now clamped to [-1.0, 2.0] before the piecewise
  evaluation, matching ClearWater's own pre-clamp and guaranteeing the stable branch's
  base term stays positive.
- A regression was found partway through the session where both fixes appeared to have
  reverted. This was diagnosed as a real cross-machine git sync issue (changes made on
  one development machine were not pushed before switching to another), not a logic
  bug. Confirmed resolved and re-verified.

### 1.2 Wind function

- `wind_function()` was applying the exponent `c` to the whole sum `(a+b*windSpeed2)`
  rather than to `windSpeed2` alone. Eq. 2.11 is `f(u_w) = f(Ri)*(a+b*u_w^c)`, confirmed
  against CE-QUAL-W2's `FW = AFW+BFW*WIND2**CFW` (exponent on wind only). This bug was
  invisible under the original `c=1` default (mathematically a no-op at that exponent)
  and would have produced an approximately 8x magnitude error the moment a bundled
  formulation default of `c=2` was introduced. Fixed.

### 1.3 Sensible and latent heat: three selectable formulations

HEC-RAS Eq. 2.10/2.11 states its wind-function coefficients a, b, c as "user-defined"
with no default given (order 1e-6 for a and b), confirmed directly from the primary
source text (Zhang and Johnson 2016, ERDC/EL TR-16-1). This was cross-checked against
CE-QUAL-W2 v4.5 source (`heat-exchange.f90` and `temperature.F90`), whose
AFW=9.2, BFW=0.46, CFW=2 (confirmed identical across two independent real CE-QUAL-W2
example applications, an estuary and a reservoir) are used in a bundled form
(RC=FW*0.47*deltaT, RE=FW*deltaE, no separate density/specific-heat/latent-heat
multiplication), confirmed to output W/m2 directly by tracing
`RN = RS+RANLW-RB-RE-RC -> HEATEX` with no unit conversion factor present. The same
9.2/0.46/2 values were already independently present in CAESAR's own simplified
equilibrium scheme (Eq. 2.19). Plugging 9.2/0.46/2 into the literal separated-term
equation structure overshoots physically plausible flux by roughly 1000x, confirming
the two coefficient sets are not interchangeable between formulations.

Resolution implemented: three selectable options, GUI-exposed on the Water Quality tab
(`useBundledSensibleLatent`, `useRichardsonStabilityCorrection`, separate coefficient
sets `windFunc_*_bundled` and `windFunc_*_separated`), with XML save/load support:

- Option A: literal Eq. 2.10/2.11, explicit rho*Cp*L terms, unconfirmed 1e-6 order
  coefficients. Not the default. Retained for textual fidelity to the printed equation.
- Option B: CE-QUAL-W2 bundled form, 9.2/0.46/2, Richardson correction off. Exact match
  to CE-QUAL-W2's validated production behaviour.
- Option C (default): same bundled form and coefficients as B, Richardson correction on,
  as a deliberate CAESAR extension. Not independently validated as a combination in
  either source, but f(Ri) is approximately 1 near neutral conditions so it matches B's
  validated behaviour in the common case.

Also fixed as part of this: `air_density()` was updated from a vapour-pressure form to
the mixing-ratio form `0.348*(P/T_K)*(1+w)/(1+1.61*w)`, matching ClearWater-modules'
`density_air()`/`density_air_sat()` exactly, including the `mixing_ratio_air()` helper
(`0.622*e/(P-e)`, with a divide-by-zero guard for P==e).

All of the above were cross-validated numerically in the Immediate Window across
multiple test pairs, with signs and magnitudes matching physical expectation and, in
several cases, matching independent hand derivation to 3+ significant figures,
including a strong-stability test case where the Richardson suppression factor matched
a hand calculation of `(1+34*Ri)^-0.8` almost exactly.

### 1.4 Met file gap-fill

`load_met_file()` had no hold-last-value behaviour once its input file ran out of rows,
unlike `load_source_temp_file()` which already had this. Any met variable would
silently sit at the C# array default of 0.0 beyond the file's actual length, with no
warning. A fix mirroring the source-temperature file's gap-fill logic was written and
applied.

## 2. Validated behaviour (confirmed via multi-day, two-point trace testing)

Using a debug trace at two stable main-channel points (an upstream, constantly
source-fed cell at 12C, and a downstream cell fed only by advection from three
identically-temperatured river sources), the assembled full energy balance was
validated over a genuine 5-day run:

- The hand-calculated `dT = q_net*dt/(rho*Cp*depth)` matched the actual model's
  `T_w_before -> T_w` step to within 0.22-0.60% on every row at both points, with no
  outliers, across depths ranging from under 1m to over 4m.
- `solar_altitude()` and `reflection_coefficient()` were confirmed working correctly:
  the shortwave peak declined slowly and monotonically across five consecutive days
  post-summer-solstice, exactly as expected from genuine solar geometry (not something
  a bug could produce by coincidence), and sunrise/sunset transitions landed at
  consistent times each day.
- No slow warming or cooling drift was observed over the 5-day run under constant
  forcing, a good global stability signal for the core physics.
- `water_longwave()` was cross-validated at new operating temperatures not previously
  tested in isolation, matching to within about 0.1 W/m2 of interpolated expectation.
- One documented, minor, explained deviation: a single row at a rapidly-filling shallow
  cell (depth changing from 0.33m to 1.81m within one 60-minute step) showed a larger
  residual (4.7%), attributed to the debug trace and the real update sampling depth at
  slightly different points within a fast-changing transient. This is a minor modelling
  nuance, not a bug, and resolved on its own once the cell's depth stabilised.

This work also surfaced an early, transient advection-driven jump at the downstream
point right at the very end of one 5-day run (a single-row +0.515C jump attributed to
advection, coinciding with depth beginning to recede), noted at the time as a plausible
early sign of the floodplain phenomenon investigated next, though not confirmed as
directly connected.

## 3. Floodplain oscillation: root cause identified (now secondary to Section 4)

Floodplain cells were observed in visualisation oscillating between under 5C and over
18C within a couple of hours. Two-point debug traces at oscillating floodplain
locations found:

- `update_water_temperature_energybalance()`'s full scheme uses a plain explicit
  forward-Euler step over the full `thermal_update_interval` (60 minutes in the test
  configuration), with a floor at 0C (`if (T_new < 0) T_new = 0;`, documented in-code as
  deferred ice handling) but no ceiling and no subcycling.
- At shallow depths (roughly 0.1-0.5m in the test data), a single hour of typical
  diurnal q_net forcing (order 200-300 W/m2 either sign) produces a per-step deltaT of
  1-2.5C. Several such steps in the same direction during a sunlit or dark period
  accumulate into swings exceeding 20C across a half-day period, and the floor clamp
  produces exact-zero readings whenever the cooling side would otherwise overshoot
  below 0C.
- This mechanism was distinguished from a second, separately observed phenomenon: large
  between-step (i.e. advection-attributed) temperature swings occurring even when net
  cell depth was nearly frozen, suggesting possible independent hydraulic churn (inflow
  and outflow nearly cancelling while still carrying significant mixing) at the same
  shallow, flow-active locations. This second thread was not fully confirmed or
  isolated before the pond test superseded it as the primary investigation.

Suggested fix, not yet implemented: reformulate the full scheme's per-step update as an
exponential relaxation toward a local equilibrium temperature, the same technique
already used by CAESAR's own already-validated simplified/equilibrium scheme
(`T_new = T_eq + (T_w - T_eq)*exp(-K_T*dt/(rho*Cp*depth))`), which is unconditionally
stable regardless of depth or timestep size, rather than raw forward-Euler with an ad
hoc floor. This is not a novel technique to introduce; it is already implemented and
proven elsewhere in the same file.

It is not yet established whether this remains a distinct issue requiring its own fix
once the Section 4 bug is resolved, or whether it was partly or wholly a symptom of it.

## 4. UNRESOLVED, BLOCKING: severe runaway temperature bug (pond test)

### 4.1 Test setup

A controlled, near-steady-state test was designed specifically to isolate advection
from the temperature physics: a simple 7x7 cell DEM, dx=10m, all edges closed to
prevent outflow, with water introduced via a single constant water-level input cell
(elevation 1.0m, water level held at 9.0m, giving 8m depth at that cell). Background and
initial water temperature 21C; all met inputs held constant (wind 4 m/s, air temp 16C,
shortwave 200 W/m2, dewpoint 12C). Debug traces were taken at a shallow (approximately
0.5m) and a deep (4-8m) cell.

### 4.2 Findings

- Both the full and simplified energy balance schemes show catastrophic runaway water
  temperatures under this setup: values reaching thousands of degrees in the trace
  data, and over 100,000C observed directly via the model's point inspector at deep
  (over 8m) locations elsewhere in the domain.
- Deep cells reach the same catastrophic outcome as shallow cells, merely delayed by a
  few cycles (roughly 20 vs 13 in the traces). This rules out a purely shallow-cell,
  small-heat-capacity explanation on its own, since a genuinely deep cell has far more
  thermal inertia and should be much more resistant to a pure discretisation-overshoot
  mechanism if that were the whole story.
- A real, confirmed, and separately significant bug was found and fixed during this
  investigation: `water_depth_prev[x,y]` was only ever assigned inside
  `save_tracer_states()`, meaning it remained permanently zero whenever the tracer
  subsystem was disabled (as in this test, and very plausibly in prior validation
  sessions too, though those happened to use stable, well-established inflow that
  masked the effect). Because `update_water_temperature_advection()` reads
  `water_depth_prev[x,y]` to compute `depth_after_outflows`, and that value was always
  zero, the branch intended only for genuinely newly-emptied cells
  (`depth_after_outflows <= 0.0 || water_depth_prev[x,y] <= 0.0 || ...`) was firing
  unconditionally for every wet cell on every iteration, discarding each cell's own
  prior temperature entirely and replacing it purely with the inflow-weighted average
  `dhdt_sumInTemp`. The genuinely intended depth-weighted blending branch never executed
  at all under these conditions.
- Fix applied: `save_temperature_states()` now also assigns
  `water_depth_prev[x,y] = water_depth[x,y]` when `isTraceWater` is false, sharing the
  array with the tracer subsystem (one writer active at a time depending on the flag)
  rather than introducing a duplicate array. This was folded into the same loop that
  already assigns `water_temp_prev`. The ordering relative to `erodedepo()`'s sequence
  (water inputs, then state-saving, then `qroute()`/`depth_update()`, then advection) was
  checked and confirmed correct.
- This fix measurably improved pre-blowup trace behaviour (residuals against hand
  calculation dropped from effectively unbounded to the 10-50% range in early rows,
  and the trajectory became visibly more coherent), but it did **not** resolve the
  runaway. The blowup still occurs.
- The most important remaining clue: after this fix, two **separate** simulation runs,
  tracing two **different** cells (the shallow and deep test points), both show
  corruption beginning at the **exact same** model cycle time,
  `cycle = 1320.008316` minutes (approximately 22 hours). This strongly suggests a
  shared, global triggering event rather than independent per-cell numerical
  instability continuing from the depth/inflow dynamics alone.
- The most obvious hypothesis for a shared trigger at a fixed model time, an input file
  running out of rows without holding its last value, was tested directly and **ruled
  out**: met files have approximately 200 rows at a 3600-minute timestep, and the water
  level input has approximately 1000 rows at a 60-minute timestep, both far exceeding
  the point at which the corruption begins.
- The 8m depth at the input cell was confirmed intentional and correct (elevation plus
  water level). The water-level boundary condition itself was confirmed to settle
  quickly after an initial transient and then hold steady; the instability is
  specifically in temperature, not in the depth/hydraulics of the boundary condition
  itself.

### 4.3 Candidate next steps (untried at end of session)

- **Direct instrumentation** (highest-value next step): expose and log
  `dhdt_sumIn` and `dhdt_sumInTemp` at the debug cell specifically around
  cycle 1300-1340, to observe the actual mechanism directly rather than continuing to
  infer it from `T_w` alone. These were not available in scope at the existing debug
  print location in this session; this would need either a small refactor to expose
  them or a temporary local instrumentation pass.
- **Domain-edge timing hypothesis**: check whether cycle ~1320 corresponds to the point
  at which the flood front from the initial 5x5 filling area first reaches the domain's
  closed edge cells. If so, this would point at a possible edge/boundary-neighbour
  handling bug in the temperature advection code that is simply untested until flow
  physically reaches the domain boundary, distinct from anything examined so far.
- **Unrelated periodic event check**: rule out any other counter, index, or interval
  (output writing, a UI refresh, an unrelated periodic recalculation) that might
  coincide with this specific cycle count independent of any input file's length.
- Once resolved: re-run the pond test to confirm bounded, physically sensible
  behaviour, then revisit whether the Section 3 floodplain forward-Euler/subcycling
  issue still needs its own dedicated fix, or was substantially explained by this bug.

## 5. Reference sources used this session

- Zhang and Johnson (2016), ERDC/EL TR-16-1, "Aquatic Nutrient Simulation Modules
  (NSMs): Theoretical Basis and User's Guide" - primary equation source for the
  temperature module.
- CE-QUAL-W2 v4.5 source code (`heat-exchange.f90`, `temperature.F90`) - used for
  independent cross-validation of the wind function form, back-radiation constant, and
  Bowen ratio constant. Established as a productive comparison source; a v5 source was
  also mentioned as available but not yet consulted.
- ClearWater-modules (`clearwater_modules/tsm/processes.py`), from the same USACE/ERDC
  lineage as HEC-RAS, in modern Python. Used for independent cross-validation of the
  Richardson number, stability function, and air density formulations. One genuine bug
  was identified in this codebase (a sign inconsistency between its `ri_number()` and
  its own `ri_function()` branch labelling) and is not something to replicate.

## 6. Code locations touched this session

- `richardson_number()`, `richardson_stability_function()`, `wind_function()`,
  `air_density()`, `mixing_ratio_air()` (new)
- `sensible_heat_flux()` / `latent_heat_flux()`, split into bundled and separated
  variants dispatched via `useBundledSensibleLatent`
- New fields: `useBundledSensibleLatent`, `useRichardsonStabilityCorrection`,
  `windFunc_a_bundled/b_bundled/c_bundled`, `windFunc_a_separated/b_separated/c_separated`
- New GUI group box and controls on the Water Quality tab for the above, plus XML
  save/load support in `menuItemConfigFileSave_Click` / `menuItemConfigFileOpen_Click`
- `load_met_file()` - gap-fill fix
- `save_temperature_states()` - now also populates `water_depth_prev` when tracers are
  off (needs the two documentation comments discussed in-session: one at
  `save_tracer_states()` noting the shared dependency, one at `save_temperature_states()`
  explaining the guard, both still to be added)
