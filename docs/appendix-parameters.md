# Appendix — Default Parameters

The starting configuration for the physics engine. These are tuned to give coherent,
playable numbers — a believable big flat-ride — not to model a specific real machine.
Tune to taste; every one of them should be a named constant / config value, never a magic
number buried in the physics.

## Masses & geometry

| Parameter | Symbol | Default | Unit |
|-----------|--------|--------:|------|
| Passenger mass | `m_pax` | 75 | kg |
| Empty cart mass | `m_cart` | 150 | kg |
| Hub structure mass | `m_hub` | 400 | kg |
| Mill arm mass | `m_arm` | 800 | kg |
| Mill arm length | `L_mill` | 6 | m |
| Hub arm length | `L_hub` | 2 | m |
| Seat offset | `r_seat` | 0.5 | m |
| Pivot→CoM offset (empty cart) | `r_g0` | 0.35 | m |
| Cart yaw inertia (empty, about pivot) | `I_cart_cm` | 220 | kg·m² |

## Dynamics & drive

| Parameter | Symbol | Default | Unit |
|-----------|--------|--------:|------|
| Timestep | `dt` | 1/120 | s |
| Telemetry rate | — | 30 | Hz |
| Vertical lift amplitude | `A` | 0.6 | m |
| Vertical lift frequency | `f` | 0.2 | Hz |
| Lift entry/exit transition | `T` | 4 | s |
| Maximum vertical coordinate acceleration | — | 0.2 | g |
| Mill stall torque | `τ_stall` | 60,000 | N·m |
| Mill max power | `P_max` | 90 | kW |
| Cart pivot damping | `c` | 900 | N·m·s/rad |
| Gravity | `g` | 9.81 | m/s² |

## Safety limits

| Parameter | Symbol | Default | Unit |
|-----------|--------|--------:|------|
| Max allowed G | `G_max` | 4.5 | g |
| Over-speed cap (mill) | `ω_max` | 2.5 | rad/s |
| Imbalance dispatch limit | — | 50 | mm |
| Wind cutoff | `windMax` | 18 | m/s |
| E-stop decel ramp | — | 8 | s |

The lift multiplies `A·sin(2πft)` by a quintic smootherstep envelope
`S(u)=6u⁵−15u⁴+10u³` during entry and by `1−S(u)` during return to neutral,
where `u=t/T`. The oscillator phase is explicit simulation state and advances only
on fixed physics ticks. It continues through each envelope so position and velocity
match at the Entering/Running and Running/Exiting phase boundaries. If stopping
interrupts entry, a quintic Hermite return matches the current envelope, rate, and
acceleration before settling at zero. Transition completion is quantized to the
nearest fixed physics tick.

For the selected profile, a conservative analytic bound on the full entry/exit
coordinate acceleration (including envelope acceleration and cross terms) is:

```
|a_vertical| ≤ A · (5.773503/T² + 2·1.875·(2πf)/T + (2πf)²)
             = 1.870847 m/s² = 0.190708 g < 0.2 g
```

This is the bound on the **lift contribution**, not the total seat load. The rider's
resultant specific force combines this vertical contribution with the existing
horizontal rotational force and gravity, then uses the existing 4.5 g protection.
The DigitalTwin tests sweep every fixed-step interruption point during entry and the
corresponding return ticks against this bound. Therefore the 0.2 g lift allowance
does not relax the combined-load safety limit.

## Loss-model coefficients

Not fixed by the plan — picked to give the sanity-check behaviour below and locked in as the
golden-scenario baseline. The coefficients are sized against each body's rotating inertia so
a de-powered rig coasts to rest in a believable time (doc 3 §3.4); the increase is biased
toward Coulomb/viscous friction (which govern the low-speed tail) over aerodynamic drag
(which mostly sets terminal speed).

| Parameter | Symbol | Role | Mill | Hub |
|-----------|--------|------|-----:|----:|
| Coulomb friction | `C_coulomb` | constant bearing drag (sets a clean, bounded-time stop) | 10,000 | 400 |
| Viscous friction | `C_viscous` | speed-proportional bearing drag | 3,500 | 500 |
| Aerodynamic drag | `k_aero` | ω² air resistance (sets terminal speed) | 6,000 | 200 |
| Motor speed floor | `ω_eps` | ~1e-3 rad/s, divide-by-zero guard (doc 3 §3.1) | 1e-3 | 1e-3 |

Units: `C_coulomb` in N·m, `C_viscous` in N·m·s/rad, `k_aero` in N·m·s²/rad².

## Sanity check

With these parameters, a **full ride** (~2.4 t of carts + people orbiting at 6 m):

- has a mill moment of inertia around **2×10⁵ kg·m²**,
- reaches cruise in **~15–25 s** on 90 kW,
- settles at a natural terminal speed of **~2.1 rad/s** — a little below the `ω_max = 2.5`
  safety cap, because the losses (dominated by aero drag at cruise) balance the motor
  before the cap; the cap is a ceiling, not the operating point,
- produces roughly **~2 g** at the seats at that terminal speed,
- and, once power is cut, **coasts to rest in ~30 s empty / ~50 s fully loaded** (the loaded
  rig carries ~2.5× the inertia, so it coasts longer).

If your numbers land far outside those ranges, something in the inertia model (doc 2) or
the motor/loss balance (doc 3) is off.
