# 任务 2.3: WebFetch 工具

## 阶段
Phase 2 — 功能扩展

## 目标
新增 `webfetch` 工具，让 Agent 可以获取 URL 内容。

## 背景
开发中经常需要查阅文档、API 参考、Stack Overflow 答案等。当前 Agent 无法访问网络内容。

## 设计方案

### 工具定义

```csharp
// ToolRegistry.cs 新增
if (enabled.Contains("webfetch"))
    tools.Add(AIFunctionFactory.Create(
        ([Description("URL to fetch")] string url,
         [Description("Output format: 'text', 'markdown', or 'html' (default: markdown)")] string? format) =>
            webTools.FetchAsync(url, format ?? "markdown"),
        "webfetch",
        "Fetch content from a URL. Returns the page content in the specified format. " +
        "Use 'markdown' for documentation, 'text' for plain content."));
```

### WebTools 实现

```csharp
// src/tools/WebTools.cs

public class WebTools
{
    private readonly HttpClient _httpClient;
    private const int MaxResponseBytes = 5 * 1024 * 1024; // 5MB
    private const int TimeoutSeconds = 30;

    public WebTools()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; Morty/1.0)");
    }

    public async Task<string> FetchAsync(string url, string format = "markdown")
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxResponseBytes)
            return $"Error: Response too large ({contentLength} bytes, max {MaxResponseBytes})";

        var content = await response.Content.ReadAsStringAsync();

        return format.ToLower() switch
        {
            "text" => HtmlToText(content),
            "markdown" => HtmlToMarkdown(content),
            "html" => TruncateOutput(content),
            _ => HtmlToMarkdown(content)
        };
    }

    /// <summary>HTML 转纯文本</summary>
    private string HtmlToText(string html) { ... }

    /// <summary>HTML 转 Markdown (简易实现)</summary>
    private string HtmlToMarkdown(string html)
    {
        // 移除 script/style 标签
        // 转换 h1-h6 → # 标题
        // 转换 <a href> → [text](url)
        // 转换 <code> → `code`
        // 转换 <pre> → ```code block```
        // 转换 <li> → - item
        // 移除其他标签，保留文本
        ...
    }
}
```

## 实现步骤

1. [ ] 创建 `src/tools/WebTools.cs`
2. [ ] 实现 HTML → Markdown 转换 (基础版: 标题/链接/代码块/列表)
3. [ ] 实现 HTML → 纯文本
4. [ ] `ToolRegistry.cs` — 注册 webfetch 工具
5. [ ] 输出截断 (复用 OutputTruncator)
6. [ ] 超时和大小限制

## 验收标准
- [ ] 能获取 HTTP/HTTPS 页面内容
- [ ] HTML 转 Markdown 格式合理
- [ ] 超时和大小限制生效
- [ ] 输出自动截断

## 参考
- opencode: `src/tool/webfetch.ts`

## 相关文件
- `src/tools/WebTools.cs` — 新建
- `src/agent/ToolRegistry.cs` — 注册
