namespace Apps.MicrosoftOneDrive.Dtos;

public class FileMetadataDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string WebUrl { get; set; }
    public long Size { get; set; }
    public MimeTypeWrapper File { get; set; }
    public UserWrapper CreatedBy { get; set; }
    public UserWrapper LastModifiedBy { get; set; }
    public ParentReferenceDto ParentReference { get; set; }
}

public class MimeTypeWrapper
{
    public string MimeType { get; set; }
}

