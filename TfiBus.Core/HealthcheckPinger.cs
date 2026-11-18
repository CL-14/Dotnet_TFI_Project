namespace TfiBus.Core;

// Sends a "still alive" ping to an outside monitoring service.
public class HealthcheckPinger
{
    private readonly HttpClient _http;
    private readonly string _url;

    public HealthcheckPinger(HttpClient http, string? url)
    {
        _http = http;
        _url = url ?? "";   // a missing setting becomes an empty string
    }

    // True when a URL has been set. A property is read like a field, without parentheses
    public bool IsConfigured
    {
        get { return _url != ""; }
    }

    // Sends the ping. Returns true if it was accepted, and false if no URL is set or the ping failed.
    // It never throws, a problem with the monitoring service must not stop the collector.
    public async Task<bool> PingAsync()
    {
        if (!IsConfigured)
        {
            return false;
        }

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(_url);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)   // the timeout
        {
            return false;
        }
    }
}
