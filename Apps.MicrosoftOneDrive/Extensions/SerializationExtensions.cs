using Newtonsoft.Json;

namespace Apps.MicrosoftOneDrive.Extensions;

public static class SerializationExtensions
{
    public static T DeserializeResponseContent<T>(this string content)
    {
        var deserializedContent = JsonConvert.DeserializeObject<T>(content, new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateTimeZoneHandling = DateTimeZoneHandling.Local
            }
        );
        return deserializedContent;
    }
}