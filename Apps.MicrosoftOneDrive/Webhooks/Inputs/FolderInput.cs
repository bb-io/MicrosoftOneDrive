using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.MicrosoftOneDrive.Webhooks.Inputs;

public class FolderInput
{
    [Display("Parent folder")] 
    [DataSource(typeof(FolderDataSourceHandler))]
    public string? ParentFolderId { get; set; }
}