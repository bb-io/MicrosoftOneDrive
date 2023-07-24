namespace Apps.MicrosoftOneDrive.Models.File.Responses;

public class DownloadFileResponse
{
    public string Filename { get; set; }
    public byte[] File { get; set; }
}