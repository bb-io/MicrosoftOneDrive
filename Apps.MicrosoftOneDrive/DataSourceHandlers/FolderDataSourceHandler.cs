﻿using Apps.MicrosoftOneDrive.Dtos;
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
        var client = new MicrosoftOneDriveClient();
        var endpoint = "/list/items?$select=id&$expand=driveItem($select=id,name,parentReference)&" +
                       "$filter=fields/ContentType eq 'Folder'&$top=20";
        var foldersDictionary = new Dictionary<string, string>();
        var foldersAmount = 0;

        do
        {
            var request = new MicrosoftOneDriveRequest(endpoint, Method.Get,
                InvocationContext.AuthenticationCredentialsProviders);
            request.AddHeader("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
            var folders = await client.ExecuteWithHandling<ListWrapper<DriveItemWrapper<FolderMetadataDto>>>(request);
            var filteredFolders = folders.Value
                .Select(w => w.DriveItem)
                .Select(i => new { i.Id, Path = GetFolderPath(i) })
                .Where(i => i.Path.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase));
            
            foreach (var file in filteredFolders)
                foldersDictionary.Add(file.Id, file.Path);
            
            foldersAmount += filteredFolders.Count();
            endpoint = folders.ODataNextLink?.Split("me/drive")[1];
        } while (foldersAmount < 20 && endpoint != null);
        
        foreach (var file in foldersDictionary)
        {
            var filePath = file.Value;
            if (filePath.Length > 40)
            {
                var filePathParts = filePath.Split("/");
                if (filePathParts.Length > 3)
                {
                    filePath = string.Join("/", filePathParts[0], "...", filePathParts[^2], filePathParts[^1]);
                    foldersDictionary[file.Key] = filePath;
                }
            }
        }

        return foldersDictionary;
    }

    private string GetFolderPath(FolderMetadataDto folder)
    {
        var parentPath = folder.ParentReference.Path.Split("root:");
        if (parentPath[1] == "")
            return folder.Name;

        return $"{parentPath[1].Substring(1)}/{folder.Name}";
    }
}