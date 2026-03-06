// =============================================================================
// Web 工具
// =============================================================================
// 提供 URL 内容获取功能，支持 HTML → Markdown/Text 转换
// =============================================================================

using System.Text;
using System.Text.RegularExpressions;

namespace Morty.Tools;

/// <summary>
/// Web 工具 — 获取 URL 内容
/// </summary>
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

    /// <summary>
    /// 获取 URL 内容
    /// </summary>
    public async Task<string> FetchAsync(string url, string format = "markdown")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return $"Error: Invalid URL '{url}'. Only http/https supported.";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            return $"Error: Failed to fetch {url}: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return $"Error: Request timed out after {TimeoutSeconds}s";
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxResponseBytes)
            return $"Error: Response too large ({contentLength} bytes, max {MaxResponseBytes / 1024 / 1024}MB)";

        var content = await response.Content.ReadAsStringAsync();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var isHtml = contentType.Contains("html") || content.TrimStart().StartsWith("<");

        var result = format.ToLower() switch
        {
            "text" => isHtml ? HtmlToText(content) : content,
            "html" => content,
            _ => isHtml ? HtmlToMarkdown(content) : content
        };

        // 截断输出
        var truncation = OutputTruncator.TruncateHead(result);
        if (truncation.Truncated)
            return truncation.Content + "\n\n[Output truncated]";

        return result;
    }

    /// <summary>
    /// HTML → 纯文本
    /// </summary>
    private static string HtmlToText(string html)
    {
        // 移除 script/style
        html = Regex.Replace(html, @"<(script|style)[^>]*>[\s\S]*?</\1>", "", RegexOptions.IgnoreCase);
        // 块元素换行
        html = Regex.Replace(html, @"<(br|p|div|h[1-6]|li|tr)[^>]*>", "\n", RegexOptions.IgnoreCase);
        // 移除标签
        html = Regex.Replace(html, @"<[^>]+>", "");
        // 解码实体
        html = DecodeHtmlEntities(html);
        // 合并多余空行
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        return html.Trim();
    }

    /// <summary>
    /// HTML → Markdown (简易实现)
    /// </summary>
    private static string HtmlToMarkdown(string html)
    {
        // 移除 script/style/head
        html = Regex.Replace(html, @"<(script|style|head)[^>]*>[\s\S]*?</\1>", "", RegexOptions.IgnoreCase);

        // 标题 h1-h6
        for (var i = 1; i <= 6; i++)
        {
            var prefix = new string('#', i);
            html = Regex.Replace(html, $@"<h{i}[^>]*>(.*?)</h{i}>",
                m => $"\n{prefix} {StripTags(m.Groups[1].Value).Trim()}\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        // 代码块
        html = Regex.Replace(html, @"<pre[^>]*><code[^>]*>([\s\S]*?)</code></pre>",
            m => $"\n```\n{DecodeHtmlEntities(StripTags(m.Groups[1].Value))}\n```\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<pre[^>]*>([\s\S]*?)</pre>",
            m => $"\n```\n{DecodeHtmlEntities(StripTags(m.Groups[1].Value))}\n```\n", RegexOptions.IgnoreCase);

        // 内联代码
        html = Regex.Replace(html, @"<code[^>]*>(.*?)</code>",
            m => $"`{StripTags(m.Groups[1].Value)}`", RegexOptions.IgnoreCase);

        // 链接
        html = Regex.Replace(html, @"<a[^>]*href=""([^""]*?)""[^>]*>(.*?)</a>",
            m => $"[{StripTags(m.Groups[2].Value).Trim()}]({m.Groups[1].Value})", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 粗体/斜体
        html = Regex.Replace(html, @"<(strong|b)[^>]*>(.*?)</\1>",
            m => $"**{StripTags(m.Groups[2].Value)}**", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<(em|i)[^>]*>(.*?)</\1>",
            m => $"*{StripTags(m.Groups[2].Value)}*", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 列表
        html = Regex.Replace(html, @"<li[^>]*>(.*?)</li>",
            m => $"\n- {StripTags(m.Groups[1].Value).Trim()}", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 段落和换行
        html = Regex.Replace(html, @"<(p|div)[^>]*>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<br[^>]*/?>", "\n", RegexOptions.IgnoreCase);

        // 移除剩余标签
        html = Regex.Replace(html, @"<[^>]+>", "");

        // 解码实体
        html = DecodeHtmlEntities(html);

        // 清理多余空白
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        html = Regex.Replace(html, @"[ \t]+\n", "\n");

        return html.Trim();
    }

    private static string StripTags(string html) =>
        Regex.Replace(html, @"<[^>]+>", "");

    private static string DecodeHtmlEntities(string text)
    {
        text = text.Replace("&amp;", "&");
        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");
        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#39;", "'");
        text = text.Replace("&nbsp;", " ");
        // 数字实体
        text = Regex.Replace(text, @"&#(\d+);", m =>
            char.ConvertFromUtf32(int.Parse(m.Groups[1].Value)));
        return text;
    }
}
