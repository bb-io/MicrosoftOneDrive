using Apps.MicrosoftOneDrive.Dtos;

namespace Apps.MicrosoftOneDrive.Models.Responses;

public class ListFilesResponse
{
    public IEnumerable<FileMetadataDto> Files { get; set; }
}