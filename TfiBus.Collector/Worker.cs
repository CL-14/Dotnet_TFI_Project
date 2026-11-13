using System.Net;
using Google.Protobuf;   // InvalidProtocolBufferException
using TfiBus.Core;
using TransitRealtime;

namespace TfiBus.Collector;

//Background servvice is the class for a loop that runs for as long as the program does, just a self reminder (:
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly FeedClient _feedClient;
    private readonly CollectorDatabase _database;

    // The host supplies all four (dependency injection).
    public Worker(ILogger<Worker> logger, IConfiguration configuration,
                  FeedClient feedClient, CollectorDatabase database)
    {
        _logger = logger;
        _configuration = configuration;
        _feedClient = feedClient;
        _database = database;
    }


    // The main loop of the collector. Runs until the host is shutting down.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Math.Max(60, x) is never below 60: the NTA allows one request per 60 seconds per token,
        // so a too-small setting is raised instead of being trusted.
        int configured = _configuration.GetValue<int>("Collector:PollIntervalSeconds", 61);
        int intervalSeconds = Math.Max(60, configured);
        if (configured < 60)
        {
            _logger.LogWarning("PollIntervalSeconds is {Configured}, below the NTA limit; using 60.", configured);
        }

        // Creates the tables on first run; does nothing if they exist. A bad path stops the program here.
        _database.CreateTables();
        _logger.LogInformation("Collector started, polling every {Seconds} seconds.", intervalSeconds);

        // The main loop: pick the feed, poll it, wait the configured interval, repeat.
        long pollNumber = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            FeedKind feed = PollSchedule.FeedFor(pollNumber);
            int extraDelaySeconds = await PollOnceAsync(feed);
            pollNumber++;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds + extraDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;   // shutting down
            }
        }

        _logger.LogInformation("Collector stopped.");
    }

    // Fetches and stores one feed. Returns how many extra seconds to wait before the next poll.
    // "async Task<int>" means: this method awaits things and eventually produces an int.
    private async Task<int> PollOnceAsync(FeedKind feed)
    {
        long polledAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();   // seconds since 1 Jan 1970, UTC

        try
        {
            int records = await FetchAndStoreAsync(feed, polledAt);
            SafeLogPoll(feed, true, 200, records, null, polledAt);
            _logger.LogInformation("Stored {Feed}: {Count} records.", feed, records);
            return 0;
        }
        catch (HttpRequestException ex)
        {
            // StatusCode is null when there was no response at all (connection failed).
            int? status = (int?)ex.StatusCode;
            SafeLogPoll(feed, false, status, 0, ex.Message, polledAt);

            if (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Rate limited fetching {Feed}; waiting 5 extra minutes.", feed);
                return 300;
            }
            if (ex.StatusCode == HttpStatusCode.Unauthorized || ex.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogError("NTA rejected the API key fetching {Feed}; waiting 15 extra minutes.", feed);
                return 900;
            }

            // Logs only the innermost error message (for example "Connection reset by peer") instead of the whole stack trace, an error can generate a lot of lines and the stack trace is not useful for a network error.
            _logger.LogWarning("Request for {Feed} failed (status {Status}): {Message}", feed, status, ex.GetBaseException().Message);
            
            return 0;
        }
        catch (TaskCanceledException)
        {
            // FeedClient is not given the stop token, so a cancellation here is the 30-second timeout.
            SafeLogPoll(feed, false, null, 0, "timeout", polledAt);
            _logger.LogWarning("Request for {Feed} timed out.", feed);
            return 0;
        }
        catch (InvalidProtocolBufferException ex)
        {
            SafeLogPoll(feed, false, null, 0, "unparseable feed: " + ex.Message, polledAt);
            _logger.LogWarning(ex, "Could not parse {Feed}.", feed);
            return 0;
        }
        catch (Exception ex)
        {
            // Anything unexpected (for example a database error) is logged and the loop carries on.
            SafeLogPoll(feed, false, null, 0, ex.Message, polledAt);
            _logger.LogError(ex, "Unexpected error polling {Feed}.", feed);
            return 0;
        }
    }

    // Fetches one feed, converts it with the mappers and writes it. Returns how many records were stored.
    private async Task<int> FetchAndStoreAsync(FeedKind feed, long polledAt)
    {
        switch (feed)
        {
            case FeedKind.Vehicles:
                {
                    FeedMessage message = await _feedClient.GetVehicleFeedAsync();
                    List<VehicleSnapshot> vehicles = FeedMapper.MapVehicles(message);
                    _database.ReplaceLatestVehicles(vehicles, polledAt);
                    _database.RecordObservedTrips(FeedMapper.ToObservedTrips(vehicles), polledAt);
                    return vehicles.Count;
                }
            case FeedKind.TripUpdates:
                {
                    FeedMessage message = await _feedClient.GetTripUpdatesFeedAsync();
                    List<PredictedTrip> trips = FeedMapper.MapTripUpdates(message);
                    _database.RecordPredictedTrips(trips, polledAt);
                    return trips.Count;
                }
            default:   // FeedKind.Alerts
                {
                    FeedMessage message = await _feedClient.GetAlertsFeedAsync();
                    List<AlertRecord> alerts = FeedMapper.MapAlerts(message);
                    _database.ReplaceCurrentAlerts(alerts, polledAt);
                    return alerts.Count;
                }
        }
    }

    // Writes to poll_log without ever throwing, so a logging failure cannot stop the collector.
    private void SafeLogPoll(FeedKind feed, bool ok, int? status, int entities, string? error, long polledAt)
    {
        try
        {
            _database.LogPoll(feed.ToString(), ok, status, entities, error, polledAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not write to poll_log.");
        }
    }
}
