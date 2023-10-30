using Apps.MicrosoftOneDrive.Webhooks.Payload;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Webhooks;

public class BridgeService
{
    private const string AppName = ApplicationConstants.AppName;
    
    private readonly RestClient _bridgeClient;

    public BridgeService(string bridgeServiceUrl)
    {
        _bridgeClient = new RestClient(new RestClientOptions(bridgeServiceUrl));
    }

    public async Task Subscribe(string url, string id, string subscriptionEvent)
    {
        var bridgeSubscriptionRequest = CreateBridgeRequest($"/webhooks/{AppName}/{id}/{subscriptionEvent}", Method.Post);
        bridgeSubscriptionRequest.AddBody(url);
        await _bridgeClient.ExecuteAsync(bridgeSubscriptionRequest);
    }
    
    public async Task<int> Unsubscribe(string url, string id, string subscriptionEvent)
    {
        var getTriggerRequest = CreateBridgeRequest($"/webhooks/{AppName}/{id}/{subscriptionEvent}", Method.Get);
        var webhooks = await _bridgeClient.GetAsync<List<BridgeGetResponse>>(getTriggerRequest);
        var webhook = webhooks.FirstOrDefault(w => w.Value == url);

        var deleteTriggerRequest = CreateBridgeRequest($"/webhooks/{AppName}/{id}/{subscriptionEvent}/{webhook.Id}", 
            Method.Delete);
        await _bridgeClient.ExecuteAsync(deleteTriggerRequest);

        var webhooksLeft = webhooks.Count - 1;
        return webhooksLeft;
    }

    public async Task StoreValue(string key, string value)
    {
        var storeValueRequest = CreateBridgeRequest($"/storage/{AppName}/{key}", Method.Post);
        storeValueRequest.AddBody(value);
        await _bridgeClient.ExecuteAsync(storeValueRequest);
    }
    
    public async Task<string> RetrieveValue(string key)
    {
        var deleteValueRequest = CreateBridgeRequest($"/storage/{AppName}/{key}", Method.Get);
        var result = await _bridgeClient.ExecuteAsync(deleteValueRequest);
        return result.Content;
    }
    
    public async Task DeleteValue(string key)
    {
        var deleteValueRequest = CreateBridgeRequest($"/storage/{AppName}/{key}", Method.Delete);
        await _bridgeClient.ExecuteAsync(deleteValueRequest);
    }

    private RestRequest CreateBridgeRequest(string endpoint, Method method)
    {
        var request = new RestRequest(endpoint, method);
        request.AddHeader("Blackbird-Token", ApplicationConstants.BlackbirdToken);
        return request;
    }
}