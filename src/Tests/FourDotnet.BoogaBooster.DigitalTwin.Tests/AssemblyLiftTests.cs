using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>The deterministic vertical lift: profile, lifecycle, continuity and the force envelope.</summary>
public sealed class AssemblyLiftTests
{
    private static readonly double Dt = TestHelpers.Dt.TotalSeconds;

    private static readonly double ReturnTicks =
        Math.Ceiling(RideParameters.LiftPeriodSeconds / Dt) + 2d;

    private static readonly double CycleTicks =
        Math.Ceiling(RideParameters.LiftPeriodSeconds / Dt);

    private static AssemblyLift RunningLift(int ticks)
    {
        var lift = new AssemblyLift();
        lift.BeginRunning();
        for (var i = 0; i < ticks; i++)
        {
            lift.Advance(Dt);
        }

        return lift;
    }

    [Fact]
    public void A_new_lift_rests_at_neutral()
    {
        var lift = new AssemblyLift();
        lift.Advance(Dt);

        Assert.Equal(LiftPhase.Neutral, lift.Phase);
        Assert.Equal(0d, lift.Height);
        Assert.Equal(0d, lift.Velocity);
        Assert.Equal(0d, lift.Acceleration);
    }

    [Fact]
    public void Beginning_to_run_starts_without_a_jump()
    {
        var lift = new AssemblyLift();
        lift.BeginRunning();
        lift.Advance(Dt);

        Assert.Equal(LiftPhase.Running, lift.Phase);
        Assert.InRange(lift.Height, 0d, 1e-3d);
        Assert.InRange(lift.Velocity, 0d, 1e-2d);
    }

    [Fact]
    public void Running_stays_within_neutral_and_stroke_and_reaches_the_top_at_half_period()
    {
        var lift = new AssemblyLift();
        lift.BeginRunning();
        var max = 0d;
        for (var i = 0; i < CycleTicks * 2; i++)
        {
            lift.Advance(Dt);
            Assert.InRange(lift.Height, -1e-9d, RideParameters.LiftStrokeMeters + 1e-9d);
            max = Math.Max(max, lift.Height);
        }

        Assert.Equal(RideParameters.LiftStrokeMeters, max, 3);
    }

    [Fact]
    public void Running_peak_acceleration_matches_the_closed_form_and_the_envelope()
    {
        var lift = new AssemblyLift();
        lift.BeginRunning();
        var peak = 0d;
        for (var i = 0; i < CycleTicks; i++)
        {
            lift.Advance(Dt);
            peak = Math.Max(peak, Math.Abs(lift.Acceleration));
        }

        var expected = RideParameters.LiftStrokeMeters / 2d * Math.Pow(RideParameters.LiftAngularFrequency, 2d);
        Assert.Equal(expected, peak, 3);
        Assert.True(peak / RideParameters.Gravity <= RideParameters.LiftMaxAccelerationG);
    }

    [Fact]
    public void Identical_runs_produce_identical_trajectories_at_every_tick()
    {
        var a = new AssemblyLift();
        var b = new AssemblyLift();
        a.BeginRunning();
        b.BeginRunning();

        for (var i = 0; i < 3000; i++)
        {
            if (i == 1000)
            {
                a.BeginReturning();
                b.BeginReturning();
            }

            a.Advance(Dt);
            b.Advance(Dt);
            Assert.Equal(a.Height, b.Height);
            Assert.Equal(a.Velocity, b.Velocity);
            Assert.Equal(a.Acceleration, b.Acceleration);
            Assert.Equal(a.Phase, b.Phase);
        }
    }

    [Fact]
    public void Position_and_velocity_are_continuous_through_a_whole_run_and_return()
    {
        var lift = new AssemblyLift();
        lift.BeginRunning();
        var maxJumpPerTick = (RideParameters.LiftStrokeMeters / 2d * RideParameters.LiftAngularFrequency * Dt) * 1.01d;
        var maxVelocityJump = RideParameters.LiftMaxAccelerationG * RideParameters.Gravity * Dt * 1.01d;
        var previousHeight = 0d;
        var previousVelocity = 0d;

        for (var i = 0; i < CycleTicks * 1.37d; i++)
        {
            lift.Advance(Dt);
            Assert.InRange(Math.Abs(lift.Height - previousHeight), 0d, maxJumpPerTick);
            Assert.InRange(Math.Abs(lift.Velocity - previousVelocity), 0d, maxVelocityJump);
            previousHeight = lift.Height;
            previousVelocity = lift.Velocity;
        }

        lift.BeginReturning();
        for (var i = 0; i < ReturnTicks; i++)
        {
            lift.Advance(Dt);
            Assert.InRange(Math.Abs(lift.Height - previousHeight), 0d, maxJumpPerTick);
            Assert.InRange(Math.Abs(lift.Velocity - previousVelocity), 0d, maxVelocityJump);
            previousHeight = lift.Height;
            previousVelocity = lift.Velocity;
        }

        Assert.Equal(LiftPhase.Neutral, lift.Phase);
        Assert.Equal(0d, lift.Height);
        Assert.Equal(0d, lift.Velocity);
        Assert.Equal(0d, lift.Acceleration);
    }

    [Fact]
    public void Returning_while_neutral_is_a_no_op()
    {
        var lift = new AssemblyLift();
        lift.BeginReturning();

        Assert.Equal(LiftPhase.Neutral, lift.Phase);
    }

    [Fact]
    public void Beginning_to_run_again_while_returning_is_ignored()
    {
        var lift = RunningLift(200);
        lift.BeginReturning();
        lift.BeginRunning();

        Assert.Equal(LiftPhase.Returning, lift.Phase);
    }

    [Fact]
    public void An_interrupted_return_never_exceeds_the_envelope_and_always_reaches_neutral()
    {
        var limit = RideParameters.LiftMaxAccelerationG * RideParameters.Gravity;

        for (var interruptTick = 1; interruptTick <= CycleTicks; interruptTick += 7)
        {
            var lift = RunningLift(interruptTick);
            var startHeight = lift.Height;
            var startVelocity = lift.Velocity;
            lift.BeginReturning();

            var peak = 0d;
            for (var i = 0; i < ReturnTicks; i++)
            {
                lift.Advance(Dt);
                peak = Math.Max(peak, Math.Abs(lift.Acceleration));
                Assert.InRange(lift.Height, -0.01d, RideParameters.LiftStrokeMeters + 0.01d);
            }

            Assert.True(peak <= limit, $"tick {interruptTick} (z0={startHeight}, v0={startVelocity}): {peak} > {limit}");
            Assert.Equal(LiftPhase.Neutral, lift.Phase);
        }
    }

    [Fact]
    public void A_ride_enters_the_lift_on_start_and_returns_it_when_stopping()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        Assert.Equal(LiftPhase.Running, ride.Mill.Lift.Phase);

        for (var i = 0; i < 600; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        Assert.True(ride.Mill.Lift.Height > 0d);

        ride.RequestTransition(RideState.Stopping);
        Assert.Equal(LiftPhase.Returning, ride.Mill.Lift.Phase);

        // The mill is at rest almost at once, but riders only unload at neutral height.
        for (var i = 0; i < 200; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        Assert.Equal(RideState.Stopping, ride.CurrentState);

        for (var i = 0; i < ReturnTicks; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        Assert.True(ride.Mill.Lift.IsNeutral);
        Assert.NotEqual(RideState.Stopping, ride.CurrentState);
    }

    [Fact]
    public void An_emergency_stop_mid_cycle_returns_the_lift_to_neutral()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        for (var i = 0; i < 500; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        ride.RequestTransition(RideState.EmergencyStop);
        for (var i = 0; i < ReturnTicks + 10; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        Assert.True(ride.Mill.Lift.IsNeutral);
        Assert.Equal(0d, ride.Mill.Lift.Height);
    }
}
