using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Invocables;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Actions;

[ActionList("Folders")]
public class FolderActions(InvocationContext context) : OneDriveInvocable(context)
{    
    [Action("Get folder metadata", Description = "Retrieve the metadata for a folder in a drive.")]
    public async Task<FolderMetadataDto> GetFolderMetadataById([ActionParameter] [Display("Folder ID")] [FileDataSource(typeof(FolderDataSourceHandler))] string folderId)
    {
        var request = new RestRequest($"/items/{folderId}", Method.Get);
        var folderMetadata = await Client.ExecuteWithHandling<FolderMetadataDto>(request);
        return folderMetadata;
    }
    
    [Action("Create folder", Description = "Create a new folder in parent folder.")]
    public async Task<FolderMetadataDto> CreateFolderInParentFolderWithId(
        [ActionParameter] [Display("Parent folder ID")] [FileDataSource(typeof(FolderDataSourceHandler))] string parentFolderId,
        [ActionParameter] [Display("Folder name")] string folderName)
    {
        var request = new RestRequest($"/items/{parentFolderId}/children", Method.Post);
        request.AddJsonBody(new
        {
            Name = folderName,
            Folder = new { }
        });

        return await Client.ExecuteWithHandling<FolderMetadataDto>(request);
    }
    
    [Action("Delete folder", Description = "Delete folder in a drive.")]
    public async Task DeleteFolderById([ActionParameter] [Display("Folder ID")] [FileDataSource(typeof(FolderDataSourceHandler))] string folderId)
    {
        var request = new RestRequest($"/items/{folderId}", Method.Delete); 
        await Client.ExecuteWithHandling(request);
    }

    [Action("Search folder", Description = "Search folders by name")]

    public async Task<IEnumerable<SimpleFolderDto>> SearhFolders([ActionParameter][Display("Folder name")] string folderName)
    {
        var endpoint = "/list/items?$select=id&$expand=driveItem($select=id,name,parentReference,folder)";

        var request = new RestRequest(endpoint, Method.Get);
        request.AddHeader("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
        var folders = await Client.ExecuteWithHandling<ListWrapper<DriveItemWrapper<SimpleFolderDto>>>(request);
        if (folders != null && folders.Value != null &&
            folders.Value.Any(i => i.DriveItem.Name.Contains(folderName, StringComparison.OrdinalIgnoreCase)))
        {
            return folders.Value
                .Select(w => w.DriveItem)
                .Where(i => i.Name.Contains(folderName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            return new SimpleFolderDto[0];
        }
    }

}