using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.MicrosoftOneDrive.Models.Requests;

public class DownloadAllFilesInFolderRequest
{
    [Display("Folder ID")]
    [FileDataSource(typeof(FolderDataSourceHandler))]
    public string FolderId { get; set; } = default!;

    [Display("Include subfolders (recursive)")]
    public bool? Recursive { get; set; }
}