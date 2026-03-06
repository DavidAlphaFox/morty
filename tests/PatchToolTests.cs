using Morty.Tools;

namespace Morty.Tests;

public class PatchToolTests
{
    [Fact]
    public void ParsePatch_AddFile()
    {
        var patch = """
            *** Begin Patch
            *** Add File: hello.txt
            +Hello world
            +Second line
            *** End Patch
            """;

        var hunks = PatchTool.ParsePatch(patch);
        Assert.Single(hunks);
        var add = Assert.IsType<PatchTool.AddHunk>(hunks[0]);
        Assert.Equal("hello.txt", add.Path);
        Assert.Equal("Hello world\nSecond line", add.Contents);
    }

    [Fact]
    public void ParsePatch_DeleteFile()
    {
        var patch = """
            *** Begin Patch
            *** Delete File: obsolete.txt
            *** End Patch
            """;

        var hunks = PatchTool.ParsePatch(patch);
        Assert.Single(hunks);
        var del = Assert.IsType<PatchTool.DeleteHunk>(hunks[0]);
        Assert.Equal("obsolete.txt", del.Path);
    }

    [Fact]
    public void ParsePatch_UpdateFile()
    {
        var patch = """
            *** Begin Patch
            *** Update File: src/app.cs
            @@ void Main()
            -Console.WriteLine("Hi");
            +Console.WriteLine("Hello");
            *** End Patch
            """;

        var hunks = PatchTool.ParsePatch(patch);
        Assert.Single(hunks);
        var upd = Assert.IsType<PatchTool.UpdateHunk>(hunks[0]);
        Assert.Equal("src/app.cs", upd.Path);
        Assert.Null(upd.MovePath);
        Assert.Single(upd.Chunks);
        Assert.Equal("void Main()", upd.Chunks[0].ChangeContext);
    }

    [Fact]
    public void ParsePatch_UpdateWithMove()
    {
        var patch = """
            *** Begin Patch
            *** Update File: old.cs
            *** Move to: new.cs
            @@ class Foo
             unchanged
            -old line
            +new line
            *** End Patch
            """;

        var hunks = PatchTool.ParsePatch(patch);
        var upd = Assert.IsType<PatchTool.UpdateHunk>(hunks[0]);
        Assert.Equal("old.cs", upd.Path);
        Assert.Equal("new.cs", upd.MovePath);
    }

    [Fact]
    public void ParsePatch_MultipleHunks()
    {
        var patch = """
            *** Begin Patch
            *** Add File: a.txt
            +content a
            *** Delete File: b.txt
            *** Update File: c.txt
            @@ start
            -old
            +new
            *** End Patch
            """;

        var hunks = PatchTool.ParsePatch(patch);
        Assert.Equal(3, hunks.Count);
        Assert.IsType<PatchTool.AddHunk>(hunks[0]);
        Assert.IsType<PatchTool.DeleteHunk>(hunks[1]);
        Assert.IsType<PatchTool.UpdateHunk>(hunks[2]);
    }

    [Fact]
    public void ParsePatch_MissingMarkers_Throws()
    {
        var patch = "just some text";
        Assert.Throws<FormatException>(() => PatchTool.ParsePatch(patch));
    }

    [Fact]
    public void ParsePatch_HeredocWrapped()
    {
        var patch = """
            cat <<'EOF'
            *** Begin Patch
            *** Add File: test.txt
            +hello
            *** End Patch
            EOF
            """;

        var hunks = PatchTool.ParsePatch(patch);
        Assert.Single(hunks);
        Assert.IsType<PatchTool.AddHunk>(hunks[0]);
    }

    [Fact]
    public async Task ApplyAsync_AddFile()
    {
        var dir = CreateTempDir();
        var tool = new PatchTool(dir);

        var patch = """
            *** Begin Patch
            *** Add File: newfile.txt
            +line 1
            +line 2
            *** End Patch
            """;

        var result = await tool.ApplyAsync(patch);
        Assert.Contains("Added", result);
        Assert.True(File.Exists(Path.Combine(dir, "newfile.txt")));
        var content = await File.ReadAllTextAsync(Path.Combine(dir, "newfile.txt"));
        Assert.Contains("line 1", content);
    }

    [Fact]
    public async Task ApplyAsync_DeleteFile()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "todelete.txt"), "content");
        var tool = new PatchTool(dir);

        var patch = """
            *** Begin Patch
            *** Delete File: todelete.txt
            *** End Patch
            """;

        var result = await tool.ApplyAsync(patch);
        Assert.Contains("Deleted", result);
        Assert.False(File.Exists(Path.Combine(dir, "todelete.txt")));
    }

    [Fact]
    public async Task ApplyAsync_UpdateFile()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "app.cs"), "void Main()\n{\n    Print(\"Hi\");\n}\n");
        var tool = new PatchTool(dir);

        var patch = """
            *** Begin Patch
            *** Update File: app.cs
            @@
            -    Print("Hi");
            +    Print("Hello");
            *** End Patch
            """;

        var result = await tool.ApplyAsync(patch);
        Assert.Contains("Modified", result);
        var content = await File.ReadAllTextAsync(Path.Combine(dir, "app.cs"));
        Assert.Contains("Hello", content);
        Assert.DoesNotContain("Hi", content);
    }

    [Fact]
    public async Task ApplyAsync_MoveFile()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "old.txt"), "content\n");
        var tool = new PatchTool(dir);

        var patch = """
            *** Begin Patch
            *** Update File: old.txt
            *** Move to: new.txt
            @@
             content
            *** End Patch
            """;

        var result = await tool.ApplyAsync(patch);
        Assert.Contains("→", result);
        Assert.False(File.Exists(Path.Combine(dir, "old.txt")));
        Assert.True(File.Exists(Path.Combine(dir, "new.txt")));
    }

    [Fact]
    public async Task ApplyAsync_FileNotFound_Error()
    {
        var dir = CreateTempDir();
        var tool = new PatchTool(dir);

        var patch = """
            *** Begin Patch
            *** Update File: missing.txt
            @@
            -old
            +new
            *** End Patch
            """;

        var result = await tool.ApplyAsync(patch);
        Assert.Contains("Error", result);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "morty_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
