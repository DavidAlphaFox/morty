using System.Text.Json.Serialization;

namespace Morty.Auth;

public class Credential
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "api";

    [JsonPropertyName("key")]
    public string Key { get; set; } = "";
}
