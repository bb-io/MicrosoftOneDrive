﻿using Apps.MicrosoftOneDrive.Dtos;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.MicrosoftOneDrive.DataSourceHandlers;

public class FolderDataSourceHandler : BaseInvocable, IAsyncDataSourceItemHandler
{
    public FolderDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var client = new MicrosoftOneDriveClient();
        var endpoint = "/list/items?$select=id&$expand=driveItem($select=id,name,parentReference,folder)&$top=20";
        var foldersList = new List<DataSourceItem>();
        var foldersAmount = 0;

        do
        {
            var request = new MicrosoftOneDriveRequest(endpoint, Method.Get,
                InvocationContext.AuthenticationCredentialsProviders);
            request.AddHeader("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
            var folders = await client.ExecuteWithHandling<ListWrapper<DriveItemWrapper<FolderMetadataDto>>>(request);
            var filteredFolders = folders.Value
                .Select(w => w.DriveItem)
                .Where(i => i.ChildCount != null)
                .Select(i => new { i.Id, Path = GetFolderPath(i) })
                .Where(i => i.Path.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase));
            
            foreach (var folder in filteredFolders)
                foldersList.Add(new DataSourceItem(folder.Id, GetDisplayPath(folder.Path)));
            
            foldersAmount += filteredFolders.Count();
            endpoint = folders.ODataNextLink?.Split("me/drive")[1];
        } while (foldersAmount < 20 && endpoint != null);        

        var rootName = "My files (root folder)";
        if (string.IsNullOrWhiteSpace(context.SearchString) 
            || rootName.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase))
        {
            var request = new MicrosoftOneDriveRequest("/root", Method.Get,
                InvocationContext.AuthenticationCredentialsProviders);
            var rootFolder = await client.ExecuteWithHandling<FolderMetadataDto>(request);
            foldersList.Add(new DataSourceItem(rootFolder.Id, rootName));
        }
            
        return foldersList;
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

    private string GetFolderPath(FolderMetadataDto folder)
    {
        var parentPath = folder.ParentReference.Path.Split("root:");
        if (parentPath[1] == "")
            return folder.Name;

        return $"{parentPath[1].Substring(1)}/{folder.Name}";
    }
}