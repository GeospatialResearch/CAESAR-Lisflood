


Note that the HEC-ResSim Water Quality User's Manual has the Richardson number equation as:

$$R_i = -\frac{2.0 \cdot g(\rho_{\text{air}}-\rho_{\text{sat}})}{\rho_{\text{air}}u_w^2}$$

Source: equation 14, https://www.hec.usace.army.mil/confluence/ResSimDocs/rswqum/appendix-a-technical-background-for-water-quality-modules

We have this defined as:
```csharp
g * (rho_air - rho_sat) / (rho_air * windSpeed2 * windSpeed2);
```

I think this needs modifying to:
```csharp
-(2.0 * g * (rho_air - rho_sat) / (rho_air * windSpeed2 * windSpeed2));
```
please check and verify.

The piecewise Richardson stability function for a stable atmosphere where $[0.01 \le R_i \lt 2]$: 

$$f(R_i) = (1 - 34Ri)^{-0.8}$$

(equation 16d). This is implemented as:
```csharp
if (Ri < 2.0) return Math.Pow(1.0 - 34.0 * Ri, -0.8);
```
which is correct, therefore no need for the suggested sign change.

However, if I am right, we will already invert the sign direction throught the Richardson equation correction above.

If I implement the correction to the Richardson number equation, this is what I get:
```pwsh
this.richardson_number(15, 20, 2, 1013.25, 70)
-0.1042774012467334
this.richardson_number(25, 20, 2, 1013.25, 70)
0.081372338827963547

this.sensible_heat_flux(15, 20, 2, -0.1042774012467334)  
-0.046699835763326716
this.sensible_heat_flux(25, 20, 2, 0.081372338827963547)
NaN
```

So the sign for $R_i$ is inverted, but we still have an issue with the sensible_heat_flux.

According to equation 11 in the user's manual, this is "typically" computed as:

$$
q_h = \left(\frac{K_h}{K_w}\right) C_p \rho_w (T_a - T_w) f(u_w)
$$

where:
* **$q_h$** : Sensible heat flux density
* **$K_h$** : Turbulent transfer coefficient for heat
* **$K_w$** : Turbulent transfer coefficient for water vapor
* **$C_p$** : Specific heat capacity at constant pressure
* **$\rho_w$** : Density of water (or fluid medium)
* **$T_a$** : Atmospheric temperature (air temperature)
* **$T_w$** : Water surface temperature
* **$f(u_w)$** : Wind speed function dependent on wind velocity $u_w$

Our implementation is:
```csharp
diffusivityRatio * Cp_air * rho_s * (airTempC - waterTempC) * f_us;
```
where the diffusivity ratio is fixed at 1.0. This looks reasonable and I think the implementation is correct. So why the NaN for a positive $R_i$?

Working through ```sensible_heat_flux()```:

```double Tw_K = waterTempC + 273.15;```

Tw_K = 293.15

```double es_water = saturation_vapour_pressure(waterTempC);```

es_water = 23.357286626396672

```double rho_s = air_density(1013.25, es_water, Tw_K);```

rho_s = 1.1936261120293761

```double f_us = wind_function(windSpeed2, Ri);```

f_us = NaN

Therefore the the issue is within ```wind_function()```. 

Stepping into this function:
```csharp
        double wind_function(double windSpeed2, double Ri)
        {
            double fRi = richardson_stability_function(Ri);
            return fRi * Math.Pow(windFunc_a + windFunc_b * windSpeed2, windFunc_c);
        }
```
called with windSpeed2 = 2.0 and Ri = 0.081372338827963547

```double fRi = richardson_stability_function(Ri);```

fRi = NaN

which will of course propogate onwards. Now stepping into ```richardson_stability_function()```:

```csharp
        double richardson_stability_function(double Ri)
        {
            if (Ri <= -1.0) return 12.3;
            if (Ri <= -0.01) return Math.Pow(1.0 - 22.0 * Ri, 0.8);
            if (Ri < 0.01) return 1.0;
            if (Ri < 2.0) return Math.Pow(1.0 - 34.0 * Ri, -0.8);
            return 0.03;
        }
```

As expected, this falls through to:

```return Math.Pow(1.0 - 34.0 * Ri, -0.8);```

1.0 - 34.0 * Ri = -1.7666595201507604

So we have a negative number and taking a power will lead to a complex number, which cannot be stored as a double and results in NaN.

Maybe we can reformulate ```richardson_stability_function``` to handle this:

```csharp
        double richardson_stability_function(double Ri)
        {
            if (Ri <= -1.0) return 12.3;
            if (Ri <= -0.01) return Math.Pow(1.0 - 22.0 * Ri, 0.8);
            if (Ri < 0.01) return 1.0;
            if (Ri < 2.0) {
                double baseValue = 1.0 - 34.0 * Ri;

                // Get the sign (-1 or 1) and calculate using the absolute (positive) base
                double sign = Math.Sign(baseValue);
                double result = Math.Pow(Math.Abs(baseValue), -0.8);

                // Reapply the sign if your formula dictates a negative base yields a negative result
                return sign * result;
                //return Math.Pow(1.0 - 34.0 * Ri, -0.8);
            }            
            return 0.03;
        }
```

Walking through this with the same inputs:

baseValue = -1.7666595201507604

as expected.

sign = -1

result = 0.63427517461495475

Therefore the value returned is:

-0.63427517461495475

Now:
```pwsh
this.sensible_heat_flux(25, 20, 2, 0.081372338827963547)
-0.011413092715283461
```

and:
```pwsh
this.sensible_heat_flux(30, 20, 2, this.richardson_number(30, 20, 2, 1013.25, 70))
-0.0097776967290307357
this.sensible_heat_flux(25, 20, 2, this.richardson_number(25, 20, 2, 1013.25, 70))
-0.011413092715283461
this.sensible_heat_flux(20, 20, 2, this.richardson_number(20, 20, 2, 1013.25, 70))
0
this.sensible_heat_flux(20, 25, 2, this.richardson_number(20, 25, 2, 1013.25, 70))
-0.047145841189387486
this.sensible_heat_flux(20, 30, 2, this.richardson_number(20, 30, 2, 1013.25, 70))
-0.13666690082625904
```

So always negative except where zero when the temperatures are the same. This doesn't feel correct.

Can you please verify:
1. The reformulation of Richardson number equation
2. The appropriateness of the sign flipping done for the power calculation in richardson_stability_function

