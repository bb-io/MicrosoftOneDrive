using Apps.MicrosoftOneDrive.Converters;
using Blackbird.Applications.Sdk.Common;
using Newtonsoft.Json;

namespace Apps.MicrosoftOneDrive.Dtos;

public class SimpleFolderDto
{
    [Display("Folder ID")]
    public string Id { get; set; }
    
    [Display("Folder name")]
    public string Name { get; set; }
       
    [JsonProperty("folder")]
    [JsonConverter(typeof(ChildCountConverter))]
    [Display("Child count")]
    public int? ChildCount { get; set; }
    
    [Display("Parent reference")]
    public ParentReferenceDto? ParentReference { get; set; }
}