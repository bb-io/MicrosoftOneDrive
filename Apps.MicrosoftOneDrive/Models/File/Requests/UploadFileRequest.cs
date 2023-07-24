using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Models.File.Requests;

public class UploadFileRequest
{
    [Display("Parent folder ID")]
    public string ParentFolderId { get; set; }
    
    public string Filename { get; set; }
    
    public byte[] File { get; set; }
}