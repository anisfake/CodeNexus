using System.Text.Json;
using System.Text.Json.Serialization;
using CodeNexus.Application.Features.FocusSessions.DTOs;

namespace CodeNexus.API.Converters;

public class RawCodeJsonConverter : JsonConverter<CompleteSessionWithRawCodeRequest>
{
    public override CompleteSessionWithRawCodeRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        string? rawCode = null;
        string? rawSummary = null;
        bool isEarlyCompletion = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString()?.ToLowerInvariant();
                reader.Read();

                switch (propertyName)
                {
                    case "rawcode":
                        rawCode = reader.GetString();
                        break;
                    case "rawsummary":
                        rawSummary = reader.GetString();
                        break;
                    case "isearlycompletion":
                        isEarlyCompletion = reader.GetBoolean();
                        break;
                }
            }
        }

        return new CompleteSessionWithRawCodeRequest(rawCode, rawSummary, isEarlyCompletion);
    }

    public override void Write(Utf8JsonWriter writer, CompleteSessionWithRawCodeRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        if (value.RawCode != null)
        {
            writer.WriteString("rawCode", value.RawCode);
        }
        
        if (value.RawSummary != null)
        {
            writer.WriteString("rawSummary", value.RawSummary);
        }
        
        writer.WriteBoolean("isEarlyCompletion", value.IsEarlyCompletion);
        
        writer.WriteEndObject();
    }
}