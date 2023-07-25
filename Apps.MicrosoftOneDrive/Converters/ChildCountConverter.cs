using Newtonsoft.Json;

namespace Apps.MicrosoftOneDrive.Converters;

public class ChildCountConverter : JsonConverter<int?>
{
    private class JsonFolder
    {
        public int ChildCount { get; set; }
    }
    
    public override bool CanWrite => false;

    public override int? ReadJson(JsonReader reader, Type objectType, int? existingValue, bool hasExistingValue, 
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.StartObject)
        {
            var folder = serializer.Deserialize<JsonFolder>(reader);
            return folder.ChildCount;
        }

        return null;
    }

    public override void WriteJson(JsonWriter writer, int? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}