using Microsoft.Extensions.Logging.Abstractions;
using Contoso.Orders.Worker;

namespace Contoso.Orders.Tests;

public sealed class WorkerTests
{
    [Fact]
    public async Task The_worker_stops_when_the_host_stops()
    {
        using var worker = new ScheduledWorker(NullLogger<ScheduledWorker>.Instance, TimeProvider.System);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.ExecuteTask?.IsCompleted);
    }
}
