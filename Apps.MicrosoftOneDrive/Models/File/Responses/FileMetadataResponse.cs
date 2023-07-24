using Apps.MicrosoftOneDrive.Dtos;
using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Models.File.Responses;

public class FileMetadataResponse
{
    public FileMetadataResponse(FileMetadataDto fileMetadata)
    {
        FileId = fileMetadata.Id;
        Filename = fileMetadata.Name;
        WebUrl = fileMetadata.WebUrl;
        SizeInBytes = fileMetadata.Size;
        MimeType = fileMetadata.File.MimeType;
        CreatedBy = fileMetadata.CreatedBy.User;
        LastModifiedBy = fileMetadata.LastModifiedBy.User;
        ParentReference = fileMetadata.ParentReference;
    }
    
    [Display("File ID")]
    public string FileId { get; set; }
    
    public string Filename { get; set; }

    [Display("Web url")]
    public string WebUrl { get; set; }
    
    [Display("Size in bytes")]
    public long SizeInBytes { get; set; }
    
    [Display("Mime type")]
    public string MimeType { get; set; }
    
    [Display("Created by")]
    public UserDto CreatedBy { get; set; }
    
    [Display("Last modified by")]
    public UserDto LastModifiedBy { get; set; }
    
    [Display("Parent reference")]
    public ParentReferenceDto ParentReference { get; set; }
}