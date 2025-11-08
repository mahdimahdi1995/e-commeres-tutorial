using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Serialization;

/// <summary>
/// Converts between JSON string/number values and int in .NET.
/// Ensures Cosmos 'id' (string) can be bound to int properties and written back as string.
/// </summary>
public sealed class StringIntJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => int.TryParse(reader.GetString(), out var i) ? i : 0,
            JsonTokenType.Number => reader.TryGetInt32(out var n) ? n : 0,
            _ => 0
        };
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        // Cosmos requires 'id' to be a string; write numeric ids as strings
        writer.WriteStringValue(value.ToString());
    }
}
