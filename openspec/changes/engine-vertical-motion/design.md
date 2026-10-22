# Design

## Context

See proposal.md for motivation. The physics tick is currently deterministic at 1/120 s, while ride geometry is composed from nested planar rotations. Seat force calculations assume no translational acceleration of the rotating assembly, and telemetry snapshots do not expose an assembly pose.

## Goals / Non-Goals

**Goals:**

- Add one vertical degree of freedom to the ride assembly without changing the nested rotational model.
- Make vertical motion deterministic, lifecycle-aware, bounded by documented parameters, and visible to rider-force and safety calculations.
- Expose the ride assembly height through the existing telemetry stream for the 3D visualization.

**Non-Goals:**

- Add independent vertical motion to hubs, gondolas, or seats.
- Add operator controls for lift amplitude, speed, or direction.
- Change the telemetry transport or introduce a separate animation source.

## Decisions

### Model vertical movement in the simulation

Add assembly height, vertical velocity, and vertical acceleration to the authoritative simulation state. Compose the assembly height into each world-space ride position while leaving relative local transforms unchanged. This keeps rendered geometry, computed forces, and telemetry tied to one source of truth. A frontend-only animation was rejected because it would look correct while producing incorrect physical forces.

### Drive motion with deterministic lifecycle phases

Advance a bounded periodic vertical profile using fixed-timestep simulation state and parameters in `RideParameters`. The running phase produces the repeated rise-and-fall motion; entry and exit phases transition smoothly from and back to the neutral height, including when stopping interrupts a cycle. Use explicit phase state rather than wall-clock time so replay and tests remain deterministic. Profile amplitude, frequency, and transition timing must be documented and selected so the motion remains within the ride's vertical-force safety envelope.

### Include lift acceleration in the existing force calculation

Add the assembly's vertical coordinate acceleration to each seat's existing world-acceleration vector before subtracting gravity and deriving specific force. Feed the resulting values through the existing force and safety path rather than creating a separate lift-only limit. This preserves a single passenger-safety decision based on the combined ride motion.

### Extend the existing telemetry snapshot

Add the assembly's world-vertical position to the full telemetry contract and update the dashboard renderer to apply it as a world-space offset to the complete ride model. Keep the existing SSE stream and shared telemetry store; a second animation loop or client-side simulation was rejected because it could drift from backend physics.

## Risks / Trade-offs

- **Vertical acceleration may combine with rotational load to exceed the existing safety threshold** → Derive profile limits against combined seat-force calculations, cover peak and transition cases with deterministic tests, and retain the existing safety response.
- **Lifecycle transitions can cause a visible or physical jerk if the profile is reset abruptly** → Store the motion phase explicitly and test position and velocity continuity when entering, stopping, and returning to neutral.
- **Rendering and telemetry may appear one sample behind the physics tick** → Publish the height in the same consistent snapshot as other telemetry and apply it in the existing frame update.

## Migration Plan

No persisted data or new endpoint is required. Add the telemetry property compatibly, then update the dashboard to consume it; an older frontend can ignore the additional field. On rollback, the renderer can fall back to neutral height when the property is absent, and the backend can disable the vertical profile without changing the SSE transport.
