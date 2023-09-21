using Apps.MicrosoftOneDrive.Dtos;

namespace Apps.MicrosoftOneDrive.Models.Responses;

public class ListFoldersResponse
{
    public IEnumerable<FolderMetadataDto> Folders { get; set; }
}