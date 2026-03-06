// =============================================================================
// 凭证存储
// =============================================================================
// 负责将 API 凭证安全存储到本地文件
// 存储位置: ~/.local/share/morty/auth.json
// 文件权限: 600 (仅所有者可读写)
// =============================================================================

using System.Text.Json;

namespace Morty.Auth;

/// <summary>
/// 凭证存储
/// </summary>
public class AuthStore
{
    /// <summary>
    /// 凭证文件路径
    /// </summary>
    private readonly string _authFilePath;

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 初始化凭证存储
    /// </summary>
    public AuthStore()
    {
        // 获取存储目录: ~/.local/share/morty
        var shareDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "morty");
        
        // 确保目录存在
        Directory.CreateDirectory(shareDir);
        
        // 凭证文件路径
        _authFilePath = Path.Combine(shareDir, "auth.json");

        // JSON 序列化选项 (带缩进)
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    /// <summary>
    /// 加载凭证
    /// </summary>
    /// <returns>凭证字典，key 为提供商名称</returns>
    public Dictionary<string, Credential> Load()
    {
        // 文件不存在，返回空字典
        if (!File.Exists(_authFilePath))
            return new Dictionary<string, Credential>();

        try
        {
            var json = File.ReadAllText(_authFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, Credential>>(json) 
                ?? new Dictionary<string, Credential>();
        }
        catch
        {
            // 读取失败，返回空字典
            return new Dictionary<string, Credential>();
        }
    }

    /// <summary>
    /// 保存凭证
    /// </summary>
    /// <param name="credentials">凭证字典</param>
    public void Save(Dictionary<string, Credential> credentials)
    {
        // 序列化为 JSON
        var json = JsonSerializer.Serialize(credentials, _jsonOptions);
        
        // 写入文件
        File.WriteAllText(_authFilePath, json);

        // Linux 设置文件权限为 600 (仅所有者可读写)
        if (OperatingSystem.IsLinux())
        {
            SetFilePermissions();
        }
    }

    /// <summary>
    /// 设置文件权限 (Linux)
    /// </summary>
    private void SetFilePermissions()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = "600 \"\"",
                UseShellExecute = false
            };
        }
        catch
        {
            // 忽略权限设置错误
        }
    }
}
