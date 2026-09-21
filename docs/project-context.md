# CAESAR-Lisflood TEMP_V1 — Project Handoff Document

## 1. Project Overview & Scope

Matt (researcher/developer, Geospatial Research Institute Toi Hangarau, University of
Canterbury, Christchurch, NZ) is extending CAESAR-Lisflood 2.0, an existing C#/.NET
WinForms hydrodynamic flood model, with a new water temperature simulation module
(codename **TEMP_V1**).

The work sits on a feature branch derived from an existing oil-spill-flow fork of
CAESAR-Lisflood. TEMP_V1 is foundational infrastructure: the temperature module is a
prerequisite for planned future water-quality and pollution modelling work (nitrates,
biological contaminants), not an end in itself.

Scope of TEMP_V1: simulate 2D spatially distributed water temperature across the
flood/flow domain, driven by meteorological forcing (air temp, wind, humidity/dewpoint,
shortwave radiation, cloud cover) and by advective mixing with inflowing water
(river/reach sources, catchment inputs, tidal/stage boundaries), using a surface energy
balance formulation. Two schemes are implemented: a simplified/equilibrium scheme and a
full explicit energy-balance scheme (shortwave, atmospheric longwave, back radiation,
sensible heat, latent heat).

Working branch on GitHub:
https://github.com/GeospatialResearch/CAESAR-Lisflood/tree/Temperature

## 2. Technical Stack & Rules

- **Language/framework**: C# / .NET WinForms (Visual Studio, Debug-mode testing).
- **Repository**: GitHub, feature branch off an oil-spill-flow CAESAR-Lisflood fork.
- **Key files**: `Caesar_Lisflood_2_0.cs` (main model logic, all TEMP_V1 physics
  functions live here), `Form2.cs`/`Form2.Designer.cs` (GUI), `Settings.cs`,
  `app.config`. Project docs: `TEMP_V1_Implementation_Status.md` (bug log, flagged/
  unverified equations, deferred items), `CAESAR-Lisflood_Water_Temperature_Module_Spec.md`
  (design spec), `README.md` (feature branch overview).
- **Multi-machine development**: Matt works across more than one machine. A prior
  regression was caused purely by not pushing to GitHub before switching machines
  (not a logic bug) — worth flagging as a recurring risk, not a one-off.
- **Constraint**: never produce full, very long C# files as a suggested change. Give
  clear anchor-point instructions (function name, surrounding lines) so Matt can apply
  snippets directly himself.
- **Documentation format**: markdown only, unless an alternative format is explicitly
  requested.

## 3. System Instructions (working style distilled from this project)

- All physics formulations must be anchored to a specific published source. AI-sourced
  or ambiguous figures are flagged as unconfirmed until independently verified, never
  presented with unearned confidence.
- Primary design reference: **Zhang & Johnson (2016), ERDC/EL TR-16-1** (HEC-RAS
  Nutrient Simulation Module water temperature documentation, Section 2). This is the
  authoritative source for equation *forms*.
- When the primary source is silent or ambiguous on a specific coefficient, cross-check
  against independent, related codebases before adopting a number:
  **CE-QUAL-W2** (Fortran source, v4.5 already reviewed, v5 available but not yet
  consulted), **ClearWater-modules** (`clearwater_modules/tsm/processes.py`, modern
  Python, same USACE/ERDC lineage), and the HEC-ResSim WQ manual. Treat matches across
  multiple independent sources as strong evidence; treat single-source claims (including
  from other AI tools) with real skepticism until cross-checked or hand-derived.
- Prefer hand-derivable, first-principles verification (unit/dimensional analysis, hand
  calculation of expected values) over trusting any single source or tool output at
  face value.
- Skeleton-first, small-increment implementation. Verify each function in isolation
  (e.g. via Visual Studio Immediate Window testing) before trusting an assembled,
  multi-function result.
- Refactoring carries regression risk — re-verify previously-confirmed physics after
  any larger structural change, don't assume it survived untouched.
- Log bugs and unverified equations/coefficients in `TEMP_V1_Implementation_Status.md`
  as they're found, with the reasoning trail, not just the fix.
- User preferences: plain text/markdown over Word docs for drafted material; no
  em-dashes in generated text; "NZ" not "New Zealand"; discuss proposed changes before
  drafting where practical; cross-check AI-sourced figures against primary sources
  before treating them as settled.

## 4. Current Progress & Context

**Implemented and confirmed working:**
- Steps 1–19 (full module skeleton through the full HEC-RAS energy balance assembly)
  compile and, as of the last session, are runtime-verified at the function level.
- `richardson_number()`, `richardson_stability_function()`, `wind_function()` — all
  confirmed correct (leading `-2.0` in Richardson number; `+34*Ri` stable branch with
  `Ri` clamped to `[-1,2]`; wind function exponent applied to wind speed alone, not the
  whole sum). Cross-validated against ClearWater-modules and hand derivation.
- Three selectable sensible/latent heat formulations, GUI-exposed with XML save/load:
  **Option A** (literal HEC-RAS separated-term, coefficients ~1e-6, explicitly
  user-defined per source with no default given — documented as unconfirmed, not
  default), **Option B** (CE-QUAL-W2 bundled form, AFW=9.2/BFW=0.46/CFW=2, no
  Richardson correction — exact match to CE-QUAL-W2 production behaviour), **Option C**
  (same bundled form, Richardson correction on, CAESAR's own extension — **default**).
  All cross-validated numerically to 3+ significant figures against hand calculation.
- `air_density()` rewritten to the mixing-ratio form matching ClearWater-modules exactly.
- Full energy balance assembly (`q_net = q_sw+q_atm-q_b+q_h+q_l`) validated against
  hand-calculated `dT` to within 1% over multi-day runs at two stable main-channel test
  points. Solar geometry (`solar_altitude()`, `reflection_coefficient()`) confirmed
  correct via diurnal pattern and post-solstice seasonal decline.
- `load_met_file()` gap-fill bug (no hold-last-value logic, unlike the source
  temperature file loader) — identified and fixed.

**Root cause identified, fix designed but not yet implemented:**
- Floodplain cells were observed oscillating wildly (under 5C to over 18C within
  hours). Root cause: `update_water_temperature_energybalance()`'s full scheme uses
  raw explicit forward-Euler integration over the full `thermal_update_interval`, with
  a floor at 0C but no ceiling and no subcycling — shallow cells (~0.1–0.5m) can swing
  tens of degrees in a single step under normal diurnal forcing. Suggested fix (not yet
  implemented): reformulate as an exponential relaxation toward equilibrium, the same
  unconditionally-stable technique already used in CAESAR's own simplified scheme,
  rather than raw forward-Euler. **Status now secondary to the item below** — not yet
  known whether this remains a distinct issue once that is resolved.

**UNRESOLVED AND BLOCKING — this is where the project currently stands:**
- A controlled "pond" test (7x7 cell closed domain, single constant water-level input,
  constant met forcing, no tracers) revealed a much more severe bug: water temperature
  runs away to thousands of degrees (over 100,000C observed via point inspector in one
  case), in **both** the full and simplified schemes, in **both** shallow and deep
  cells (deep cells merely delayed by a few cycles, ruling out a pure small-heat-
  capacity explanation).
- One real, confirmed bug was found and fixed along the way: `water_depth_prev[x,y]`
  was only ever populated by the tracer subsystem (`save_tracer_states()`), so it
  stayed at zero whenever tracers were off — causing
  `update_water_temperature_advection()`'s "cell was previously empty" branch to fire
  unconditionally every iteration for every cell, discarding each cell's own prior
  temperature in favour of purely the inflow-weighted average. Fixed by having
  `save_temperature_states()` also populate `water_depth_prev` when tracers are
  disabled (shared array, not duplicated; ordering relative to `erodedepo()` confirmed
  correct).
- This fix measurably improved pre-blowup behaviour but **did not resolve the
  runaway**.
- Key unexplained clue: after the fix, two separate simulation runs (different traced
  cells, shallow and deep) both show corruption beginning at the **exact same** model
  cycle time, `1320.008316` minutes (~22 hours) — pointing at a shared trigger, not
  independent per-cell instability. The obvious hypothesis (an input file running out
  of rows without holding its last value) was tested and **ruled out**: all relevant
  input files far exceed the simulation length.
- **Session ended at an impasse here.** A full narrative write-up of the entire
  verification session (all reasoning, all cross-source checks, all dead ends) exists
  as a separate document (`TEMP_V1_Session_Summary.md`) if deeper detail is needed than
  this handoff provides.

## 5. Next Steps

1. **Resolve the pond-test runaway bug** — this blocks everything else. Two untried,
   concrete next moves:
   - Directly instrument `dhdt_sumIn` and `dhdt_sumInTemp` at the debug cell around
     cycle 1300–1340 (currently not exposed at the debug print location; needs a small
     refactor or temporary local instrumentation) to observe the mechanism directly
     rather than continuing to infer it from `T_w` alone.
   - Check whether cycle ~1320 corresponds to the point at which the flood front from
     the test domain's initial filling area first reaches the closed edge cells —
     possible untested edge/boundary-neighbour handling bug in the temperature
     advection code.
2. Once resolved: re-run the pond test to confirm bounded, physically sensible
   behaviour, then re-examine whether the floodplain forward-Euler/subcycling issue
   (Section 4, first item) needs its own dedicated fix or was substantially explained
   by the runaway bug.
3. Add two still-outstanding documentation comments flagged during the fix: one at
   `save_tracer_states()` noting the temperature module's shared dependency on
   `water_depth_prev`, one at `save_temperature_states()` explaining the guard —
   needed so a future refactor of the tracer subsystem doesn't silently reintroduce
   this bug.
4. Deferred, lower-priority backlog: bed/sediment heat exchange, ice formation,
   variable water density (planned as a combined temperature/salinity/suspended-
   sediment formulation, not a temperature-only patch), NetCDF/gridded meteorological
   forcing ingestion, and an XML config structure refactor (ideally timed alongside the
   NetCDF work).
