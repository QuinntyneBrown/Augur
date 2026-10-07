namespace Contoso.Orders.Worker;

/// <summary>Runs background work on a fixed interval until the host stops. Put your scheduled work in <see cref="RunOnceAsync"/>.</summary>
public sealed partial class ScheduledWorker(ILogger<ScheduledWorker> logger, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly ILogger<ScheduledWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            try
            {
                await Task.Delay(Interval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private Task RunOnceAsync(CancellationToken stoppingToken)
    {
        LogRan(time.GetUtcNow());
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled work ran at {RanAt}")]
    private partial void LogRan(DateTimeOffset ranAt);
}
