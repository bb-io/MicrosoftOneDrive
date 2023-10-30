using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Microsoft.AspNetCore.WebUtilities;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Webhooks;

public class WebhookHandler : BaseInvocable, IWebhookEventHandler, IAsyncRenewableWebhookEventHandler
{
    private const string SubscriptionEvent = "updated"; // the only event type supported for drive items
    const string Resource = "/me/drive/root";
    private string BridgeWebhooksUrl = "";

    public WebhookHandler(InvocationContext invocationContext) : base(invocationContext)
    {
        BridgeWebhooksUrl = InvocationContext.UriInfo.BridgeServiceUrl.ToString() + $"/webhooks/{ApplicationConstants.AppName}"; ;
    }

    public async Task SubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var oneDriveClient = new RestClient(new RestClientOptions("https://graph.microsoft.com/v1.0"));
        var targetSubscription = await GetTargetSubscription(authenticationCredentialsProviders, oneDriveClient);
        var bridgeService = new BridgeService(InvocationContext.UriInfo.BridgeServiceUrl.ToString());
        string subscriptionId;
        
        if (targetSubscription is null)
        {
            var createSubscriptionRequest = new MicrosoftOneDriveRequest("/subscriptions", Method.Post,
                authenticationCredentialsProviders);
            createSubscriptionRequest.AddJsonBody(new
            {
                ChangeType = SubscriptionEvent,
                NotificationUrl = BridgeWebhooksUrl,
                Resource = Resource,
                ExpirationDateTime = (DateTime.Now + TimeSpan.FromMinutes(40000)).ToString("O"),
                ClientState = ApplicationConstants.OneDriveClientState
            });
            var response = await oneDriveClient.ExecuteAsync(createSubscriptionRequest);
            var subscription = response.Content.DeserializeResponseContent<SubscriptionDto>();
            subscriptionId = subscription.Id;
            
            var deltaRequest = new MicrosoftOneDriveRequest("/me/drive/root/delta", Method.Get, 
                authenticationCredentialsProviders); 
            response = await oneDriveClient.ExecuteAsync(deltaRequest);
            var result = response.Content.DeserializeResponseContent<ListWrapper<object>>();
            
            while (result.ODataNextLink != null)
            {
                var endpoint = result.ODataNextLink?.Split("v1.0")[1];
                deltaRequest = new MicrosoftOneDriveRequest(endpoint, Method.Get, authenticationCredentialsProviders);
                response = await oneDriveClient.ExecuteAsync(deltaRequest);
                result = response.Content.DeserializeResponseContent<ListWrapper<object>>();
            }
            
            var deltaToken = QueryHelpers.ParseQuery(result.ODataDeltaLink!.Split("?")[1])["token"];
            await bridgeService.StoreValue(subscriptionId, deltaToken);
        }
        else
            subscriptionId = targetSubscription.Id;
        
        await bridgeService.Subscribe(values["payloadUrl"], subscriptionId, SubscriptionEvent);
    }

    public async Task UnsubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var oneDriveClient = new RestClient(new RestClientOptions("https://graph.microsoft.com/v1.0"));
        var targetSubscription = await GetTargetSubscription(authenticationCredentialsProviders, oneDriveClient);
        var subscriptionId = targetSubscription.Id;
        
        var bridgeService = new BridgeService(InvocationContext.UriInfo.BridgeServiceUrl.ToString());
        var webhooksLeft = await bridgeService.Unsubscribe(values["payloadUrl"], subscriptionId, SubscriptionEvent);

        if (webhooksLeft == 0)
        {
            await bridgeService.DeleteValue(subscriptionId);
            var deleteSubscriptionRequest = new MicrosoftOneDriveRequest($"/subscriptions/{subscriptionId}", 
                Method.Delete, authenticationCredentialsProviders);
            await oneDriveClient.ExecuteAsync(deleteSubscriptionRequest);
        }
    }

    [Period(39995)]
    public async Task RenewSubscription(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var oneDriveClient = new RestClient(new RestClientOptions("https://graph.microsoft.com/v1.0"));
        var targetSubscription = await GetTargetSubscription(authenticationCredentialsProviders, oneDriveClient);
        var updateSubscriptionRequest = new MicrosoftOneDriveRequest($"/subscriptions/{targetSubscription.Id}", 
            Method.Patch, authenticationCredentialsProviders);
        updateSubscriptionRequest.AddJsonBody(new
        {
            ExpirationDateTime = (DateTime.Now + TimeSpan.FromMinutes(40000)).ToString("O")
        });
        await oneDriveClient.ExecuteAsync(updateSubscriptionRequest);
    }

    private async Task<SubscriptionDto?> GetTargetSubscription(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders, 
        RestClient oneDriveClient)
    {
        var subscriptionsRequest = new MicrosoftOneDriveRequest("/subscriptions", Method.Get, 
            authenticationCredentialsProviders);
        var response = await oneDriveClient.ExecuteAsync(subscriptionsRequest);
        var subscriptions = response.Content.DeserializeResponseContent<SubscriptionWrapper>().Value;
        var targetSubscription = subscriptions.FirstOrDefault(s => s.Resource == Resource
                                                                   && s.NotificationUrl == BridgeWebhooksUrl);
        return targetSubscription;
    }
}