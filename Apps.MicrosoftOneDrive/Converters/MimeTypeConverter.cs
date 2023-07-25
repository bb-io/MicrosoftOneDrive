using Newtonsoft.Json;

namespace Apps.MicrosoftOneDrive.Converters;

public class MimeTypeConverter : JsonConverter
{
    private class JsonFile
    {
        public string MimeType { get; set; }
        public Dictionary<string, string> Hashes { get; set; }
    }
    
    public override bool CanWrite => false;
    public override bool CanConvert(Type objectType) => false;

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.StartObject)
        {
            var file = serializer.Deserialize<JsonFile>(reader);
            return file.MimeType;
        }

        return null;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
