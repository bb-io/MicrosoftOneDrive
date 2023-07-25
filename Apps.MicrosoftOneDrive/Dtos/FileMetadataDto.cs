using Apps.MicrosoftOneDrive.Converters;
using Blackbird.Applications.Sdk.Common;
using Newtonsoft.Json;

namespace Apps.MicrosoftOneDrive.Dtos;

public class FileMetadataDto
{
    [Display("File ID")]
    public string Id { get; set; }
    
    [Display("Filename")]
    public string Name { get; set; }
    
    [Display("Web url")]
    public string WebUrl { get; set; }
    
    [Display("Size in bytes")]
    public long Size { get; set; }
    
    [JsonProperty("file")]
    [JsonConverter(typeof(MimeTypeConverter))]
    [Display("Mime type")]
    public string MimeType { get; set; }
    
    [JsonConverter(typeof(UserConverter))]
    [Display("Created by")]
    public UserDto CreatedBy { get; set; }
    
    [JsonConverter(typeof(UserConverter))]
    [Display("Last modified by")]
    public UserDto LastModifiedBy { get; set; }
    
    [Display("Parent reference")]
    public ParentReferenceDto ParentReference { get; set; }
}
