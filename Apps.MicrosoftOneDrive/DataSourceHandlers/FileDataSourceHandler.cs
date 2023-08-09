using Apps.MicrosoftOneDrive.Actions;
using Apps.MicrosoftOneDrive.Dtos;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.MicrosoftOneDrive.DataSourceHandlers;

public class FileDataSourceHandler : BaseInvocable, IAsyncDataSourceHandler
{
    public FileDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public async Task<Dictionary<string, string>> GetDataAsync(DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var files = await GetFiles();
        var filesDictionary = new Dictionary<string, string>();
        
        foreach (var file in files)
        {
            var filePath = await GetFilePath(file);
            
            if (!filePath.Contains(context.SearchString ?? "", StringComparison.OrdinalIgnoreCase))
                continue;
            
            if (filePath.Length > 40)
            {
                var filePathParts = filePath.Split("/");
                if (filePathParts.Length > 3)
                    filePath = string.Join("/", filePathParts[0], "...", filePathParts[^2], filePathParts[^1]);
            }
            
            filesDictionary.Add(file.Id, filePath);
        }

        return filesDictionary;
    }

    private async Task<List<FileMetadataDto>> GetFiles()
    {
        var client = new MicrosoftOneDriveClient();
        var endpoint = "/root/search(q='.')";
        var files = new List<FileMetadataDto>();
        
        do
        {
            var request = new MicrosoftOneDriveRequest(endpoint, Method.Get, InvocationContext.AuthenticationCredentialsProviders);
            var result = await client.ExecuteWithHandling<ListWrapper<FileMetadataDto>>(request);
            var currentFiles = result.Value.Where(item => item.MimeType != null);
            files.AddRange(currentFiles);
            endpoint = result.ODataNextLink?.Split("drive")[1];
        } while (endpoint != null);

        return files;
    }

    private async Task<string> GetFilePath(FileMetadataDto file)
    {
        var parentFolder = await new StorageActions().GetFolderMetadataById(
            InvocationContext.AuthenticationCredentialsProviders,
            file.ParentReference.Id);
        var parentFolderName = parentFolder.Name;

        if (parentFolderName == "root")
            return file.Name;
        
        var parentFolderParentPath = parentFolder.ParentReference.Path;

        if (parentFolderParentPath == "/drive/root:")
            return $"{parentFolderName}/{file.Name}";

        var parentPathRelativeToRoot = parentFolderParentPath.Split(":/")[1] + "/" + parentFolderName;
        return $"{parentPathRelativeToRoot}/{file.Name}";
    }
}