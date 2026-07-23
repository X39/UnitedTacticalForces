using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class HostingAuditWriterTelemetryTests
{
    [Fact]
    public async Task Add_CapturesCurrentTelemetryTraceAndSpan()
    {
        await using var store = await HostingTestStore.CreateAsync();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == HostingTelemetry.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = HostingTelemetry.StartRuntimeOperation("test-provider", "test-operation");
        Assert.NotNull(activity);

        new HostingAuditWriter().Add(
            store.DbContext,
            EHostingAuditEventKind.Recovery,
            "Test recovery.");
        await store.DbContext.SaveChangesAsync();

        var record = await store.DbContext.HostingAuditRecords.AsNoTracking().SingleAsync();
        Assert.Equal(activity.TraceId.ToString(), record.TraceId);
        Assert.Equal(activity.SpanId.ToString(), record.SpanId);
    }
}
