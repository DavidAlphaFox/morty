// =============================================================================
// 凭证模型
// =============================================================================

using System.Text.Json.Serialization;

namespace Morty.Auth;

/// <summary>
/// API 凭证
/// </summary>
public class Credential
{
    /// <summary>
    /// 凭证类型: api, oauth 等
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "api";

    /// <summary>
    /// API Key
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";
}
