using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Models.Responses;
using Apps.MicrosoftOneDrive.Webhooks.Inputs;
using Apps.MicrosoftOneDrive.Webhooks.Memory;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;
using Blackbird.Applications.SDK.Blueprints;
using Microsoft.AspNetCore.WebUtilities;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Webhooks;

[PollingEventList]
public class PollingList(InvocationContext invocationContext) : BaseInvocable(invocationContext)
{
    [BlueprintEventDefinition(BlueprintEvent.FilesCreatedOrUpdated)]
    [PollingEvent("On files created or updated",Description = "On files created or updated")]
    public async Task<PollingEventResponse<DeltaTokenMemory, ListFilesResponse>> OnFilesCreatedOrUpdated(
        PollingEventRequest<DeltaTokenMemory> request,
        [PollingEventParameter] FolderInput folder,
        [PollingEventParameter] IncludeSubfoldersInput includeSubfolders)
    {
        if(request.Memory == null)
        {
            GetChangedItems<FileMetadataDto>(null, out var firstDeltaToken);
            return new()
            {
                FlyBird = false,
                Memory = new() { DeltaToken = firstDeltaToken }
            };
        }

        var changedItems = GetChangedItems<FileMetadataDto>(request.Memory.DeltaToken, out var newDeltaToken);
        
        IEnumerable<FileMetadataDto> filteredChangedFiles;
        if (includeSubfolders?.IncludeSubfolders == true && !string.IsNullOrEmpty(folder.ParentFolderId))
        {
            var folderMetadata = await GetFolderMetadataById(folder.ParentFolderId);
            if (folderMetadata?.ParentReference?.Path == null || string.IsNullOrEmpty(folderMetadata.Name))
            {
                filteredChangedFiles = Enumerable.Empty<FileMetadataDto>();
            }
            else
            {
                var parentPath = folderMetadata.ParentReference.Path.TrimEnd('/');
                var folderPath = $"{parentPath}/{folderMetadata.Name}";

                filteredChangedFiles = changedItems
                    .Where(item => item.MimeType != null
                                   && item.ParentReference?.Path != null
                                   && item.ParentReference.Path.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase));
            }
        }
        else
        {
            filteredChangedFiles = changedItems
                .Where(item => item.MimeType != null 
                               && (string.IsNullOrEmpty(folder.ParentFolderId) 
                                   || item.ParentReference.Id.Equals(folder.ParentFolderId, StringComparison.OrdinalIgnoreCase)));
        }
        
        var changedFiles = filteredChangedFiles.ToList();
        if (changedFiles.Count == 0)
        {
            return new()
            {
                FlyBird = false,
                Memory = new() { DeltaToken = newDeltaToken }
            };
        }

        return new()
        {
            FlyBird = true,
            Memory = new() { DeltaToken = newDeltaToken },
            Result = new() { Files = changedFiles }
        };
    }

    [PollingEvent("On folders created or updated", Description = "On folders created or updated")]
    public async Task<PollingEventResponse<DeltaTokenMemory, ListFoldersResponse>> OnFoldersCreatedOrUpdated(
        PollingEventRequest<DeltaTokenMemory> request,
        [PollingEventParameter] FolderInput folder)
    {
        if (request.Memory == null)
        {
            GetChangedItems<FolderMetadataDto>(null, out var firstDeltaToken);
            return new()
            {
                FlyBird = false,
                Memory = new() { DeltaToken = firstDeltaToken }
            };
        }

        var changedFolders = GetChangedItems<FolderMetadataDto>(request.Memory.DeltaToken, out var newDeltaToken)
            .Where(item => item.ChildCount != null && item.ParentReference!.Id != null && (folder.ParentFolderId == null || item.ParentReference.Id == folder.ParentFolderId))
            .ToList();

        if (changedFolders.Count == 0)
            return new()
            {
                FlyBird = false,
                Memory = new() { DeltaToken = newDeltaToken }
            };

        return new()
        {
            FlyBird = true,
            Memory = new() { DeltaToken = newDeltaToken },
            Result = new() { Folders = changedFolders }
        };
    }
    
    private async Task<FolderMetadataDto> GetFolderMetadataById(string folderId)
    {
        var client = new MicrosoftOneDriveClient();
        var request = new MicrosoftOneDriveRequest($"/items/{folderId}", Method.Get, InvocationContext.AuthenticationCredentialsProviders);
        var folderMetadata = await client.ExecuteWithHandling<FolderMetadataDto>(request);
        return folderMetadata;
    }

    private List<T> GetChangedItems<T>(string deltaToken, out string newDeltaToken)
    {
        var deltaTokenQueryParameter = string.IsNullOrEmpty(deltaToken) ? string.Empty : $"?token={deltaToken}";
        var client = new MicrosoftOneDriveClient();
        var items = new List<T>();
        var request = new MicrosoftOneDriveRequest($"/root/delta{deltaTokenQueryParameter}", Method.Get, InvocationContext.AuthenticationCredentialsProviders);
        var result = client.ExecuteWithHandling<ListWrapper<T>>(request).Result;
        items.AddRange(result.Value);

        while (result.ODataNextLink != null)
        {
            var endpoint = result.ODataNextLink?.Split("drive")[1];
            request = new MicrosoftOneDriveRequest(endpoint, Method.Get, InvocationContext.AuthenticationCredentialsProviders);
            result = client.ExecuteWithHandling<ListWrapper<T>>(request).Result;
            items.AddRange(result.Value);
        }

        newDeltaToken = QueryHelpers.ParseQuery(result.ODataDeltaLink!.Split("?")[1])["token"];
        return items;
    }
}