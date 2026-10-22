using System.Text;
using System.Text.Json;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class VerticalMotionTests
{
    [Fact]
    public void The_full_entry_and_exit_profile_stays_inside_its_documented_acceleration_bound()
    {
        var ride = CreateStartedRide();
        var maximumObservedAcceleration = 0d;
        var transitionSteps = (int)(RideParameters.VerticalMotionTransitionSeconds / TestHelpers.Dt.TotalSeconds);

        for (var i = 0; i < transitionSteps; i++)
        {
            ride.Advance(TestHelpers.Dt);
            maximumObservedAcceleration = Math.Max(
                maximumObservedAcceleration,
                Math.Abs(ride.AssemblyVerticalAccelerationMetersPerSecondSquared));
        }

        ride.RequestTransition(RideState.Stopping);
        for (var i = 0; i < transitionSteps; i++)
        {
            ride.Advance(TestHelpers.Dt);
            maximumObservedAcceleration = Math.Max(
                maximumObservedAcceleration,
                Math.Abs(ride.AssemblyVerticalAccelerationMetersPerSecondSquared));
        }

        Assert.True(
            RideParameters.VerticalMotionAccelerationBoundG < RideParameters.VerticalMotionAccelerationLimitG,
            $"Analytic bound {RideParameters.VerticalMotionAccelerationBoundG:F6} g must be below "
            + $"{RideParameters.VerticalMotionAccelerationLimitG:F3} g.");
        Assert.True(
            maximumObservedAcceleration <= RideParameters.VerticalMotionAccelerationBoundMetersPerSecondSquared,
            $"Observed acceleration {maximumObservedAcceleration:F6} m/s² exceeded the analytic bound "
            + $"{RideParameters.VerticalMotionAccelerationBoundMetersPerSecondSquared:F6} m/s².");
    }

    [Fact]
    public void Stops_interrupted_at_any_entry_tick_remain_continuous_and_inside_the_acceleration_bound()
    {
        var transitionSteps = (int)(RideParameters.VerticalMotionTransitionSeconds / TestHelpers.Dt.TotalSeconds);
        var maximumObservedAcceleration = 0d;

        for (var stopStep = 0; stopStep <= transitionSteps; stopStep++)
        {
            var motion = new VerticalMotion();
            motion.BeginRun();
            Advance(motion, stopStep);

            var positionBeforeStop = motion.PositionMeters;
            var velocityBeforeStop = motion.VelocityMetersPerSecond;
            var accelerationBeforeStop = motion.AccelerationMetersPerSecondSquared;
            motion.BeginReturnToNeutral();

            Assert.Equal(positionBeforeStop, motion.PositionMeters);
            Assert.Equal(velocityBeforeStop, motion.VelocityMetersPerSecond);
            Assert.Equal(accelerationBeforeStop, motion.AccelerationMetersPerSecondSquared);

            for (var i = 0; i < transitionSteps; i++)
            {
                motion.Advance(TestHelpers.Dt.TotalSeconds);
                maximumObservedAcceleration = Math.Max(
                    maximumObservedAcceleration,
                    Math.Abs(motion.AccelerationMetersPerSecondSquared));
            }
        }

        Assert.True(
            maximumObservedAcceleration <= RideParameters.VerticalMotionAccelerationBoundMetersPerSecondSquared,
            $"Interrupted transition acceleration {maximumObservedAcceleration:F6} m/s² exceeded the bound "
            + $"{RideParameters.VerticalMotionAccelerationBoundMetersPerSecondSquared:F6} m/s².");
    }

    [Fact]
    public void Identical_initial_state_and_fixed_ticks_replay_the_same_vertical_trajectory()
    {
        var first = CreateStartedRide();
        var second = CreateStartedRide();
        var stepsBeforeStop = 700;
        var stepsAfterStop = 500;

        for (var i = 0; i < stepsBeforeStop; i++)
        {
            first.Advance(TestHelpers.Dt);
            second.Advance(TestHelpers.Dt);
            AssertSameVerticalState(first, second);
        }

        first.RequestTransition(RideState.Stopping);
        second.RequestTransition(RideState.Stopping);

        for (var i = 0; i < stepsAfterStop; i++)
        {
            first.Advance(TestHelpers.Dt);
            second.Advance(TestHelpers.Dt);
            AssertSameVerticalState(first, second);
        }
    }

    [Fact]
    public void Entering_and_leaving_the_running_profile_preserves_position_and_velocity_continuity()
    {
        var ride = CreateStartedRide();
        Assert.Equal(0d, ride.AssemblyVerticalPositionMeters);
        Assert.Equal(0d, ride.AssemblyVerticalVelocityMetersPerSecond);

        Advance(ride, steps: 60);
        var positionBeforeStop = ride.AssemblyVerticalPositionMeters;
        var velocityBeforeStop = ride.AssemblyVerticalVelocityMetersPerSecond;

        ride.RequestTransition(RideState.Stopping);

        Assert.Equal(positionBeforeStop, ride.AssemblyVerticalPositionMeters);
        Assert.Equal(velocityBeforeStop, ride.AssemblyVerticalVelocityMetersPerSecond);
        ride.Advance(TestHelpers.Dt);
        Assert.True(Math.Abs(ride.AssemblyVerticalPositionMeters - positionBeforeStop) < 0.02d);
        Assert.True(Math.Abs(ride.AssemblyVerticalVelocityMetersPerSecond - velocityBeforeStop) < 0.05d);

        Advance(
            ride,
            steps: (int)(RideParameters.VerticalMotionTransitionSeconds / TestHelpers.Dt.TotalSeconds) - 1);
        Assert.Equal(VerticalMotionPhase.Neutral, ride.VerticalMotionPhase);
        Assert.Equal(0d, ride.AssemblyVerticalPositionMeters);
        Assert.Equal(0d, ride.AssemblyVerticalVelocityMetersPerSecond);
        Assert.Equal(0d, ride.AssemblyVerticalAccelerationMetersPerSecondSquared);
    }

    [Fact]
    public void Assembly_height_offsets_every_world_component_without_changing_nested_geometry()
    {
        const double millAngle = 0.73d;
        const double hubAngle = -0.41d;
        const double gondolaAngle = 0.29d;
        const double height = 0.37d;

        var neutralPositions = new[]
        {
            RideKinematics.MillWorldPosition(0d),
            RideKinematics.HubWorldPosition(millAngle, 2, 0d),
            RideKinematics.GondolaPivotWorldPosition(millAngle, 2, hubAngle, 3, 0d),
            RideKinematics.SeatWorldPosition(
                millAngle, 2, hubAngle, 3, gondolaAngle, SeatPosition.Left, 0d),
            RideKinematics.SeatWorldPosition(
                millAngle, 2, hubAngle, 3, gondolaAngle, SeatPosition.Right, 0d)
        };
        var raisedPositions = new[]
        {
            RideKinematics.MillWorldPosition(height),
            RideKinematics.HubWorldPosition(millAngle, 2, height),
            RideKinematics.GondolaPivotWorldPosition(millAngle, 2, hubAngle, 3, height),
            RideKinematics.SeatWorldPosition(
                millAngle, 2, hubAngle, 3, gondolaAngle, SeatPosition.Left, height),
            RideKinematics.SeatWorldPosition(
                millAngle, 2, hubAngle, 3, gondolaAngle, SeatPosition.Right, height)
        };

        for (var i = 0; i < neutralPositions.Length; i++)
        {
            Assert.Equal(neutralPositions[i].X, raisedPositions[i].X);
            Assert.Equal(neutralPositions[i].Y, raisedPositions[i].Y);
            Assert.Equal(height, raisedPositions[i].Z - neutralPositions[i].Z, 12);
        }
    }

    [Fact]
    public void Vertical_specific_force_combines_with_rotation_and_rest_remains_one_g()
    {
        var gondola = new Gondola(0, 0);
        gondola.AdvancePhysics(
            millAngle: 0d,
            millOmega: 0d,
            hubAngle: 0d,
            hubOmega: 0d,
            dt: TestHelpers.Dt.TotalSeconds);

        Assert.Equal(1d, gondola.VerticalG, 12);
        Assert.Equal(1d, gondola.ResultantG, 12);

        var liftAcceleration = RideParameters.Gravity * 0.1d;
        gondola.AdvancePhysics(
            millAngle: 0d,
            millOmega: 2d,
            hubAngle: 0d,
            hubOmega: 0d,
            dt: TestHelpers.Dt.TotalSeconds,
            assemblyVerticalAccelerationMetersPerSecondSquared: liftAcceleration);

        Assert.Equal(1.1d, gondola.VerticalG, 12);
        Assert.True(gondola.ResultantG > gondola.VerticalG);
    }

    [Fact]
    public void Reaching_the_existing_resultant_g_limit_engages_the_ride_safety_stop()
    {
        var ride = CreateSafeRide();
        ride.SetMainEnginePower(new EnginePower(100d));
        ride.SetHubEnginePower(new EnginePower(100d));
        ride.RequestTransition(RideState.Started);

        var maximumSteps = (int)(30d / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < maximumSteps && ride.CurrentState == RideState.Started; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        Assert.Equal(RideState.Stopping, ride.CurrentState);
        Assert.Equal(RideSafetyReason.OverG, ride.SafetyReason);
        Assert.True(ride.Mill.BrakesEngaged);
    }

    [Fact]
    public async Task The_sse_frame_serializes_the_current_assembly_height_in_camel_case()
    {
        var ride = CreateStartedRide();
        Advance(ride, steps: 100);
        var telemetry = ride.ToTelemetry();
        Assert.NotEqual(0d, telemetry.AssemblyVerticalPositionMeters);

        var result = TypedResults.ServerSentEvents(
            OneFrame(telemetry),
            eventType: "ride-telemetry");
        var context = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<JsonOptions>();
        using var serviceProvider = services.BuildServiceProvider();
        context.RequestServices = serviceProvider;
        await using var output = new MemoryStream();
        context.Response.Body = output;

        await result.ExecuteAsync(context);

        var frameText = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("event: ride-telemetry", frameText);
        Assert.Contains("\"assemblyVerticalPositionMeters\":", frameText);
        Assert.Contains(
            JsonSerializer.Serialize(
                telemetry.AssemblyVerticalPositionMeters,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            frameText);
    }

    private static Ride CreateSafeRide()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        return ride;
    }

    private static Ride CreateStartedRide()
    {
        var ride = CreateSafeRide();
        ride.RequestTransition(RideState.Started);
        return ride;
    }

    private static void Advance(Ride ride, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }
    }

    private static void Advance(VerticalMotion motion, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            motion.Advance(TestHelpers.Dt.TotalSeconds);
        }
    }

    private static void AssertSameVerticalState(Ride first, Ride second)
    {
        Assert.Equal(first.AssemblyVerticalPositionMeters, second.AssemblyVerticalPositionMeters);
        Assert.Equal(first.AssemblyVerticalVelocityMetersPerSecond, second.AssemblyVerticalVelocityMetersPerSecond);
        Assert.Equal(
            first.AssemblyVerticalAccelerationMetersPerSecondSquared,
            second.AssemblyVerticalAccelerationMetersPerSecondSquared);
        Assert.Equal(first.VerticalMotionPhase, second.VerticalMotionPhase);
    }

    private static async IAsyncEnumerable<RideTelemetry> OneFrame(RideTelemetry telemetry)
    {
        await Task.Yield();
        yield return telemetry;
    }
}
