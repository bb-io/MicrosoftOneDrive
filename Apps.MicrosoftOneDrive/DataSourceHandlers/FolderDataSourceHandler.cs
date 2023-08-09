using Apps.MicrosoftOneDrive.Actions;
using Apps.MicrosoftOneDrive.Dtos;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.MicrosoftOneDrive.DataSourceHandlers;

public class FolderDataSourceHandler : BaseInvocable, IAsyncDataSourceHandler
{
    public FolderDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public async Task<Dictionary<string, string>> GetDataAsync(DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var folders = await GetFolders();
        var foldersDictionary = new Dictionary<string, string>();
        
        foreach (var folder in folders)
        {
            var folderPath = await GetFolderPath(folder);
            
            if (!folderPath.Contains(context.SearchString ?? "", StringComparison.OrdinalIgnoreCase))
                continue;
            
            if (folderPath.Length > 40)
            {
                var folderPathParts = folderPath.Split("/");
                if (folderPathParts.Length > 3)
                    folderPath = string.Join("/", folderPathParts[0], "...", folderPathParts[^2], folderPathParts[^1]);
            }
            
            foldersDictionary.Add(folder.Id, folderPath);
        }

        return foldersDictionary;
    }

    private async Task<List<FolderMetadataDto>> GetFolders()
    {
        var client = new MicrosoftOneDriveClient();
        var endpoint = "/root/search(q='.')";
        var folders = new List<FolderMetadataDto>();
        
        do
        {
            var request = new MicrosoftOneDriveRequest(endpoint, Method.Get, InvocationContext.AuthenticationCredentialsProviders);
            var result = await client.ExecuteWithHandling<ListWrapper<FolderMetadataDto>>(request);
            var currentFolders = result.Value.Where(item => item.ChildCount != null);
            folders.AddRange(currentFolders);
            endpoint = result.ODataNextLink?.Split("drive")[1];
        } while (endpoint != null);

        return folders;
    }

    private async Task<string> GetFolderPath(FolderMetadataDto folder)
    {
        var parentFolder = await new StorageActions().GetFolderMetadataById(
            InvocationContext.AuthenticationCredentialsProviders,
            folder.ParentReference.Id);
        var parentFolderName = parentFolder.Name;

        if (parentFolderName == "root")
            return folder.Name;
        
        var parentFolderParentPath = parentFolder.ParentReference.Path;

        if (parentFolderParentPath == "/drive/root:")
            return $"{parentFolderName}/{folder.Name}";

        var parentPathRelativeToRoot = parentFolderParentPath.Split(":/")[1] + "/" + parentFolderName;
        return $"{parentPathRelativeToRoot}/{folder.Name}";
    }
}