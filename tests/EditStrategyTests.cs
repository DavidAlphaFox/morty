using Morty.Tools;

namespace Morty.Tests;

public class EditStrategyTests
{
    [Fact]
    public void ExactMatch_Replaces()
    {
        var content = "hello world\nfoo bar\n";
        var result = EditStrategy.TryReplace(content, "foo bar", "baz qux");
        Assert.True(result.Success);
        Assert.Equal("Exact", result.Strategy);
        Assert.Contains("baz qux", result.Content);
    }

    [Fact]
    public void ExactMatch_MultipleOccurrences_UsesNonExactStrategy()
    {
        // "foo" appears twice - ExactReplacer fails due to uniqueness check
        // but other strategies (WhitespaceNormalized, etc.) may still match
        var content = "foo\nfoo\n";
        var result = EditStrategy.TryReplace(content, "foo", "bar");
        // Multiple matches — strategies with uniqueness checks should fail
        // but some strategies may succeed; that's by design
        if (result.Success)
            Assert.NotEqual("Exact", result.Strategy);
    }

    [Fact]
    public void ExactMatch_NotFound_Fails()
    {
        var content = "hello world";
        var result = EditStrategy.TryReplace(content, "not found", "replacement");
        Assert.False(result.Success);
    }

    [Fact]
    public void LineTrimmed_IgnoresTrailingSpaces()
    {
        var content = "hello   \nworld   \n";
        var result = EditStrategy.TryReplace(content, "hello\nworld", "foo\nbar");
        Assert.True(result.Success);
    }

    [Fact]
    public void WhitespaceNormalized_CollapsesSpaces()
    {
        var content = "  int   x  =  1;\n";
        var result = EditStrategy.TryReplace(content, "int x = 1;", "int x = 2;");
        Assert.True(result.Success);
    }

    [Fact]
    public void IndentationFlexible_HandlesOffsets()
    {
        var content = "    if (true)\n    {\n        return;\n    }\n";
        var result = EditStrategy.TryReplace(content,
            "if (true)\n{\n    return;\n}",
            "if (false)\n{\n    return;\n}");
        Assert.True(result.Success);
        Assert.Contains("if (false)", result.Content);
    }

    [Fact]
    public void BlockAnchor_MatchesFirstAndLastLine()
    {
        // BlockAnchor needs: first/last lines match at the exact line distance
        var content = "before\nstart\n  middle1\n  middle2\nend\nafter\n";
        var result = EditStrategy.TryReplace(content,
            "start\n  middle1\n  middle2\nend",
            "start\n  new content\n  updated\nend");
        Assert.True(result.Success);
        Assert.Contains("new content", result.Content);
        Assert.Contains("after", result.Content);
    }
}
