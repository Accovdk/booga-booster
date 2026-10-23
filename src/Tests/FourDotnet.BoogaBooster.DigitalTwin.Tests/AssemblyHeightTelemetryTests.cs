using System.Text;
using System.Text.Json;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>The assembly height travels in the telemetry snapshot and over the SSE stream.</summary>
public sealed class AssemblyHeightTelemetryTests
{
    private const string PropertyName = "assemblyHeightMeters";

    private static RideStore StartedStore(int ticks)
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        store.RequestStateTransition(RideState.Loading);
        store.RequestStateTransition(RideState.Safe);
        store.RequestStateTransition(RideState.Started);
        for (var i = 0; i < ticks; i++)
        {
            store.Advance(RideParameters.TimeStep);
        }

        return store;
    }

    [Fact]
    public void A_resting_ride_reports_neutral_height()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));

        Assert.Equal(0d, store.GetTelemetry().AssemblyHeightMeters);
    }

    [Fact]
    public void The_snapshot_carries_the_current_lift_height()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        for (var i = 0; i < 480; i++)
        {
            ride.Advance(RideParameters.TimeStep);
        }

        var telemetry = ride.ToTelemetry();

        Assert.True(telemetry.AssemblyHeightMeters > 0d);
        Assert.Equal(ride.Mill.Lift.Height, telemetry.AssemblyHeightMeters);
    }

    [Fact]
    public void The_property_is_optional_when_constructing_a_snapshot()
    {
        var full = Ride.Create().ToTelemetry();
        var legacy = new RideTelemetry(
            full.State, full.SimulationTimeSeconds, full.IsSafeToStart, full.SafetyReason,
            full.AvailableTransitions, full.BoardedPassengerCount, full.BrakesEngaged,
            full.Mill, full.Hubs, full.Gondolas);

        Assert.Equal(0d, legacy.AssemblyHeightMeters);
    }

    [Fact]
    public async Task The_serialized_sse_frame_includes_the_current_height()
    {
        var store = StartedStore(ticks: 480);
        var stream = new RideTelemetryStream(store, new FakeTimeProvider());

        await using var frames = stream.Stream(CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await frames.MoveNextAsync());
        var frame = frames.Current;

        var body = await ExecuteSseAsync(frame);

        var dataLine = body.Split('\n').First(line => line.StartsWith("data:", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(dataLine["data:".Length..].Trim());
        Assert.True(json.RootElement.TryGetProperty(PropertyName, out var height));
        Assert.Equal(frame.AssemblyHeightMeters, height.GetDouble());
        Assert.True(height.GetDouble() > 0d);
        Assert.Contains("event: ride-telemetry", body, StringComparison.Ordinal);
    }

    private static async Task<string> ExecuteSseAsync(RideTelemetry frame)
    {
        var services = new ServiceCollection().AddLogging().ConfigureHttpJsonOptions(_ => { }).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        await using var body = new MemoryStream();
        context.Response.Body = body;

        var result = TypedResults.ServerSentEvents(SingleFrame(frame), eventType: "ride-telemetry");
        await result.ExecuteAsync(context);

        return Encoding.UTF8.GetString(body.ToArray());
    }

    private static async IAsyncEnumerable<RideTelemetry> SingleFrame(RideTelemetry frame)
    {
        yield return frame;
        await Task.CompletedTask;
    }
}
