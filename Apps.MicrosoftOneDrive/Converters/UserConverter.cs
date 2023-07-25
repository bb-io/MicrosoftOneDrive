using Apps.MicrosoftOneDrive.Dtos;
using Newtonsoft.Json;

namespace Apps.MicrosoftOneDrive.Converters;

public class UserConverter : JsonConverter
{
    private class JsonUser
    {
        public UserDto User { get; set; }
    }
    
    public override bool CanWrite => false;
    public override bool CanConvert(Type objectType) => false;

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.StartObject)
        {
            var user = serializer.Deserialize<JsonUser>(reader);
            return user.User;
        }

        return null;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}