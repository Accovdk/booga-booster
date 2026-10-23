using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>Assembly height is composed into world positions without touching nested geometry.</summary>
public sealed class AssemblyHeightKinematicsTests
{
    private static GreatMill SpinningMill(bool lift, int ticks)
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100d));
        mill.SetAllHubPower(new EnginePower(50d));
        mill.ReleaseAllGondolaBrakes();
        if (lift)
        {
            mill.Lift.BeginRunning();
        }

        TestHelpers.StepMill(mill, ticks);
        return mill;
    }

    private static List<(string Name, WorldPoint Point)> AllComponents(GreatMill mill)
    {
        var points = new List<(string, WorldPoint)> { ("axis", mill.AxisWorldPosition) };
        for (var h = 0; h < RideParameters.HubCount; h++)
        {
            points.Add(($"hub{h}", mill.HubWorldPosition(h)));
            for (var g = 0; g < RideParameters.GondolasPerHub; g++)
            {
                points.Add(($"gondola{h}.{g}", mill.GondolaWorldPosition(h, g)));
                points.Add(($"seatL{h}.{g}", mill.SeatWorldPosition(h, g, SeatPosition.Left)));
                points.Add(($"seatR{h}.{g}", mill.SeatWorldPosition(h, g, SeatPosition.Right)));
            }
        }

        return points;
    }

    [Fact]
    public void Every_component_receives_the_same_vertical_displacement_and_no_horizontal_change()
    {
        var lifted = SpinningMill(lift: true, ticks: 500);
        var flat = SpinningMill(lift: false, ticks: 500);
        Assert.True(lifted.Lift.Height > 0.1d);

        var liftedPoints = AllComponents(lifted);
        var flatPoints = AllComponents(flat);

        Assert.Equal(flatPoints.Count, liftedPoints.Count);
        for (var i = 0; i < liftedPoints.Count; i++)
        {
            var delta = liftedPoints[i].Point - flatPoints[i].Point;
            Assert.Equal(lifted.Lift.Height, delta.Z, 12);
            Assert.Equal(0d, delta.X, 12);
            Assert.Equal(0d, delta.Y, 12);
        }
    }

    [Fact]
    public void Relative_positions_between_components_are_unchanged_by_the_lift()
    {
        var lifted = AllComponents(SpinningMill(lift: true, ticks: 400));
        var flat = AllComponents(SpinningMill(lift: false, ticks: 400));

        for (var i = 1; i < lifted.Count; i++)
        {
            var liftedRelative = lifted[i].Point - lifted[0].Point;
            var flatRelative = flat[i].Point - flat[0].Point;
            Assert.Equal(flatRelative, liftedRelative);
        }
    }

    [Fact]
    public void At_neutral_height_all_components_sit_at_zero_z()
    {
        foreach (var (name, point) in AllComponents(new GreatMill()))
        {
            Assert.True(point.Z == 0d, name);
        }
    }

    [Fact]
    public void The_seats_straddle_the_pivot_by_the_seat_offset()
    {
        var mill = SpinningMill(lift: true, ticks: 100);
        var pivot = mill.GondolaWorldPosition(1, 2);
        var left = mill.SeatWorldPosition(1, 2, SeatPosition.Left);
        var right = mill.SeatWorldPosition(1, 2, SeatPosition.Right);

        Assert.Equal(2d * RideParameters.SeatLateralOffset, (left - right).Planar.Length, 9);
        Assert.Equal(pivot.Z, left.Z);
        Assert.Equal(pivot.Z, right.Z);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    public void An_out_of_range_hub_is_rejected(int hub, int gondola)
    {
        var mill = new GreatMill();

        Assert.ThrowsAny<Exception>(() => mill.HubWorldPosition(hub));
        Assert.ThrowsAny<Exception>(() => mill.GondolaWorldPosition(hub, gondola));
    }
}
