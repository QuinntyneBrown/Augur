namespace Contoso.Orders.Worker;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHostedService<ScheduledWorker>();
        builder.Build().Run();
    }
}
