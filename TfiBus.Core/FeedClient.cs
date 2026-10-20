using TransitRealtime; //FeedMessage: the parse feed

namespace TfiBus.Core;

public class FeedClient
{
    //we set it const so the value is fixed at compiled time
    private const string VehicleUrl = "https://api.nationaltransport.ie/gtfsr/v2/vehicles";

    //i create fields only visible inside this class
    //assigned once in the constructor, then never changed
    private readonly HttpClient _http;
    private readonly string _apikey;

    // constructor
    public FeedClient(HttpClient http, string apiKey)

    {
        //fail early with a clear message to avoid a 401 later.
        //"throw" stops the method and raises an exception for caller to handle.
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is missing or empty.", nameof(apiKey));
        }

        _http = http;
        _apikey = apiKey;

    }

    //fetches the live vehicle feed and returns it parse.
    //Task<FeedMessage> = a FeedMessage that arrives when the awaits finish
    //throws if the request fails or the response can't be parsed

    public async Task<FeedMessage> GetVehicleFeedAsync()
    {
        //build get request
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, VehicleUrl);
        request.Headers.Add("x-api-key", _apikey);

        //send the request and pauses here until the response headers arrive
        using HttpResponseMessage response = await _http.SendAsync(request);

        //throw HttpRequestException for any 400/500 status
        //the exception carries the status code, so the caller can reach to each one.
        response.EnsureSuccessStatusCode();

        //reads he body as a stram of bytes
        //the feed is binary, can't be treated as text.
        using Stream body = await response.Content.ReadAsStreamAsync();


        // Parser is generated with the class; ParseFrom reads the protobuf bytes from the stream
        // and builds a FeedMessage. Throws InvalidProtocolBufferException if the data is invalid
        FeedMessage feed = FeedMessage.Parser.ParseFrom(body);
        
        return feed;

    }
}
