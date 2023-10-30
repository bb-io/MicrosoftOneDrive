using System.Net;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Apps.MicrosoftOneDrive.Models.Responses;
using Apps.MicrosoftOneDrive.Webhooks.Inputs;
using Apps.MicrosoftOneDrive.Webhooks.Payload;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Webhooks;

[WebhookList]
public class WebhookList: BaseInvocable
{
    private static readonly object LockObject = new();
    
    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;
    
    public WebhookList(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    [Webhook("On files updated or created", typeof(WebhookHandler), 
        Description = "This webhook is triggered when files are updated or created.")]
    public async Task<WebhookResponse<ListFilesResponse>> OnFilesUpdatedOrCreated(WebhookRequest request, 
        [WebhookParameter] FolderInput folder)
    {
        var payload = DeserializePayload(request);
        var changedFiles = GetChangedItems<FileMetadataDto>(payload.DeltaToken, out var newDeltaToken)
            .Where(item => item.MimeType != null
                           && (folder.ParentFolderId == null || item.ParentReference.Id == folder.ParentFolderId))
            .ToList();
        
        if (!changedFiles.Any())
            return new WebhookResponse<ListFilesResponse>
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
                ReceivedWebhookRequestType = WebhookRequestType.Preflight
            };

        await StoreDeltaToken(payload.DeltaToken, newDeltaToken);
        return new WebhookResponse<ListFilesResponse>
        {
            HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
            Result = new ListFilesResponse { Files = changedFiles }
        };
    }
    
    [Webhook("On folders updated or created", typeof(WebhookHandler), 
        Description = "This webhook is triggered when folders are updated or created.")]
    public async Task<WebhookResponse<ListFoldersResponse>> OnFoldersUpdatedOrCreated(WebhookRequest request, 
        [WebhookParameter] FolderInput folder)
    {
        var payload = DeserializePayload(request);
        var changedFolders = GetChangedItems<FolderMetadataDto>(payload.DeltaToken, out var newDeltaToken)
            .Where(item => item.ChildCount != null 
                           && item.ParentReference!.Id != null  
                           && (folder.ParentFolderId == null || item.ParentReference.Id == folder.ParentFolderId))
            .ToList();
        
        if (!changedFolders.Any())
            return new WebhookResponse<ListFoldersResponse>
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
                ReceivedWebhookRequestType = WebhookRequestType.Preflight
            };

        await StoreDeltaToken(payload.DeltaToken, newDeltaToken);
        return new WebhookResponse<ListFoldersResponse>
        {
            HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
            Result = new ListFoldersResponse { Folders = changedFolders }
        };
    }

    private List<T> GetChangedItems<T>(string deltaToken, out string newDeltaToken)
    {
        var client = new MicrosoftOneDriveClient();
        var items = new List<T>();
        var request = new MicrosoftOneDriveRequest($"/root/delta?token={deltaToken}", Method.Get, Creds);
        var result = client.ExecuteWithHandling<ListWrapper<T>>(request).Result;
        items.AddRange(result.Value);

        while (result.ODataNextLink != null)
        {
            var endpoint = result.ODataNextLink?.Split("drive")[1];
            request = new MicrosoftOneDriveRequest(endpoint, Method.Get, Creds);
            result = client.ExecuteWithHandling<ListWrapper<T>>(request).Result;
            items.AddRange(result.Value);
        }
        
        newDeltaToken = QueryHelpers.ParseQuery(result.ODataDeltaLink!.Split("?")[1])["token"];
        return items;
    }

    private EventPayload DeserializePayload(WebhookRequest request)
    {
        var payload = JsonConvert.DeserializeObject<EventPayload>(request.Body.ToString(), new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore
            }
        ) ?? throw new InvalidCastException(nameof(request.Body));
        return payload;
    }

    private async Task StoreDeltaToken(string oldDeltaToken, string newDeltaToken)
    {
        const string resource = "/me/drive/root";
        string bridgeWebhooksUrl = InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/') + $"/webhooks/{ApplicationConstants.AppName}";
        
        var oneDriveClient = new RestClient(new RestClientOptions("https://graph.microsoft.com/v1.0"));
        var subscriptionsRequest = new MicrosoftOneDriveRequest("/subscriptions", Method.Get, Creds);
        var response = await oneDriveClient.ExecuteAsync(subscriptionsRequest);
        var subscriptions = response.Content.DeserializeResponseContent<SubscriptionWrapper>().Value;
        var targetSubscription = subscriptions.Single(s => s.Resource == resource
                                                                   && s.NotificationUrl == bridgeWebhooksUrl);

        var bridgeService = new BridgeService(InvocationContext.UriInfo.BridgeServiceUrl.ToString());
        
        lock (LockObject)
        {
            var storedDeltaToken = bridgeService.RetrieveValue(targetSubscription.Id).Result.Trim('"');
            if (storedDeltaToken == oldDeltaToken)
                bridgeService.StoreValue(targetSubscription.Id, newDeltaToken).Wait();
        }
    }
}