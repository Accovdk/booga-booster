using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public class LiftParametersTests
{
    [Fact]
    public void AngularFrequency_IsTwoPiOverPeriod()
    {
        Assert.Equal(2d * Math.PI / RideParameters.LiftPeriodSeconds, RideParameters.LiftAngularFrequency, 12);
    }

    [Fact]
    public void PeakRunningAcceleration_IsWithinForceEnvelope()
    {
        var peak = RideParameters.LiftStrokeMeters / 2d
            * RideParameters.LiftAngularFrequency * RideParameters.LiftAngularFrequency;

        Assert.True(peak / RideParameters.Gravity <= RideParameters.LiftMaxAccelerationG);
    }

    [Fact]
    public void LiftBudget_StaysSmallComparedWithTheOverGLimit()
    {
        Assert.True(RideParameters.LiftMaxAccelerationG < RideParameters.MaxGForce);
        Assert.True(RideParameters.LiftReturnSeconds > 0d);
    }
}
