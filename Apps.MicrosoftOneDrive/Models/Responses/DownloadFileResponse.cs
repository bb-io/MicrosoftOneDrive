using File = Blackbird.Applications.Sdk.Common.Files.File;

namespace Apps.MicrosoftOneDrive.Models.Responses;

public class DownloadFileResponse
{
    public File File { get; set; }
}