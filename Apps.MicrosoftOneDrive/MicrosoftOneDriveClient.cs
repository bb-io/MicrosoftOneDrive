using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Microsoft.AspNetCore.WebUtilities;
using RestSharp;

namespace Apps.MicrosoftOneDrive;

public class MicrosoftOneDriveClient : RestClient
{
    public MicrosoftOneDriveClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders) : base(new RestClientOptions
        {
            ThrowOnAnyError = false, BaseUrl = GetBaseUrl()
        }) 
    {
        this.AddDefaultHeader("Authorization", authenticationCredentialsProviders.First(p => p.KeyName == "Authorization").Value);
    }

    private static Uri GetBaseUrl()
    {
        return new Uri("https://graph.microsoft.com/v1.0/me/drive");
    }
    
    public async Task<T> ExecuteWithHandling<T>(RestRequest request)
    {
        var response = await ExecuteWithHandling(request);
        return response.Content.DeserializeResponseContent<T>();
    }
    
    public async Task<RestResponse> ExecuteWithHandling(RestRequest request)
    {
        var response = await ExecuteAsync(request);
        
        if (response.IsSuccessful)
            return response;

        throw ConfigureErrorException(response.Content);
    }

    private Exception ConfigureErrorException(string responseContent)
    {
        var error = responseContent.DeserializeResponseContent<ErrorDto>();
        return new PluginApplicationException(error.Error.Message);
    }

    public async Task<FolderMetadataDto> GetFolderMetadataById(string folderId)
    {
        var request = new RestRequest($"/items/{folderId}", Method.Get);
        var folderMetadata = await ExecuteWithHandling<FolderMetadataDto>(request);
        return folderMetadata;
    }

    public List<T> GetChangedItems<T>(string deltaToken, out string newDeltaToken)
    {
        var deltaTokenQueryParameter = string.IsNullOrEmpty(deltaToken) ? string.Empty : $"?token={deltaToken}";
        var items = new List<T>();
        var request = new RestRequest($"/root/delta{deltaTokenQueryParameter}", Method.Get);
        var result = ExecuteWithHandling<ListWrapper<T>>(request).Result;
        items.AddRange(result.Value);

        while (result.ODataNextLink != null)
        {
            var endpoint = result.ODataNextLink?.Split("drive")[1];
            request = new RestRequest(endpoint, Method.Get);
            result = ExecuteWithHandling<ListWrapper<T>>(request).Result;
            items.AddRange(result.Value);
        }

        newDeltaToken = QueryHelpers.ParseQuery(result.ODataDeltaLink!.Split("?")[1])["token"];
        return items;
    }
}