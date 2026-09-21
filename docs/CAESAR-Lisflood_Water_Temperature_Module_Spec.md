# CAESAR-Lisflood Water Temperature Module — Implementation Specification (v1)

**Status:** Implemented (Steps 1-19). Core physics runtime-verified against hand derivation and cross-source checks. **A severe, unresolved runaway-temperature bug currently blocks full-scheme trust — see `TEMP_V1_Implementation_Status.md` §3 for details before relying on this document's equations in isolation.** The equations below have been updated to reflect corrections made during verification; they are the as-built equations, not only the original design.
**Benchmark reference:** Zhang & Johnson (2016), *Aquatic Nutrient Simulation Modules (NSMs) Developed for Hydrologic and Hydraulic Models*, ERDC/EL TR-16-1, Section 2 (Water Temperature Simulation Module, "TEMP"), as implemented in HEC-RAS. Equation numbers below (e.g. "HEC-RAS Eq. 2.4") refer to that report.
**Secondary cross-validation references** (used during verification where the primary source was ambiguous or gave no default): CE-QUAL-W2 v4.5 source (`heat-exchange.f90`, `temperature.F90`); ClearWater-modules (`clearwater_modules/tsm/processes.py`).

---

## 1. Purpose and scope

Add a water temperature state (`water_temp[x,y]`) to CAESAR-Lisflood, replacing the current hardcoded constant (`oil_T = 298`) used by the oil-spill module, and providing the foundation for future nutrient/biological contaminant modules (which will need water temperature for Arrhenius-type rate correction, per HEC-RAS §2.3).

Two selectable schemes are implemented, matching HEC-RAS NSM exactly in structure:

- **Full energy balance** (HEC-RAS §2.1): shortwave, atmospheric longwave, back longwave, latent heat, sensible heat, each computed from meteorological forcing; optional Richardson-number stability correction on the turbulent wind function. Bed/sediment heat exchange (§2.1.6) is **deferred** (see §7).
- **Simplified (equilibrium temperature) balance** (HEC-RAS §2.2): closed-form equilibrium temperature and overall exchange coefficient, driven by dew point, wind speed and shortwave radiation only.

A user-facing switch selects which scheme is active for a given run. Default parameterisation matches HEC-RAS's published constants exactly; deviations are clearly flagged as CAESAR-specific extensions (see §3.10).

---

## 2. Assumptions register (finalised)

| # | Assumption | Status |
|---|---|---|
| A1 | `water_temp` stored in °C; converted to K only inside Stefan-Boltzmann terms | Confirmed |
| A2 | Dry cells carry no meaningful temperature; on re-wetting, a cell's temperature is initialised purely from the depth-weighted temperature of the water flowing in (no memory of prior dry-state value) | Confirmed |
| A3 | Spatial forcing: uniform (default) or zone-based, reusing the rainfall-zonation (`rainzonation`/`nRainZones`) pattern | Confirmed |
| A4 | Full NetCDF/gridded forcing deferred; zone-based mechanism is the forward-compatible bridge (a per-cell zone ID indexing a met time series is the limiting case of a full grid) | Confirmed |
| A5 | Text input files, one per met variable, tabular format identical to `hourly_rain_data` (rows = time step, columns = zone) | Confirmed |
| A6 | Single shared `met_data_time_step` across all met variables for v1 | Confirmed |
| A7 | Loader is written so a future NetCDF reader only needs to populate the same in-memory arrays; no downstream logic should depend on data provenance | Confirmed |
| A8 | Initial background water temperature: user-supplied raster (same header format as `elev`), read only if starting wet; if starting dry, temperature is set when a cell first wets, from inflow | Confirmed |
| A8a | Temperature of inflowing water (reach/point sources): an **additional optional column** in the existing per-source hydrograph files, following the precedent already set by solutes (columns 15+ in `inputfile[,,]`) | Confirmed |
| A9 | Shortwave: match HEC-RAS's solar-altitude-dependent reflection coefficient `R_s` (not a fixed albedo) as the **default**; retain a simplified fixed-albedo-with-suspended-sediment-adjustment as a selectable **CAESAR-extension** option | **Both**, HEC-RAS default |
| A10 | Suspended-sediment albedo adjustment is explicitly labelled as a CAESAR-specific empirical extension, not part of the HEC-RAS benchmark, and only active when the non-default albedo option is selected | Confirmed |
| A11 | Atmospheric longwave: match HEC-RAS's Swinbank-type, cloud-cover-only formula (no humidity term) as the **default**; retain a humidity-aware (Idso/Brunt-type) formula as a selectable **CAESAR-extension** option | **Both**, HEC-RAS default |
| A12 | Sensible heat: shared wind function with latent heat, plus user-adjustable diffusivity ratio `K_h/K_w` (default 1.0, range 0.5–1.5). **Amended during verification**: this diffusivity ratio applies only under the "separated" wind-function option (see A13a below); the "bundled" option uses a fixed Bowen-ratio constant instead, matching its cross-validation source | Confirmed, amended |
| A13 | Wind function includes the Richardson-number atmospheric-stability correction `f(R_i)` (HEC-RAS Eqs. 2.13–2.14) | Confirmed as one of three selectable options — see A13a |
| A13a | **Added during verification.** HEC-RAS's own wind-function coefficients (`a`,`b`,`c`, Eq. 2.11) are stated in the source as user-defined with no default given. Cross-checking against CE-QUAL-W2 found a different, production-validated coefficient set used in a structurally different ("bundled") formulation that cannot be mixed with the literal HEC-RAS term structure without a ~1000x magnitude error. Resolved by implementing three selectable, GUI-exposed sensible/latent heat formulations: **Option A** (literal HEC-RAS separated-term equations, unconfirmed order-1e-6 coefficients), **Option B** (CE-QUAL-W2 bundled form, coefficients 9.2/0.46/2, no Richardson correction — matches CE-QUAL-W2 exactly), **Option C, default** (same bundled form and coefficients as B, Richardson correction retained as a CAESAR extension). See §3.2 for the full equations. | Confirmed |
| A13a | New energy-balance latent heat term is thermally-informative only; **not** coupled back into the existing hydraulic water-balance evaporation term (`evaporate()`/`k_evap`), which remains untouched in v1 | Confirmed |
| A14 | Bed/sediment heat exchange (`q_sed`) deferred; HEC-RAS default parameters recorded now (§7) for future use | Confirmed |
| A15 | Ice/freezing deferred; `water_temp` floored at 0 °C, documented as a known simplification | Confirmed |
| A16 | Two-cadence time-stepping: fine-grained advective mixing every hydraulic iteration (tied to `qx`/`qy`), coarse-grained surface energy exchange on a user-configurable interval (default 60 min) — **applies to the full energy balance only**; the simplified (equilibrium) scheme is analytically cheap enough to run every iteration if desired, but for consistency will use the same coarse interval by default (user-overridable) | Confirmed |
| A17 | Both schemes implemented, user-switchable via a GUI radio button/checkbox, mirroring HEC-RAS's own full-vs-simplified choice | Confirmed |
| A18 | Water density (`ρ_w`) treated as a constant (1000 kg/m³) in v1. Deferred properly rather than patched in piecemeal: a future upgrade should account jointly for temperature, salinity, and suspended-sediment concentration, not just HEC-RAS's temperature-only Eq. 2.3 | Confirmed — deferred, scoped for future work |

**Resolution:** `ρ_w` = 1000 kg m⁻³ (constant) for v1, confirmed. This is deferred deliberately rather than as an oversight: a physically complete density model for this application needs to combine three effects that the model already has (or will have) the data for — temperature (HEC-RAS Eq. 2.3), salinity (relevant if/when tidal/estuarine and seawater-influenced reaches are modelled — note `ro_water` in the existing oil-spill code is already a fixed seawater-adjacent density constant, so there's precedent for salinity mattering here), and suspended-sediment concentration (the model already tracks grain-size fractions and `Vsusptot`/`oil_conc`-style suspended load, so a sediment-loading correction to bulk density is a natural, well-supported extension rather than a bolt-on). Implementing only the HEC-RAS temperature-only formula now would give a false sense of completeness and would likely need reworking once salinity/sediment are added anyway. **Recommendation for the future phase:** derive or adopt a combined empirical density relationship (temperature × salinity × suspended solids) in one pass, rather than layering three separate partial corrections over time — flagged here as a defined piece of future work, not an open question.

---

## 3. Governing equations (as they will be implemented)

All equations below are HEC-RAS §2 equations unless marked **[CAESAR ext.]**.

### 3.1 Core governing equation (both schemes)

```
ΔT_w = (q_net · Δt) / (ρ_w · C_pw · depth)
```
- `ρ_w` = 1000 kg m⁻³ (constant, A18)
- `C_pw` = 4186 J kg⁻¹ °C⁻¹ (standard value for water)
- `depth` = `water_depth[x,y]` (m), floored at `water_depth_erosion_threshold` to avoid divide-by-near-zero blow-up in very shallow cells (same guard style as `oil_conc`)
- `Δt` = elapsed seconds since last thermal update (coarse cadence, A16)

### 3.2 Full energy balance — flux terms

```
q_net = q_sw + q_atm − q_b + q_h + q_l          (as implemented; q_sed deferred, §7)
```
**Note on sign convention**: the raw HEC-RAS Eq. 2.1 convention is `q_net = q_sw + q_atm − q_b − q_h − q_l`, with `q_h`/`q_l` defined such that a positive value denotes a *loss*. As implemented here, `sensible_heat_flux()` and `latent_heat_flux()` are each written so that a positive return value always means a *gain* to the water, consistent with every other flux term in this module — this is why they are *added* above rather than subtracted. This is a deliberate, documented convention choice, confirmed correct via runtime validation (§3.2 below), not a discrepancy with the source.

**Shortwave (Eq. 2.4–2.5), HEC-RAS default:**
```
q_sw = q_o · α_t · (1 − R_s) · (1 − 0.65·C_L²)
q_o  = (Q_0/r²) · (sinφ·sinδ + cosφ·cosδ·cos(h_r))
```
- `Q_0` = 1360 W m⁻² (solar constant)
- `r` = normalised earth-orbit radius (day-of-year function)
- `φ` = site latitude (rad) — **new required setup input**
- `δ` = solar declination (day-of-year function)
- `h_r` = solar hour angle (time-of-day + longitude function) — **site longitude and time zone also required**
- `α_t` = atmospheric attenuation (fraction, literature default ~0.70–0.80 depending on elevation/turbidity; exposed as a tunable constant)
- `R_s` = reflection coefficient, function of solar altitude (standard published curve — to be implemented as a lookup/fitted function of solar elevation angle)
- `C_L` = cloud cover fraction (0–1), from met input or user-assigned constant if unavailable

**Shortwave — CAESAR-extension option [selectable]:**
```
albedo_eff = albedo_water_base + k_sed · min(1, susp_conc / susp_conc_ref)   [CAESAR ext.]
q_sw = q_sw_input · (1 − albedo_eff)
```
Used only when the user selects "fixed albedo + sediment adjustment" mode instead of HEC-RAS's `R_s`. `susp_conc` will read from the existing `Vsusptot`/grain-size suspended fraction where available.

**Atmospheric longwave (Eq. 2.6), HEC-RAS default:**
```
q_atm = 0.937×10⁻⁵ · (1 + 0.17·C_L^n) · σ · T_a(K)⁶
```
- `σ` = 5.670×10⁻⁸ W m⁻² K⁻⁴ (Stefan-Boltzmann constant)
- `T_a(K)` = air temperature in Kelvin
- `n` = cloud-cover exponent (report OCR ambiguous between linear and squared; **to confirm from a secondary published source before final coding** — treated as a named constant, not hardcoded inline, so it's trivial to correct)

**Atmospheric longwave — CAESAR-extension option [selectable]:**
```
ε_atm = f(e_a, C_L)     (Idso/Brunt-type, humidity-aware)   [CAESAR ext.]
q_atm = ε_atm · σ · T_a(K)⁴
```

**Back (water) longwave (Eq. 2.7), fixed — no alternative option:**
```
q_b = 0.97 · σ · T_w(K)⁴
```

**IMPORTANT — superseded during verification.** The original design below (literal HEC-RAS Eqs. 2.8–2.11 with a single shared wind function) is retained as **Option A**, but is **not the default** as implemented. HEC-RAS's own source states its wind-function coefficients as user-defined with no default given, and testing found they cannot be combined with a second, cross-validated coefficient set discovered during verification. See "Sensible/latent heat formulation options" immediately below for the as-built architecture; the equations after that describe each option precisely.

**Sensible/latent heat formulation options (as implemented, replacing the single design below):**

Three formulations are selectable via `useBundledSensibleLatent` and `useRichardsonStabilityCorrection`, each with its own coefficient set (`windFunc_*_separated` / `windFunc_*_bundled`):

| Option | `q_h`, `q_l` structure | Coefficients | Richardson correction | Notes |
|---|---|---|---|---|
| A | Literal Eqs. 2.8/2.10 below, explicit `ρ_s·Cp_air·L` terms | `a=b=1e-6, c=1` (HEC-RAS's own stated order; no default given in source) | Yes | Not the default; retained for textual fidelity |
| B | Bundled (see below) | `a=9.2, b=0.46, c=2` (CE-QUAL-W2 production default) | No | Exact match to CE-QUAL-W2 behaviour |
| C (default) | Bundled (see below) | Same as B | Yes | CAESAR extension; not validated as a combination in either source, but converges to B under near-neutral conditions |

**Latent heat, Option A — literal HEC-RAS (Eqs. 2.8–2.9):**
```
q_l = (0.622/P) · L · ρ_s · (e_s − e_a) · f(u_s)
```
- `P` = atmospheric pressure (mb)
- `L` = latent heat of vaporisation, T-dependent
- `ρ_s` = density of saturated air (mixing-ratio form, matching ClearWater-modules exactly — see below)
- `e_s` = saturation vapour pressure at `T_w` (Eq. 2.9, confirmed against source)
- `e_a` = actual vapour pressure of air, from relative humidity + air temperature
- `f(u_s)` = wind function, separated-coefficient variant (below)

**Sensible heat, Option A — literal HEC-RAS (Eq. 2.10):**
```
q_h = (K_h/K_w) · C_p_air · ρ_s · (T_a − T_w) · f(u_s)
```
- `C_p_air` ≈ 1005 J kg⁻¹ °C⁻¹
- `K_h/K_w` = diffusivity ratio, default 1.0, user range 0.5–1.5 (applies only to this option)
- `P` (site pressure) is now correctly passed into this function — the original design's fixed-1013.25-mb simplification was removed during verification.

**Latent and sensible heat, Options B/C — CE-QUAL-W2 bundled form:**
```
q_l = FW · (e_a − e_s)                                    [e_a, e_s in mmHg]
q_h = diffusivityRatio · FW · BOWEN_CONSTANT · (T_a − T_w)
```
- `FW` = the wind function (below), evaluated with the *bundled* coefficient set
- `BOWEN_CONSTANT = 0.47` (matches CE-QUAL-W2 exactly, and matches the constant already used in this module's own simplified scheme, Eq. 2.18/2.20 — independent internal cross-check)
- `e_s`, `e_a` computed from this module's own confirmed Eq. 2.9 saturation vapour pressure (mb), converted to mmHg (`× 0.750062`) to match CE-QUAL-W2's calibration units, rather than introducing a second vapour-pressure formula
- No separate density/specific-heat/latent-heat multiplication — confirmed via source inspection that CE-QUAL-W2's own flux assembly requires none (its `FW` already outputs W/m² directly)
- `diffusivityRatio` here is an optional CAESAR-extension multiplier on the sensible term only, neutral at its default of 1.0

**Wind function (both structures share this, coefficients differ by option):**
```
f(u_s) = f(R_i) · (a + b·u_s2^c)
```
**Corrected during verification**: the exponent `c` applies to `u_s2` alone, not to the whole `(a+b·u_s2)` sum — the original design's parenthesisation was ambiguous and the as-implemented version was found to contain this bug, since fixed. Confirmed against CE-QUAL-W2's `FW = AFW+BFW·WIND2^CFW` (exponent on wind speed only).
- `a`, `b`, `c` = per-option coefficients, see table above
- `u_s2` = wind speed corrected to 2 m reference height via the log law (Eq. 2.12)
- `f(R_i)` = Richardson-number stability correction (Eqs. 2.13–2.14a–e), piecewise, **corrected during verification**:
  - unstable (`R_i ≤ −1`): `f(R_i) = 12.3`
  - unstable (`−1 < R_i ≤ −0.01`): `f(R_i) = (1 − 22·R_i)^0.8`
  - neutral (`−0.01 < R_i < 0.01`): `f(R_i) = 1`
  - stable (`0.01 ≤ R_i < 2`): `f(R_i) = (1 + 34·R_i)^(−0.8)` **(corrected from `1 − 34·R_i`, which produced NaN for any `Ri > 1/34`; confirmed correct via structural symmetry with the unstable branch, boundary continuity at Ri=2, and an exact match to ClearWater-modules)**
  - stable (`R_i ≥ 2`): `f(R_i) = 0.03`
  - **`R_i` is now explicitly clamped to `[-1.0, 2.0]` before this piecewise evaluation** (added during verification, guarantees the stable branch's base term stays positive)
  - `R_i = -2.0 · g·(ρ_air − ρ_sat)/(ρ_air·u_s2²)`, `g` = 9.806 m s⁻² **(corrected during verification — the leading `-2.0` factor was missing from the original design; confirmed against the primary source's equation and its separately stated sign convention: `ρ_air > ρ_sat` denotes an unstable atmosphere and a negative `Ri`)**
  - `ρ_air`, `ρ_sat` now use the mixing-ratio density form `ρ = 0.348·(P/T_K)·(1+w)/(1+1.61·w)`, matching ClearWater-modules exactly (corrected from an earlier vapour-pressure form during verification)

### 3.3 Simplified (equilibrium temperature) balance — HEC-RAS §2.2

```
ρ_w·C_pw·(depth)·(∂T_w/∂t) = K_T·(T_eq − T_w)
```
This is a first-order linear ODE in `T_w`, analytically integrable over a timestep `Δt`:
```
T_w(t+Δt) = T_eq + (T_w(t) − T_eq)·exp(−K_T·Δt / (ρ_w·C_pw·depth))
```
(This closed-form update is unconditionally stable — no timestep restriction — and is the reason this scheme doesn't strictly need the coarse-cadence throttling that the full scheme benefits from; we'll use the same coarse interval by default anyway for consistency and simpler code paths, per A16.)

- `K_T = 4.5 + 0.05·T_w + (β + 0.47)·f(u_w7)` (Eq. 2.18)
- `f(u_w7) = 9.2 + 0.46·u_w7²` (Eq. 2.19), wind speed at 7 m reference height
- `β = 0.35 + 0.015·T̄ + 0.0012·T̄²`, where `T̄ = (T_w + T_d)/2` (Eq. 2.20)
- `T_eq = T_d + q_sw/K_T` (Eq. 2.21)
- Required inputs: dew point `T_d`, wind speed, shortwave radiation only (no cloud cover, humidity, or pressure needed for this scheme)

### 3.4 Advection / mixing (both schemes, every hydraulic iteration)

Follows the existing tracer-advection pattern (`save_tracer_states()` / `update_tracer_states()`), but as a genuine weighted average of °C values (not a 0–1 proportion split):

```
T_w(x,y)_new = [depth_after_outflows · T_w_prev(x,y) + Σ(inflow_depth_i · T_w_prev(source_i))] / water_depth_new(x,y)
```
using the same `dhdt_x`/`dhdt_y` bookkeeping already computed for tracers. Runs only when `isSimulateTemperature == true`, independently of `isTraceWater`.

### 3.5 Water density (A18)

Constant `ρ_w = 1000 kg/m³` in v1. HEC-RAS Eq. 2.3 (temperature-only quartic fit) recorded here for reference, but **not** to be implemented in isolation later — see §7 for the scoped future upgrade (temperature + salinity + suspended sediment combined):
```
ρ_w(T) = 999.973·(1 − ((T−3.9863)²·(T+288.9414)) / (508929.2·(T+68.12963)))
```

---

## 4. Meteorological input variables (finalised list)

| Variable | Units | Required for | Optional? | Fallback if missing |
|---|---|---|---|---|
| Air temperature | °C | Both schemes (full: `q_atm`, `q_h`; simplified: implicit via dew point) | No (full/simplified both need it directly or via dew point) | — |
| Shortwave radiation | W m⁻² | Both schemes | No (or derivable from clear-sky+cloud model as a documented fallback path) | Clear-sky solar geometry model + cloud cover |
| Wind speed | m s⁻¹ | Both schemes | No | — |
| Relative humidity | % | Full scheme only (`e_a` for latent heat; longwave under CAESAR-ext. humidity option) | Yes | User-assigned constant |
| Cloud cover | fraction 0–1 | Full scheme only (`q_sw` HEC-RAS `R_s` path uses `C_L²`; `q_atm`) | Yes | User-assigned constant (clear sky = 0 with warning) |
| Atmospheric pressure | mb | Full scheme only (`q_l`) | Yes | Standard sea-level pressure default (1013.25 mb), adjustable for site elevation |
| Dew point | °C | Simplified scheme only | No for that scheme | — |

**Site/geometry constants** (single values, not time series): latitude, longitude, time zone offset, site elevation, wind measurement height. These feed the solar-geometry and wind-height-correction calculations and are entered once in the GUI (not per-timestep).

**File format:** identical tabular structure to `hourly_rain_data[,]` — one value per row (time step) per column (zone), loaded via a new `load_met_file()` following the existing `load_hydrofile()`/rainfall-loading pattern exactly, including the same "skip blank/`#`-commented lines" robustness already built into `load_hydrofile()`.

---

## 5. New data structures

| Array | Type/shape | Purpose |
|---|---|---|
| `water_temp` | `double[xmax+2, ymax+2]` | Current water temperature (°C) |
| `water_temp_prev` | `double[xmax+2, ymax+2]` | Previous-iteration value, for advection bookkeeping (mirrors `water_depth_prev`) |
| `met_zonation` | `int[xmax+2, ymax+2]` | Per-cell met-zone ID (mirrors `rainzonation`) |
| `nMetZones` | `int` | Number of met zones in use |
| `metZones` | `int[]` | List of zone IDs present (mirrors `rainZones`) |
| `hourly_air_temp` | `double[,]` | [time step, zone] |
| `hourly_shortwave` | `double[,]` | [time step, zone] |
| `hourly_windspeed` | `double[,]` | [time step, zone] |
| `hourly_humidity` | `double[,]` | [time step, zone], optional |
| `hourly_cloudcover` | `double[,]` | [time step, zone], optional |
| `hourly_pressure` | `double[,]` | [time step, zone], optional |
| `hourly_dewpoint` | `double[,]` | [time step, zone], required only if simplified scheme selected |
| `isSimulateTemperature` | `bool` | Master enable |
| `useSimplifiedTempScheme` | `bool` | Scheme selector (false = full balance, true = simplified) |
| `useHecRasLongwave` / `useHecRasAlbedo` | `bool` | HEC-RAS-default vs CAESAR-extension option selectors (A9/A11) |
| `met_data_time_step` | `double` | Minutes, mirrors `rain_data_time_step` |
| `thermal_update_interval` | `double` | Minutes, default 60 |
| `thermal_time` | `double` | Next scheduled thermal-update time (mirrors `save_time`, `creep_time` idiom) |
| `siteLatitude`, `siteLongitude`, `siteTimeZone`, `siteElevation`, `windMeasurementHeight` | `double` | Single-value setup constants |
| `diffusivityRatio` (`Kh_Kw`) | `double` | Default 1.0. Under the bundled formulation (below) this is an optional CAESAR-extension multiplier on the sensible term only; under the separated formulation it is the literal Eq. 2.10 `Kh/Kw` |
| `albedoWaterBase`, `albedoSedimentCoeff`, `suspCondRef` | `double` | CAESAR-extension albedo option parameters |
| `useBundledSensibleLatent` | `bool` | **Added during verification.** Selects the sensible/latent heat formulation family: bundled (CE-QUAL-W2-derived, default) vs. separated (literal HEC-RAS) |
| `useRichardsonStabilityCorrection` | `bool` | **Added during verification.** Whether `f(R_i)` is applied to the wind function; forced `true` when the separated formulation is active |
| `windFunc_a_bundled`, `windFunc_b_bundled`, `windFunc_c_bundled` | `double` | **Added during verification.** Default 9.2 / 0.46 / 2.0 (CE-QUAL-W2 production values) |
| `windFunc_a_separated`, `windFunc_b_separated`, `windFunc_c_separated` | `double` | **Added during verification.** Default 1e-6 / 1e-6 / 1.0 (HEC-RAS source's own stated order of magnitude) |

---

## 6. Integration points (function-by-function)

| Existing function/location | Change required |
|---|---|
| `Form1` field declarations | Add all new arrays/scalars from §5 |
| `zero_values()` | Zero/initialise `water_temp`, `water_temp_prev`, `met_zonation` |
| `initialise()` | Allocate new arrays sized to `xmax+2, ymax+2` and time-series lengths; read new GUI values into the scalar constants |
| `load_data()` | Add: (a) optional water-temperature initial-condition raster load (mirrors `bedrockbox`/DEM-style load, only if provided and domain starts wet), (b) optional met-zonation raster load (mirrors the `rainzonation` block almost verbatim), (c) calls to new `load_met_file()` for each met variable |
| **New:** `load_met_file()` | Mirrors `load_hydrofile()` — tabular text reader with comment/blank-line handling, populates one `hourly_*` array |
| `load_hydrofile()` | Extend column-counting logic to optionally read an extra "source temperature" column per reach/point input file (A8a), analogous to how solute columns (15+) are already handled |
| `catchment_water_input_and_hydrology()`, `reach_water_and_sediment_input()`, `stage_tidal_input()` | Each needs to assign a temperature to newly-added water (from the new input column, or fall back to current air temperature if not supplied), analogous to how `watertracer` proportions are assigned on input |
| **New:** `update_water_temperature_advection(local_time_factor)` | Called every hydraulic iteration from `erodedepo()`, immediately after `update_tracer_states()`; implements §3.4 using existing `dhdt_x`/`dhdt_y` |
| **New:** `update_water_temperature_energybalance()` | Called from `erodedepo()` on the coarse `thermal_time` schedule (same idiom as `creep_time`, `soil_erosion_time`); implements §3.2 or §3.3 depending on `useSimplifiedTempScheme`; parallelised with `Parallel.For`/`down_scan` exactly like `evaporate()`/`qroute()` |
| **New:** helper functions | `solar_geometry()` (declination, hour angle, extraterrestrial radiation), `reflection_coefficient(solar_altitude)`, `richardson_number(...)`, `wind_function(...)`, `saturation_vapour_pressure(T)`, `latent_heat_of_vaporisation(T)`, `interpolate_met(variable, cycle, zone)` (mirrors existing `calc_J`-style time interpolation) |
| `oil_evaporation()` | Replace constant `oil_T` with `water_temp[x,y]` |
| `initialise_oil_simulation()` | Remove/repurpose the hardcoded `oil_T = 298` assignment |
| `drawwater()` | Add a new visualisation branch (mirrors `Tau`/`Vel` colour-mapping) plus a new `comboBox1` entry "water temperature" |
| `save_data()` | Add a new `typeflag` for writing a temperature raster (mirrors the water-depth/velocity output block) |
| `InitializeComponent()` / `Form2`-style Designer edits | New "Water Quality" TabPage with all GUI controls listed in §8 |
| Menu items (`mainMenu1` structure) | New top-graphics menu entry + save-options entry for temperature, following the exact existing pattern for oil depth/concentration |

---

## 7. Deferred items (documented, not built in v1)

- **Bed/sediment heat exchange** (`q_sed`, HEC-RAS §2.1.6). Parameters recorded for future use: default sediment layer thickness 10 cm, thermal diffusivity `α_s` = 0.005 cm² s⁻¹ (range 0.002–0.012), `ρ_s·C_ps` = 0.64 cal cm⁻³ °C⁻¹ default (full materials table available in the source report if a user wants to override for a specific substrate).
- **Ice formation.** `water_temp` floored at 0 °C; sub-zero energy-balance results are clipped, not modelled as latent heat of fusion. Documented limitation, most relevant in freezing conditions.
- **Full NetCDF/gridded meteorological ingestion.** Text time-series (uniform or zone-based) only in v1; architecture designed so a NetCDF reader is a drop-in replacement for `load_met_file()` with no downstream changes.
- **Variable water density** — constant `ρ_w` used in v1 (A18). Deferred as a properly-scoped future piece of work rather than a quick patch: should combine temperature (HEC-RAS Eq. 2.3), salinity, and suspended-sediment concentration in a single combined formulation, since the model will have (or already has) the underlying data for all three (grain-size/suspended-load tracking already exists; salinity becomes relevant once tidal/estuarine reaches are modelled, and the existing oil-spill code's `ro_water` constant is a precedent for salinity-influenced density mattering here). Implementing only the temperature term now would need reworking once salinity/sediment effects are added, so this is deliberately left as one combined future task.
- **Coupling of energy-balance latent heat to the hydraulic water balance** (`evaporate()`) — kept decoupled (A13a).

**Not deferred by design — currently blocking, tracked in `TEMP_V1_Implementation_Status.md` §3:** a severe runaway-temperature bug found via a controlled test (both schemes affected, both shallow and deep cells affected) is unresolved as of this revision. A related but secondary shallow-cell numerical stability issue (explicit forward-Euler with no subcycling in the full scheme) has an identified root cause and a designed-but-unimplemented fix. Neither of these is a scoping decision; both are open defects. See the status document for full detail before treating either scheme as production-ready.

---

## 8. GUI additions (new "Water Quality" tab)

- Master checkbox: "Simulate water temperature"
- Scheme selector: radio buttons "Full energy balance" / "Simplified (equilibrium temperature)"
- Sub-option checkboxes (full scheme only): "Use HEC-RAS default longwave (cloud-only)" vs "Use humidity-aware longwave [extension]"; "Use HEC-RAS default reflection coefficient" vs "Use fixed albedo + sediment adjustment [extension]"
- Initial condition: constant value box, or "load raster" file box (mirrors `bedrockbox` pattern)
- Met input file boxes: air temperature, shortwave, wind speed, humidity (optional), cloud cover (optional), pressure (optional), dew point (required only if simplified scheme chosen)
- Met time step box, met-zonation raster file box (mirrors rainfall-zonation controls)
- Site constants: latitude, longitude, time zone, elevation, wind measurement height
- Thermal update interval (minutes)
- Diffusivity ratio, albedo/sediment coefficients — advanced/calibration group box, defaulted from literature values so most users never need to touch them
- **Added during verification**: sensible/latent heat formulation group box — radio buttons for bundled (default) vs. separated formulation, a Richardson-correction checkbox (disabled and forced on when separated is selected, since no sourced variant of the literal HEC-RAS equations omits it), and two coefficient-triplet rows (bundled a/b/c, separated a/b/c), each enabled only when its formulation is active
- Output/visualisation: new entry in top-graphics menu and `comboBox1`, new save-option checkbox

---

## 9. Phased implementation order

1. **Data structures + text met loader + GUI scaffolding** (no thermodynamics yet) — get `water_temp` allocated, initial-condition loading working, met files loading into arrays, and a visualisation/output pathway in place, all populated with a placeholder constant so the plumbing can be tested end-to-end first.
2. **Simplified (equilibrium temperature) scheme** — smallest input burden, analytically stable, fastest to validate against hand-calculated values, and the recommended first "real" deliverable.
3. **Advection/mixing** (§3.4) — wire into the hydraulic loop, test with the simplified scheme active so the whole pipeline (inflow temperatures, mixing, energy exchange) is validated before the more complex full scheme is added.
4. **Full energy balance**, HEC-RAS-default sub-options only (solar-altitude `R_s`, cloud-only longwave) — validate against hand/spreadsheet calculations of the HEC-RAS equations for a simple test case.
5. **CAESAR-extension sub-options** (humidity-aware longwave, sediment-adjusted albedo) as additional selectable modes.
6. **Oil-spill integration** — swap `oil_T` constant for live `water_temp`.
7. **Documentation pass** — user-facing notes on all deferred items (§7), required inputs, and defaults.

---

*End of specification. Once confirmed, implementation will proceed file-by-file/function-by-function following §6, in the phase order given in §9.*
