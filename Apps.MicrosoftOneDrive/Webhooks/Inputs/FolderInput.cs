using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.MicrosoftOneDrive.Webhooks.Inputs;

public class FolderInput
{
    [Display("Folder ID")] 
    [DataSource(typeof(FolderDataSourceHandler))]
    public string? ParentFolderId { get; set; }
}