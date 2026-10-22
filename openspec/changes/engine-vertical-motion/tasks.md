# Tasks

## 1. Deterministic Vertical Motion

- [ ] 1.1 Derive and document lift amplitude, frequency, and transition timing in the physics parameters and `docs/`; verify the peak vertical acceleration stays within the documented force envelope.
- [ ] 1.2 Add vertical position, velocity, acceleration, and lifecycle phase to simulation state; implement bounded fixed-timestep motion and smooth entry/return-to-neutral behavior, with deterministic replay and continuity tests.

## 2. Kinematics and Rider Safety

- [ ] 2.1 Apply assembly height to world-space positions without changing nested geometry; verify every component receives the same vertical displacement.
- [ ] 2.2 Include lift acceleration in seat specific-force calculations and the existing safety path; verify rest remains 1.0 g and tests cover combined-load threshold behavior.

## 3. Telemetry and Visualization

- [ ] 3.1 Add assembly world-vertical position to the telemetry contract and snapshot; verify serialized SSE frames include the current height.
- [ ] 3.2 Apply streamed assembly height as a world-space offset in the 3D visualization; verify the ride moves with telemetry and remains at neutral height when height is absent.

## 4. Integration Verification

- [ ] 4.1 Run the DigitalTwin and frontend focused tests, verify changed backend libraries retain at least 80% line coverage, and run the solution build and frontend test suite.
