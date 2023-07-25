using Apps.MicrosoftOneDrive.Dtos;

namespace Apps.MicrosoftOneDrive.Models.Folder.Responses;

public class ListFilesInFolderResponse
{
    public IEnumerable<FileMetadataDto> Files { get; set; }
}