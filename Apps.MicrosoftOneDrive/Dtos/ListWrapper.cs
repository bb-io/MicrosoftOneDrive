using Newtonsoft.Json;

namespace Apps.MicrosoftOneDrive.Dtos;

public class ListWrapper<T>
{
    [JsonProperty("@odata.nextLink")]
    public string? ODataNextLink { get; set; }
    
    public IEnumerable<T> Value { get; set; }
}