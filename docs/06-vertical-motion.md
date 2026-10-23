# 6. Vertical Motion — The Whole Assembly Rises and Falls

The three nested rotations all happen in the horizontal plane. This document adds **one
vertical degree of freedom**: the entire assembly (mill, hubs, carts, seats) translates
along the world vertical axis `z` (up is positive) as one rigid structure. Nested local
transforms are untouched; the height is composed into world-space positions only.

---

## 6.1 State and lifecycle

The authoritative state is `(z, ż, z̈, phase)`, advanced on the fixed `dt = 1/120 s` tick
(doc 1). There is no wall clock: the trajectory is a pure function of the tick count.

| Phase | Meaning | Entered when |
|-------|---------|--------------|
| `Neutral` | at rest at neutral height, `z = ż = z̈ = 0` | initially; after a return completes |
| `Running` | periodic rise-and-fall | the ride enters `Started` |
| `Returning` | finishing the cosine cycle down to neutral | the ride enters `Stopping` or `EmergencyStop` |

The ride does not move from `Stopping`/`EmergencyStop` to `Offloading` until the mill is
at rest **and** the lift is back at `Neutral`, so riders never unload from a moving platform.

## 6.2 Running profile

```
z(τ) = (A/2)·(1 − cos ωτ)      ω = 2π/T
ż(τ) = (A/2)·ω·sin ωτ
z̈(τ) = (A/2)·ω²·cos ωτ
```

`z ∈ [0, A]`. At `τ = 0` the profile is at the bottom with `ż = 0`, so **entry is
continuous in position and velocity** (no jump when the ride starts). `τ` is advanced
by `ω·dt` per tick and wrapped to `[0, 2π)`.

Parameters (`RideParameters`): stroke `A = 1.5 m`, period `T = 8 s`
(`ω = 0.7854 rad/s`).

Peak running acceleration: `(A/2)·ω² = 0.75 × 0.6169 = 0.463 m/s² = 0.047 g`.

## 6.3 Return to neutral (including interrupted cycles)

When the run ends — normally or part-way through a cycle — the lift does **not** restart
or blend: it keeps following the same cosine until the cycle reaches its bottom
(`ωτ = 2π`), where `z = ż = 0` exactly, and then settles to `Neutral`. Because the state
is never replaced, position and velocity stay continuous at the interruption and at
arrival, the height never drops below neutral, and the acceleration stays inside the
running envelope (`≤ 0.047 g`). The return therefore takes at most one period `T = 8 s`
(a blended return was rejected: a cubic Hermite glide from a descending state undershoots
below neutral). If the lift never left neutral, it settles immediately; at `Neutral`
the call is a no-op.

## 6.4 World-space composition

Assembly height `z` is added to the world `z` of every ride point — mill axis, hub
centres, cart pivots, seats. Horizontal coordinates and all relative offsets are
unchanged, so every component receives the identical displacement `Δz`.

## 6.5 Effect on rider force

The assembly's vertical coordinate acceleration `z̈` is added to every seat's world
acceleration *before* subtracting gravity (doc 5 §5.1):

```
a = a_horizontal + (0, 0, z̈)        f = a − g = (a_horizontal, z̈ + 9.81)
G_vertical = (9.81 + z̈) / 9.81       G_felt = √(G_forward² + G_lateral² + G_vertical²)
```

At rest `z̈ = 0`, so `G_vertical = 1.0` and `G_felt = 1.000` — the rest sensation is
preserved. The same `G_max = 4.5 g` limit applies to the combined `G_felt` (`Gondola.FeltG`,
`Gondola.ExceedsGForceLimit`, `GreatMill.PeakFeltG`/`IsOverG`); there is no separate lift
limit.

## 6.6 Force envelope

Lift adds at most `LiftMaxAccelerationG = 0.10 g` to the vertical specific force, so
`G_vertical ∈ [0.90, 1.10]`. Because it is added in quadrature to the horizontal load,
the effect on the total is `≈ (G_v² − 1)/(2·G_h)`: even at `G_h = 4.4 g` it moves the
total by under `0.03 g`. The chosen peak (0.047 g, running and returning alike) stays
inside the budget; tests assert this for every interruption point.

---

*Back to the [index](README.md). Default numeric parameters:
[appendix-parameters.md](appendix-parameters.md).*
