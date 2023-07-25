using Apps.MicrosoftOneDrive.Dtos;

namespace Apps.MicrosoftOneDrive.Models.Responses;

public class ListFilesInFolderResponse
{
    public IEnumerable<FileMetadataDto> Files { get; set; }
}