using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Models.File.Requests;

public class FilePathRequest
{
    [Display("File path relative to drive's root")]
    public string FilePathRelativeToRoot { get; set; }
}