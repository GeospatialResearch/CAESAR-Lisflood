# Water Temperature Module (feature branch)

## Overview

This branch adds a water temperature simulation capability to CAESAR-Lisflood, building on the existing oil-spill flow implementation ([PDF1994/CAESAR-LISFLOOD-Oil-spill-implementation](https://github.com/PDF1994/CAESAR-LISFLOOD-Oil-spill-implementation)).

Currently, water temperature in the oil-spill module is a hardcoded constant. This branch replaces that with a dynamic, spatially- and temporally-varying water temperature driven by meteorological forcing (air temperature, solar radiation, wind speed, humidity, cloud cover, atmospheric pressure). This is intended as foundational infrastructure for future water-quality work (nitrate and biological contaminant modules), which will require water temperature for kinetic-rate correction.

## Method and benchmark

The thermodynamic formulation is based on the HEC-RAS Nutrient Simulation Module (NSM) Water Temperature Simulation Module (TEMP), as documented in:

> Zhang, Z. and Johnson, B.E. (2016). *Aquatic Nutrient Simulation Modules (NSMs) Developed for Hydrologic and Hydraulic Models.* ERDC/EL TR-16-1, Section 2. U.S. Army Engineer Research and Development Center.

Two selectable schemes are implemented, matching the structure of the reference documentation:

- **Full energy balance** — shortwave radiation, atmospheric longwave, back longwave, latent heat, and sensible heat, each computed from meteorological forcing, with an optional Richardson-number atmospheric stability correction on the turbulent wind function.
- **Simplified (equilibrium temperature) balance** — a closed-form, low-input-burden alternative requiring only dew point, wind speed, and shortwave radiation.

Default parameterisation matches the HEC-RAS reference equations exactly, so results can be checked against that benchmark. A small number of CAESAR-specific extensions (e.g. a suspended-sediment-adjusted albedo option) are available as selectable alternatives, clearly distinguished from the HEC-RAS defaults.

## Scope of this branch

**Included:**
- New `water_temp[x,y]` state variable, advected/mixed with flow using the same source-tracing infrastructure used for water-source and solute tracers.
- Text-file meteorological forcing (uniform or zone-based, following the existing rainfall-zonation pattern).
- Full and simplified energy balance schemes, user-selectable.
- Integration with the oil-spill module (replacing the current hardcoded temperature constant).
- New GUI tab for water-quality/temperature controls, met input file selection, and initial conditions.

**Explicitly deferred (documented, not implemented here):**
- Gridded/NetCDF meteorological forcing (text time series only for now; the internal interface is designed so this can be added later without reworking downstream code).
- Bed/sediment heat exchange.
- Ice formation (temperature is floored at 0 °C).
- Variable water density (a constant value is used; a future upgrade should combine temperature, salinity, and suspended-sediment effects together rather than as separate patches).
- Coupling of the new energy-balance latent heat term to the existing hydraulic evaporation term.

## Status

Design and equations finalised (see `CAESAR-Lisflood_Water_Temperature_Module_Spec.md`). Implementation in progress, following a skeleton-first, incremental approach: data structures and empty function stubs are added first, compiled and tested, then filled in function by function.

## Related documents

- `CAESAR-Lisflood_Water_Temperature_Module_Spec.md` — full technical specification: assumptions, equations, data structures, integration points, and phased implementation order.
