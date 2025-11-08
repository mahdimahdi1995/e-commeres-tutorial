using System.Text.Json.Serialization;
using Core.Serialization;

namespace Core.Entities;

public class BaseEntity
{
    // Map to Cosmos 'id' field for SDK
    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringIntJsonConverter))]
    public int Id { get; set; }
}
