using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.SDK.Blueprints.Interfaces.FileStorage;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.MicrosoftOneDrive.Models.Requests;
public class DownloadFileRequest : IDownloadFileInput
{
    [Display("File ID")]
    [FileDataSource(typeof(FileDataSourceHandler))]
    public string FileId { get; set; }
}
