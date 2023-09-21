using Blackbird.Applications.Sdk.Common;

namespace Apps.MicrosoftOneDrive.Dtos;

public class ParentReferenceDto
{
    [Display("Parent folder ID")]
    public string? Id { get; set; }
    
    [Display("Parent folder path")]
    public string? Path { get; set; }
    
    [Display("Drive type")]
    public string DriveType { get; set; }
    
    [Display("Drive ID")]
    public string DriveId { get; set; }

    [Display("Site ID")]
    public string? SiteId { get; set; }
}