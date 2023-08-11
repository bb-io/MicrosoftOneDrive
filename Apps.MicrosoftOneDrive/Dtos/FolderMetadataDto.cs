using Apps.MicrosoftOneDrive.Converters;
using Blackbird.Applications.Sdk.Common;
using Newtonsoft.Json;

namespace Apps.MicrosoftOneDrive.Dtos;

public class FolderMetadataDto
{
    [Display("Folder ID")]
    public string Id { get; set; }
    
    [Display("Folder name")]
    public string Name { get; set; }
    
    [Display("Web url")]
    public string? WebUrl { get; set; }
    
    [Display("Size in bytes")]
    public long? Size { get; set; }
    
    [JsonConverter(typeof(UserConverter))]
    [Display("Created by")]
    public UserDto? CreatedBy { get; set; }
    
    [JsonConverter(typeof(UserConverter))]
    [Display("Last modified by")]
    public UserDto? LastModifiedBy { get; set; }
    
    [JsonProperty("folder")]
    [JsonConverter(typeof(ChildCountConverter))]
    [Display("Child count")]
    public int? ChildCount { get; set; }
    
    [Display("Parent reference")]
    public ParentReferenceDto? ParentReference { get; set; }
}