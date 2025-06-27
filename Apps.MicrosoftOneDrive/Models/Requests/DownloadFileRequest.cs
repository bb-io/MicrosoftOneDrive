using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.SDK.Blueprints.Interfaces.FileStorage;

namespace Apps.MicrosoftOneDrive.Models.Requests;
public class DownloadFileRequest : IDownloadFileInput
{
    [Display("File ID")]
    [DataSource(typeof(FileDataSourceHandler))]
    public string FileId { get; set; }
}
