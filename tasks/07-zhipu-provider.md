# 任务: 实现 Zhipu Provider

## 阶段
Phase 3: LLM 提供商

## 描述
实现智谱 (Zhipu) LLM 提供商，支持 GLM-4 系列模型。

## 验收标准
- [ ] 可成功调用智谱 API
- [ ] 支持同步和流式输出
- [ ] 支持工具调用
- [ ] 正确处理错误

## 实现步骤

### 7.1 创建 ZhipuProvider 类
创建 `src/llm/ZhipuProvider.cs`:
```csharp
public class ZhipuProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    
    public string Name => "zhipu";
    
    public IReadOnlyList<string> SupportedModels => new[]
    {
        "glm-4",
        "glm-4-flash", 
        "glm-4-plus",
        "glm-4v-plus"
    };
    
    public ZhipuProvider(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://open.bigmodel.cn/api/paas/v4";
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }
}
```

### 7.2 实现 Chat 接口
```csharp
public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
{
    var payload = new
    {
        model = request.Model,
        messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToList(),
        temperature = request.Temperature,
        max_tokens = request.MaxTokens
    };
    
    var response = await _httpClient.PostAsJsonAsync(
        $"{_baseUrl}/chat/completions",
        payload,
        ct);
    
    response.EnsureSuccessStatusCode();
    
    var result = await response.Content.ReadFromJsonAsync<ZhipuResponse>(cancellationToken: ct);
    return MapToChatResponse(result);
}
```

### 7.3 实现流式输出
```csharp
public async IAsyncEnumerable<string> StreamChatAsync(
    ChatRequest request, 
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var payload = new
    {
        model = request.Model,
        messages = request.Messages,
        stream = true
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
            
            var chunk = JsonSerializer.Deserialize<ZhipuStreamChunk>(data);
            if (chunk?.Choices?.First()?.Delta?.Content is { } content)
            {
                yield return content;
            }
        }
    }
}
```

### 7.4 实现工具调用
```csharp
public async Task<ChatResponse> ChatWithToolsAsync(
    ChatRequest request, 
    IList<AgentTool> tools,
    CancellationToken ct = default)
{
    // 添加工具定义到请求
    var payload = new
    {
        model = request.Model,
        messages = request.Messages,
        tools = tools.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            }
        }).ToList()
    };
    
    // 发送请求并处理工具调用
    // ...
}
```

## API 端点
- Chat: `POST https://open.bigmodel.cn/api/paas/v4/chat/completions`
- Models: `GET https://open.bigmodel.cn/api/paas/v4/models`

## 相关文件
- tasks/06-llm-provider-interface.md
- design/dotnet-coding-agent.md (3.1.1 Zhipu Provider)
