using File = Blackbird.Applications.Sdk.Common.Files.File;

namespace Apps.MicrosoftOneDrive.Models.Requests;

public class UploadFileRequest
{
    public File File { get; set; }
}