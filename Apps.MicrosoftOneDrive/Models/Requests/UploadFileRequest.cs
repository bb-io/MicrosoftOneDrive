using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Models.Requests;

public class UploadFileRequest
{
    public string Filename { get; set; }
    
    public byte[] File { get; set; }
}