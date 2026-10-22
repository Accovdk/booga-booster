# Proposal

## Why

The ride currently rotates entirely in the horizontal plane, so the visitor never experiences the whole attraction rising or falling. Adding a controlled vertical ride motion will give the simulation a more dynamic movement profile while preserving the existing nested rotation experience.

## What Changes

- Add deterministic vertical translation of the complete ride assembly during the running portion of a ride; the mill, hubs, gondolas, and seats move together while retaining their relative geometry.
- Smoothly transition the assembly into and out of its vertical motion, and include the resulting acceleration in rider-force and safety calculations.
- Include the assembly's vertical position in telemetry so the dashboard's 3D ride visualization follows the motion.

## Capabilities

### New Capabilities

- `engine-vertical-motion`: Defines the ride assembly's vertical motion profile, lifecycle, determinism, and physical effects.

### Modified Capabilities

- `ride-telemetry-streaming`: Add the assembly's vertical position to the full telemetry snapshot for visualization.

## Impact

- DigitalTwin physics state, kinematics, seat-force calculations, safety limits, and physics parameter documentation.
- Ride telemetry contract and the SSE-backed Angular 3D visualization.
- Focused backend physics/telemetry tests and frontend visualization tests.
