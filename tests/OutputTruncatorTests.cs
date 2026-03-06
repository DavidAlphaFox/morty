using Morty.Tools;

namespace Morty.Tests;

public class OutputTruncatorTests
{
    [Fact]
    public void SmallContent_NotTruncated()
    {
        var result = OutputTruncator.TruncateHead("short content");
        Assert.False(result.Truncated);
        Assert.Equal("short content", result.Content);
    }

    [Fact]
    public void TruncateTail_SmallContent_NotTruncated()
    {
        var result = OutputTruncator.TruncateTail("short content");
        Assert.False(result.Truncated);
        Assert.Equal("short content", result.Content);
    }

    [Fact]
    public void TruncateHead_LargeContent_KeepsBeginning()
    {
        var lines = Enumerable.Range(1, 3000).Select(i => $"line {i}").ToArray();
        var content = string.Join("\n", lines);

        var result = OutputTruncator.TruncateHead(content);
        Assert.True(result.Truncated);
        // TruncateHead keeps the beginning
        Assert.Contains("line 1", result.Content);
        Assert.DoesNotContain("line 3000", result.Content);
    }

    [Fact]
    public void TruncateTail_LargeContent_KeepsEnd()
    {
        var lines = Enumerable.Range(1, 3000).Select(i => $"line {i}").ToArray();
        var content = string.Join("\n", lines);

        var result = OutputTruncator.TruncateTail(content);
        Assert.True(result.Truncated);
        // Should keep the end (tail truncation keeps the end)
        Assert.Contains("line 3000", result.Content);
    }
}
