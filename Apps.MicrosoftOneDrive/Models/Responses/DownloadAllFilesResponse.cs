using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.MicrosoftOneDrive.Models.Responses;

public class DownloadAllFilesResponse
{
    public IEnumerable<FileReference> Files { get; set; } = Array.Empty<FileReference>();
}