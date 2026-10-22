# Spec Delta

## Purpose

Defines the ride assembly's deterministic vertical translation, including its operating lifecycle and the resulting physical effects on riders. The capability keeps vertical movement part of the ride experience rather than an unrelated visual animation.

## ADDED Requirements

### Requirement: Whole assembly moves vertically as one structure
While a ride is running, the system SHALL move the mill, hubs, gondolas, and seats together along the world vertical axis without changing their relative positions or orientations because of the translation.

#### Scenario: Vertical movement preserves nested geometry
- **WHEN** the ride assembly changes vertical position during a run
- **THEN** every ride component receives the same vertical displacement
- **AND** the relative positions and orientations of the mill, hubs, gondolas, and seats remain unchanged by that displacement

### Requirement: Vertical ride motion is smooth and bounded
The system SHALL apply a smooth, bounded vertical motion profile during a run and SHALL transition into and out of that profile without discontinuities in assembly position or velocity.

#### Scenario: Ride enters its running motion
- **WHEN** the ride enters its running state
- **THEN** vertical movement begins without an instantaneous change in assembly position or velocity

#### Scenario: Ride leaves its running motion
- **WHEN** the ride leaves its running state
- **THEN** vertical movement returns the assembly to its neutral position without an instantaneous change in assembly position or velocity

### Requirement: Vertical motion is deterministic
The system SHALL advance vertical motion using the same fixed physics timestep and explicit simulation state as the rest of the ride, so identical initial state and inputs produce identical vertical trajectories.

#### Scenario: Repeated simulation produces the same vertical trajectory
- **WHEN** two simulations start from identical state and receive identical inputs for the same number of physics ticks
- **THEN** their assembly vertical position, velocity, and acceleration are identical at every tick

### Requirement: Vertical acceleration contributes to rider forces
The system SHALL include assembly vertical acceleration when calculating seat specific force and SHALL subject the resulting rider forces to the existing ride safety limits.

#### Scenario: Rider force reflects vertical acceleration
- **WHEN** the assembly has non-zero vertical acceleration
- **THEN** each seat's calculated vertical specific force reflects that acceleration in addition to gravity and the existing rotational acceleration

#### Scenario: Existing force safety limit is reached
- **WHEN** combined rotational and vertical motion causes a seat force to reach the configured safety limit
- **THEN** the existing safety response reduces drive power or stops the ride as specified by ride safety behavior