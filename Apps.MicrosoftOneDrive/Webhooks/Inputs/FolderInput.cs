using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.MicrosoftOneDrive.Webhooks.Inputs;

public class FolderInput
{
    [Display("Folder ID")] 
    [FileDataSource(typeof(FolderDataSourceHandler))]
    public string? ParentFolderId { get; set; }
}