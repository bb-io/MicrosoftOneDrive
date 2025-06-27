using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Blueprints.Interfaces.FileStorage;

namespace Apps.MicrosoftOneDrive.Models.Responses;

public class DownloadFileResponse : IDownloadFileOutput
{
    public FileReference File { get; set; }
}