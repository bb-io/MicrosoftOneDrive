using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using RestSharp;

namespace Apps.MicrosoftOneDrive;

public class WebhookLogger
{
    private static readonly string LogUrl = @"https://webhook.site/c2123368-7888-4ef6-bbb6-19ead1f97113";

    public static async Task LogAsync<T>(T obj)
        where T : class
    {
        var request = new RestRequest(string.Empty, Method.Post)
            .WithJsonBody(obj);
        var client = new RestClient(LogUrl);
        await client.ExecuteAsync(request);
    }
}