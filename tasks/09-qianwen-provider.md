# 任务: 实现 Qianwen Provider

## 阶段
Phase 3: LLM 提供商

## 描述
实现百炼 (Qianwen/qwen3) LLM 提供商。

## 验收标准
- [ ] 可成功调用百炼 API
- [ ] 支持同步和流式输出
- [ ] 支持工具调用 (qwen3)
- [ ] 支持视觉模型 (qwen2.5-vl)

## 实现步骤

### 9.1 创建 QianwenProvider 类
创建 `src/llm/QianwenProvider.cs`:
```csharp
public class QianwenProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    
    public string Name => "qianwen";
    
    public IReadOnlyList<string> SupportedModels => new[]
    {
        "qwen-turbo",
        "qwen-plus", 
        "qwen-max",
        "qwen-long",
        "qwen2.5-coder",
        "qwen2.5-coder-32b",
        "qwen2.5-vl",
        "qwen2.5-vl-32b"
    };
    
    public QianwenProvider(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }
}
```

### 9.2 实现 Chat 接口
```csharp
public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
{
    var payload = new
    {
        model = request.Model,
        messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Role == "user" && HasImages(m) 
                ? ConvertToVisionContent(m) 
                : m.Content
        }).ToList(),
        temperature = request.Temperature,
        max_tokens = request.MaxTokens,
        enable_stream = false
    };
    
    var response = await _httpClient.PostAsJsonAsync(
        $"{_baseUrl}/chat/completions",
        payload,
        ct);
    
    response.EnsureSuccessStatusCode();
    
    var result = await response.Content.ReadFromJsonAsync<QianwenResponse>(cancellationToken: ct);
    return MapToChatResponse(result);
}
```

### 9.3 实现流式输出
```csharp
public async IAsyncEnumerable<string> StreamChatAsync(
    ChatRequest request, 
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var payload = new
    {
        model = request.Model,
        messages = request.Messages,
        temperature = request.Temperature,
        max_tokens = request.MaxTokens,
        enable_stream = true
    };
    
    var content = new StringContent(
        JsonSerializer.Serialize(payload),
        Encoding.UTF8,
        "application/json");
    
    var response = await _httpClient.PostAsync(
        $"{_baseUrl}/chat/completions",
        content,
        ct);
    
    using var stream = await response.Content.ReadAsStreamAsync(ct);
    using var reader = new StreamReader(stream);
    
    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync(ct);
        if (line?.StartsWith("data: ") == true)
        {
            var data = line[6..];
            if (data == "[DONE]") yield break;
            
            var chunk = JsonSerializer.Deserialize<QianwenStreamChunk>(data);
            if (chunk?.Choices?.First()?.Delta?.Content is { } text)
            {
                yield return text;
            }
        }
    }
}
```

### 9.4 视觉模型支持
```csharp
private object ConvertToVisionContent(ChatMessage message)
{
    // qwen2.5-vl 支持图片输入
    return new
    {
        text = message.Content,
        images = ExtractImageUrls(message.Content)
    };
}
```

## API 端点
- Chat: `POST https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions`
- Models: `GET https://dashscope.aliyuncs.com/api/v1/models`

## 认证方式
- Header: `Authorization: Bearer <api_key>`

## 相关文件
- tasks/06-llm-provider-interface.md
- tasks/07-zhipu-provider.md
- design/dotnet-coding-agent.md (3.1.3 Qianwen Provider)
