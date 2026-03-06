# 任务: 实现 MiniMax Provider

## 阶段
Phase 3: LLM 提供商

## 描述
实现 MiniMax LLM 提供商，需要处理 HMAC-SHA256 API 签名认证。

## 验收标准
- [ ] 可成功调用 MiniMax API
- [ ] HMAC-SHA256 签名正确
- [ ] 支持同步和流式输出
- [ ] 支持工具调用

## 实现步骤

### 8.1 创建签名工具类
创建 `src/llm/MiniMaxSigner.cs`:
```csharp
public static class MiniMaxSigner
{
    public static string Sign(string apiKey, string method, string url, string timestamp, string body)
    {
        // 1. 拼接签名字符串
        var signString = $"{method}\n{url}\n{timestamp}\n{body}";
        
        // 2. HMAC-SHA256 计算
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signString));
        
        // 3. Base64 编码
        return Convert.ToBase64String(hash);
    }
    
    public static string GenerateAuthHeader(string apiKey, string method, string url, string body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Sign(apiKey, method, url, timestamp, body);
        
        return $"Bearer {apiKey}:{timestamp}:{signature}";
    }
}
```

### 8.2 创建 MiniMaxProvider 类
创建 `src/llm/MiniMaxProvider.cs`:
```csharp
public class MiniMaxProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    
    public string Name => "minimax";
    
    public IReadOnlyList<string> SupportedModels => new[]
    {
        "MiniMax-M2",
        "MiniMax-M2.1"
    };
    
    public MiniMaxProvider(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.minimax.io/v1";
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-Minimax-Api-Version", "2024-05-01");
    }
}
```

### 8.3 实现带签名的请求
```csharp
private async Task<HttpResponseMessage> SendRequestAsync(
    string method, 
    string endpoint, 
    object? body,
    CancellationToken ct = default)
{
    var url = $"{_baseUrl}{endpoint}";
    var bodyJson = body != null ? JsonSerializer.Serialize(body) : "";
    
    // 生成认证头
    var authHeader = MiniMaxSigner.GenerateAuthHeader(
        _apiKey, 
        method, 
        url, 
        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
        bodyJson);
    
    var request = new HttpRequestMessage(new HttpMethod(method), url);
    request.Headers.Add("Authorization", authHeader);
    
    if (!string.IsNullOrEmpty(bodyJson))
    {
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
    }
    
    return await _httpClient.SendAsync(request, ct);
}
```

### 8.4 实现 Chat 接口
```csharp
public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
{
    var payload = new
    {
        model = request.Model,
        messages = request.Messages,
        temperature = request.Temperature,
        max_tokens = request.MaxTokens
    };
    
    var response = await SendRequestAsync("POST", "/chat/completions", payload, ct);
    response.EnsureSuccessStatusCode();
    
    var result = await response.Content.ReadFromJsonAsync<MiniMaxResponse>(cancellationToken: ct);
    return MapToChatResponse(result);
}
```

## API 端点
- Chat: `POST https://api.minimax.io/v1/chat/completions`
- Embeddings: `POST https://api.minimax.io/v1/embeddings`
- Models: `GET https://api.minimax.io/v1/models`

## 认证方式
- Header: `Authorization: Bearer <api_key>:<timestamp>:<signature>`
- Header: `X-Minimax-Api-Version: 2024-05-01`

## 相关文件
- tasks/06-llm-provider-interface.md
- tasks/07-zhipu-provider.md
- design/dotnet-coding-agent.md (3.1.2 MiniMax Provider)
