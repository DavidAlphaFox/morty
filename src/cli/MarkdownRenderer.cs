// =============================================================================
// Markdown 终端渲染器
// =============================================================================
// 将 Markdown 文本渲染为 ANSI 彩色终端输出
// 支持: 标题、代码块、内联代码、粗体、列表
// =============================================================================

using System.Text;
using System.Text.RegularExpressions;

namespace Morty.CLI;

/// <summary>
/// Markdown 终端渲染器
/// </summary>
public static class MarkdownRenderer
{
    // ANSI 颜色码
    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Dim = "\x1b[2m";
    private const string Cyan = "\x1b[36m";
    private const string Yellow = "\x1b[33m";
    private const string Green = "\x1b[32m";
    private const string Gray = "\x1b[90m";
    private const string BgGray = "\x1b[48;5;236m";

    /// <summary>
    /// 渲染 Markdown 到终端
    /// </summary>
    public static void Render(string markdown)
    {
        var lines = markdown.Split('\n');
        var inCodeBlock = false;

        foreach (var line in lines)
        {
            // 代码块处理
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    Console.Write($"{Gray}```{Reset}");
                    Console.WriteLine();
                    inCodeBlock = false;
                }
                else
                {
                    var lang = line.TrimStart()[3..].Trim();
                    Console.Write($"{Gray}```{lang}{Reset}");
                    Console.WriteLine();
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                Console.Write($"{BgGray}{Dim}  {line}{Reset}");
                Console.WriteLine();
                continue;
            }

            // 标题
            if (line.StartsWith("### "))
            {
                Console.Write($"{Bold}{Green}### {line.Substring(4)}{Reset}");
                Console.WriteLine();
                continue;
            }
            if (line.StartsWith("## "))
            {
                Console.Write($"{Bold}{Cyan}## {line.Substring(3)}{Reset}");
                Console.WriteLine();
                continue;
            }
            if (line.StartsWith("# "))
            {
                Console.Write($"{Bold}{Cyan}# {line.Substring(2)}{Reset}");
                Console.WriteLine();
                continue;
            }

            // 列表
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                var indent = line.Length - line.TrimStart().Length;
                Console.Write(new string(' ', indent));
                Console.Write($"{Green}•{Reset} ");
                RenderInline(line.TrimStart()[2..]);
                Console.WriteLine();
                continue;
            }

            // 普通行
            RenderInline(line);
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 渲染行内 Markdown (粗体、内联代码)
    /// </summary>
    private static void RenderInline(string text)
    {
        // 内联代码
        text = Regex.Replace(text, @"`([^`]+)`", m =>
            $"{Yellow}`{m.Groups[1].Value}`{Reset}");

        // 粗体
        text = Regex.Replace(text, @"\*\*([^*]+)\*\*", m =>
            $"{Bold}{m.Groups[1].Value}{Reset}");

        Console.Write(text);
    }
}

/// <summary>
/// 终端 Spinner 动画
/// </summary>
public class Spinner : IDisposable
{
    private readonly string _message;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _task;
    private static readonly string[] Frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    public Spinner(string message)
    {
        _message = message;
        _task = Task.Run(AnimateAsync);
    }

    private async Task AnimateAsync()
    {
        var i = 0;
        while (!_cts.Token.IsCancellationRequested)
        {
            Console.Write($"\r  {Frames[i % Frames.Length]} {_message}");
            i++;
            try
            {
                await Task.Delay(80, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // 清除 spinner 行
        Console.Write($"\r{new string(' ', _message.Length + 6)}\r");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _task.Wait();
        _cts.Dispose();
    }
}
