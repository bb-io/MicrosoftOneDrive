﻿using Apps.MicrosoftOneDrive.Dtos;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.MicrosoftOneDrive.DataSourceHandlers;

public class FileDataSourceHandler : BaseInvocable, IAsyncDataSourceItemHandler
{
    public FileDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var client = new MicrosoftOneDriveClient();
        var endpoint = "/list/items?$select=id&$expand=driveItem($select=id,name,parentReference,file)&$top=20";
        var filesList = new List<DataSourceItem>();
        var filesAmount = 0;

        do
        {
            var request = new MicrosoftOneDriveRequest(endpoint, Method.Get,
                InvocationContext.AuthenticationCredentialsProviders);
            request.AddHeader("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
            var files = await client.ExecuteWithHandling<ListWrapper<DriveItemWrapper<FileMetadataDto>>>(request);
            var filteredFiles = files.Value
                .Select(w => w.DriveItem)
                .Where(i => i.MimeType != null)
                .Select(i => new { i.Id, Path = GetFilePath(i) })
                .Where(i => i.Path.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase));
            
            foreach (var file in filteredFiles)
                filesList.Add(new DataSourceItem(file.Id, GetDisplayPath(file.Path)));
            
            filesAmount += filteredFiles.Count();
            endpoint = files.ODataNextLink?.Split("me/drive")[1];
        } while (filesAmount < 20 && endpoint != null);

        return filesList;
    }

    private string GetDisplayPath(string path)
    {
        if (path.Length > 40)
        {
            var filePathParts = path.Split("/");
            if (filePathParts.Length > 3)
            {
                path = string.Join("/", filePathParts[0], "...", filePathParts[^2], filePathParts[^1]);
            }
        }
        return path;
    }

    private string GetFilePath(FileMetadataDto file)
    {
        var parentPath = file.ParentReference.Path.Split("root:");
        if (parentPath[1] == "")
            return file.Name;

        return $"{parentPath[1].Substring(1)}/{file.Name}";
    }
}