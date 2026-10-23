using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>Lift acceleration joins the seat's specific force and the over-G threshold.</summary>
public sealed class LiftSpecificForceTests
{
    private static readonly double Dt = TestHelpers.Dt.TotalSeconds;
    private static readonly double PeakLift =
        RideParameters.LiftStrokeMeters / 2d * Math.Pow(RideParameters.LiftAngularFrequency, 2d);

    /// <summary>Mill speed whose pure horizontal load at the first gondola is <paramref name="horizontalG"/>.</summary>
    private static double OmegaForHorizontalG(double horizontalG) =>
        Math.Sqrt(horizontalG * RideParameters.Gravity / (RideParameters.MillArmLength + RideParameters.HubArmLength));

    [Fact]
    public void At_rest_every_seat_reads_exactly_one_g()
    {
        var mill = new GreatMill();
        TestHelpers.StepMill(mill, 5);

        foreach (var hub in mill.Hubs)
        {
            foreach (var gondola in hub.Gondolas)
            {
                Assert.Equal(1d, gondola.VerticalG, 12);
                Assert.Equal(1d, gondola.FeltG, 12);
            }
        }

        Assert.Equal(1d, mill.PeakFeltG, 12);
        Assert.False(mill.IsOverG);
    }

    [Fact]
    public void A_gondola_that_has_not_been_stepped_reads_one_g()
    {
        Assert.Equal(1d, new Gondola(0, 0).FeltG, 12);
    }

    [Theory]
    [InlineData(2.5d)]
    [InlineData(-2.5d)]
    [InlineData(0d)]
    public void Vertical_specific_force_is_gravity_plus_assembly_acceleration(double acceleration)
    {
        var gondola = new Gondola(0, 0);
        gondola.AdvancePhysics(0d, 0d, 0d, 0d, Dt, acceleration);

        var expected = (RideParameters.Gravity + acceleration) / RideParameters.Gravity;
        Assert.Equal(expected, gondola.VerticalG, 12);
        Assert.Equal(expected, gondola.FeltG, 12);
    }

    [Fact]
    public void Lift_acceleration_adds_to_the_horizontal_load_in_the_felt_total()
    {
        var omega = OmegaForHorizontalG(2d);
        var withoutLift = new Gondola(0, 0);
        var withLift = new Gondola(0, 0);

        withoutLift.AdvancePhysics(0d, omega, 0d, 0d, Dt);
        withLift.AdvancePhysics(0d, omega, 0d, 0d, Dt, PeakLift);

        Assert.True(withLift.FeltG > withoutLift.FeltG);
        Assert.Equal(withoutLift.ForwardG, withLift.ForwardG, 12);
        Assert.Equal(withoutLift.LateralG, withLift.LateralG, 12);
    }

    [Fact]
    public void Combined_load_crosses_the_limit_only_when_the_lift_acceleration_is_added()
    {
        var omega = OmegaForHorizontalG(4.38d);

        var rotationOnly = new Gondola(0, 0);
        rotationOnly.AdvancePhysics(0d, omega, 0d, 0d, Dt);
        var liftedUp = new Gondola(0, 0);
        liftedUp.AdvancePhysics(0d, omega, 0d, 0d, Dt, PeakLift);
        var liftedDown = new Gondola(0, 0);
        liftedDown.AdvancePhysics(0d, omega, 0d, 0d, Dt, -PeakLift);

        Assert.False(rotationOnly.ExceedsGForceLimit);
        Assert.True(liftedUp.ExceedsGForceLimit);
        Assert.False(liftedDown.ExceedsGForceLimit);
        Assert.True(liftedUp.FeltG >= RideParameters.MaxGForce);
    }

    [Fact]
    public void The_mill_peak_is_the_highest_seat_load_and_every_seat_feels_the_same_lift_acceleration()
    {
        var mill = new GreatMill();
        mill.Lift.BeginRunning();
        TestHelpers.StepMill(mill, 100);

        var expectedVertical = (RideParameters.Gravity + mill.Lift.Acceleration) / RideParameters.Gravity;
        var highest = 0d;
        foreach (var hub in mill.Hubs)
        {
            foreach (var gondola in hub.Gondolas)
            {
                Assert.Equal(expectedVertical, gondola.VerticalG, 12);
                highest = Math.Max(highest, gondola.FeltG);
            }
        }

        Assert.NotEqual(1d, expectedVertical);
        Assert.Equal(highest, mill.PeakFeltG, 12);
        Assert.False(mill.IsOverG);
    }
}
