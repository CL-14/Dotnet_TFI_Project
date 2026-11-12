using TfiBus.Core;

namespace TfiBus.Collector;

//Background servvice is the class for a loop that runs for as long as the program does, just a self reminder (:
public class Worker : BackgroundService
{
    //fields filled in by the constructor, then never changed.
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;

    //the host passes in the logger and the configuration (dependency injection)
    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    //runs once at startup and keeps going until stoppingToken signals "shut down"
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //read collector: PollIntervalSeconds from appsettings.json; 61 if it's missing
        int intervalSeconds = _configuration.GetValue<int>("Collector:PollIntervalSeconds");
        long pollNumber = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            FeedKind feed = PollSchedule.FeedFor(pollNumber);

            //structured logging: {Number} and {Feed} are named slots that get filled with the values.
            _logger.LogInformation("Poll {Number}: would fetch {Feed}", pollNumber, feed);
            pollNumber++;

            try
            {
                //waits between polls. he token makes the wait end at once on shutdown
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; //shutting down: leave the loop
            }
        }

        _logger.LogInformation("Collector stopped");
    }
}



// public class Worker(ILogger<Worker> logger) : BackgroundService
// {
//     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         while (!stoppingToken.IsCancellationRequested)
//         {
//             if (logger.IsEnabled(LogLevel.Information))
//             {
//                 logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
//             }
//             await Task.Delay(1000, stoppingToken);
//         }
//     }
// }
