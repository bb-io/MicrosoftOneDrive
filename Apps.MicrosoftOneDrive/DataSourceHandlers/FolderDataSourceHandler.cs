using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Invocables;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;
using RestSharp;
using File = Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems.File;

namespace Apps.MicrosoftOneDrive.DataSourceHandlers;

public class FolderDataSourceHandler(InvocationContext invocationContext) : OneDriveInvocable(invocationContext), IAsyncFileDataSourceItemHandler
{
    private const string RootFolderDisplayName = "My files";

    public async Task<IEnumerable<FileDataItem>> GetFolderContentAsync(FolderContentDataSourceContext context, CancellationToken cancellationToken)
    {
        var result = new List<FileDataItem>();
        var sourceItems = await ListItemsInFolderById(string.IsNullOrEmpty(context.FolderId) ? "root" : context.FolderId);

        foreach (var item in sourceItems)
        {
            result.Add(string.IsNullOrEmpty(item.MimeType) ? new Folder() { Id = item.FileId, Date = item.CreatedDateTime, DisplayName = item.Name, IsSelectable = true } :
            new File() { Id = item.FileId, Date = item.LastModifiedDateTime, DisplayName = item.Name, Size = item.Size, IsSelectable = false });
        }
        return result;
    }

    public async Task<IEnumerable<FolderPathItem>> GetFolderPathAsync(FolderPathDataSourceContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context?.FileDataItemId))
            return new List<FolderPathItem>() { new FolderPathItem() { DisplayName = RootFolderDisplayName, Id = "root" } };

        var result = new List<FolderPathItem>();
        try
        {
            var fileMetadataDto = await GetFileMetadataById(context.FileDataItemId);
            var parentFolderId = fileMetadataDto?.ParentReference?.Id;

            while (!string.IsNullOrEmpty(parentFolderId))
            {
                var parentFolder = await GetFileMetadataById(parentFolderId);
                result = result.Prepend(new FolderPathItem()
                {
                    DisplayName = parentFolder.Name,
                    Id = parentFolder.FileId,
                }).ToList();
                parentFolderId = parentFolder?.ParentReference?.Id;
            }
            var rootFolder = result.FirstOrDefault();
            if (rootFolder != null)
            {
                rootFolder.DisplayName = RootFolderDisplayName;
                rootFolder.Id = "root";
            }
        }
        catch (Exception ex)
        {
            result.Add(new FolderPathItem() { DisplayName = RootFolderDisplayName, Id = "root" });
        }
        return result;
    }

    private async Task<List<FileMetadataDto>> ListItemsInFolderById(string folderId)
    {
        var filesInFolder = new List<FileMetadataDto>();
        string? next = $"/items/{folderId}/children";

        do
        {
            var request = Uri.IsWellFormedUriString(next, UriKind.Absolute)
                ? new RestRequest(new Uri(next!), Method.Get)
                : new RestRequest(next!, Method.Get);

            var result = await Client.ExecuteWithHandling<ListWrapper<FileMetadataDto>>(request);

            var page = result?.Value ?? Array.Empty<FileMetadataDto>();
            filesInFolder.AddRange(page);

            next = result?.ODataNextLink;
        }
        while (!string.IsNullOrEmpty(next));

        return filesInFolder;
    }

    public async Task<FileMetadataDto> GetFileMetadataById(string fileId)
    {
        var request = new RestRequest($"/items/{fileId}", Method.Get);
        return await Client.ExecuteWithHandling<FileMetadataDto>(request);
    }
}