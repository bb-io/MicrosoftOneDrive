using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Invocables;
using Apps.MicrosoftOneDrive.Models.Responses;
using Apps.MicrosoftOneDrive.Webhooks.Inputs;
using Apps.MicrosoftOneDrive.Webhooks.Memory;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;
using Blackbird.Applications.SDK.Blueprints;

namespace Apps.MicrosoftOneDrive.Webhooks;

[PollingEventList("Files")]
public class FilePolling(InvocationContext invocationContext) : OneDriveInvocable(invocationContext)
{
    [BlueprintEventDefinition(BlueprintEvent.FilesCreatedOrUpdated)]
    [PollingEvent("On files updated", Description = "Triggered when files are updated, or new files are added")]
    public async Task<PollingEventResponse<DeltaTokenMemory, ListFilesResponse>> OnFilesCreatedOrUpdated(
        PollingEventRequest<DeltaTokenMemory> request,
        [PollingEventParameter] FolderInput folder,
        [PollingEventParameter] IncludeSubfoldersInput includeSubfolders)
    {
        if(request.Memory == null)
        {
            Client.GetChangedItems<FileMetadataDto>(null, out var firstDeltaToken);
            return new()
            {
                FlyBird = false,
                Memory = new() { DeltaToken = firstDeltaToken }
            };
        }

        var changedItems = Client.GetChangedItems<FileMetadataDto>(request.Memory.DeltaToken, out var newDeltaToken);
        
        IEnumerable<FileMetadataDto> filteredChangedFiles;
        if (includeSubfolders?.IncludeSubfolders == true && !string.IsNullOrEmpty(folder.ParentFolderId))
        {
            var folderMetadata = await Client.GetFolderMetadataById(folder.ParentFolderId);
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

}