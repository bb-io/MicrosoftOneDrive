using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Models.File.Requests;

public class FileIdRequest
{
    [Display("File ID")]
    public string FileId { get; set; }
}