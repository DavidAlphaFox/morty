using Morty.Agent;

namespace Morty.Tests;

public class PermissionCheckerTests
{
    [Fact]
    public async Task DefaultRules_ReadAllowed()
    {
        var checker = new PermissionChecker();
        var allowed = await checker.CheckAsync("read_file",
            new Dictionary<string, object?> { ["path"] = "test.cs" });
        Assert.True(allowed);
    }

    [Fact]
    public async Task DefaultRules_GrepAllowed()
    {
        var checker = new PermissionChecker();
        var allowed = await checker.CheckAsync("grep",
            new Dictionary<string, object?> { ["pattern"] = "test" });
        Assert.True(allowed);
    }

    [Fact]
    public async Task EditWithoutAskUser_Denied()
    {
        // No askUser callback → defaults to deny
        var checker = new PermissionChecker();
        var allowed = await checker.CheckAsync("edit_file",
            new Dictionary<string, object?> { ["path"] = "test.cs" });
        Assert.False(allowed);
    }

    [Fact]
    public async Task EditWithAskUser_Allowed()
    {
        var checker = new PermissionChecker(askUser: _ =>
            Task.FromResult(PermissionAction.Allow));
        var allowed = await checker.CheckAsync("edit_file",
            new Dictionary<string, object?> { ["path"] = "test.cs" });
        Assert.True(allowed);
    }

    [Fact]
    public async Task BashReadOnly_AutoAllowed()
    {
        var checker = new PermissionChecker();
        var allowed = await checker.CheckAsync("bash",
            new Dictionary<string, object?> { ["command"] = "ls -la" });
        Assert.True(allowed);
    }

    [Fact]
    public async Task BashDangerous_NeedsPermission()
    {
        var checker = new PermissionChecker();
        // No askUser → denied
        var allowed = await checker.CheckAsync("bash",
            new Dictionary<string, object?> { ["command"] = "rm -rf /tmp/test" });
        Assert.False(allowed);
    }

    [Fact]
    public async Task CustomDenyRule_Blocks()
    {
        var rules = new List<PermissionRule>
        {
            new() { Permission = "read", Pattern = "*", Action = PermissionAction.Deny }
        };
        var checker = new PermissionChecker(rules);
        var allowed = await checker.CheckAsync("read_file",
            new Dictionary<string, object?> { ["path"] = "test.cs" });
        Assert.False(allowed);
    }

    [Fact]
    public async Task UnknownTool_Allowed()
    {
        var checker = new PermissionChecker();
        var allowed = await checker.CheckAsync("unknown_tool", null);
        Assert.True(allowed);
    }
}
