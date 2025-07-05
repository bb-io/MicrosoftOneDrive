using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Invocables;
using Apps.MicrosoftOneDrive.Models.Responses;
using Apps.MicrosoftOneDrive.Webhooks.Inputs;
using Apps.MicrosoftOneDrive.Webhooks.Memory;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;

namespace Apps.MicrosoftOneDrive.Webhooks;

[PollingEventList("Folders")]
public class FolderPolling(InvocationContext invocationContext) : OneDriveInvocable(invocationContext)
{   

    [PollingEvent("On folders updated", Description = "Triggers when folders are updated or new folders are created")]
    public async Task<PollingEventResponse<DeltaTokenMemory, ListFoldersResponse>> OnFoldersCreatedOrUpdated(
        PollingEventRequest<DeltaTokenMemory> request,
        [PollingEventParameter] FolderInput folder)
    {
        if (request.Memory == null)
        {
            Client.GetChangedItems<FolderMetadataDto>(null, out var firstDeltaToken);
            return new()
            {
                FlyBird = false,
                Memory = new() { DeltaToken = firstDeltaToken }
            };
        }

        var changedFolders = Client.GetChangedItems<FolderMetadataDto>(request.Memory.DeltaToken, out var newDeltaToken)
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
    

}