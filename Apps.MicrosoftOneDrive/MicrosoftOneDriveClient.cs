using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using RestSharp;
using System.Net;

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

        throw ConfigureErrorException(response);
    }

    private Exception ConfigureErrorException(RestResponse responseContent)
    {
        if (responseContent is null)
            return new PluginApplicationException("No response received from server.");

        var content = responseContent.Content ?? string.Empty;
        var statusCode = responseContent.StatusCode;
        var statusInt = (int)statusCode;

        var isHtml =
            (responseContent.ContentType?.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0) ||
            content.TrimStart().StartsWith("<");

        if (isHtml)
        {
            var htmlMsg = ExtractHtmlErrorMessage(content);
            return new PluginApplicationException($"HTTP {statusInt} {statusCode}: {htmlMsg}");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            var extra = !string.IsNullOrWhiteSpace(responseContent.ErrorMessage) ? $" Details: {responseContent.ErrorMessage}" : "";
            return new PluginApplicationException($"HTTP {statusInt} {statusCode}. Response body is empty.{extra}");
        }

        try
        {
            var err = content.DeserializeResponseContent<ErrorDto>();
            var message = err?.Error?.Message?.Trim();

            if (statusCode == HttpStatusCode.Unauthorized)
                return new PluginMisconfigurationException("Unauthorized. Please re-connect your Microsoft account.");

            if (statusCode == HttpStatusCode.NotFound)
                return new PluginApplicationException($"{message ?? "Resource not found."} Please check the inputs for this action.");

            if (!string.IsNullOrWhiteSpace(message))
                return new PluginApplicationException(message);
        }
        catch
        {
        }

        var detail = string.IsNullOrWhiteSpace(content) ? responseContent.ErrorMessage : content;
        detail = string.IsNullOrWhiteSpace(detail) ? "Unknown error." : detail.Trim();

        return new PluginApplicationException($"HTTP {statusInt} {statusCode}. {detail}");
    }

    private static string ExtractHtmlErrorMessage(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "N/A";

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
        var body = doc.DocumentNode.SelectSingleNode("//body")?.InnerText?.Trim();

        title = string.IsNullOrWhiteSpace(title) ? "No Title" : title;
        body = string.IsNullOrWhiteSpace(body) ? "No Description" : body;

        return $"{title}:\nError Description: {body}";
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