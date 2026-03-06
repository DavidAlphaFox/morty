using Morty.Agent;

namespace Morty.Tests;

public class TodoListTests
{
    [Fact]
    public void Read_Empty_ReturnsMessage()
    {
        var todo = new TodoList();
        var result = todo.Read();
        Assert.Equal("No todos yet.", result);
    }

    [Fact]
    public void Write_ValidJson_UpdatesList()
    {
        var todo = new TodoList();
        var json = """
            [
                {"content": "Task 1", "status": "pending", "priority": "high"},
                {"content": "Task 2", "status": "in_progress", "priority": "medium"}
            ]
            """;
        var result = todo.Write(json);
        Assert.Contains("Task 1", result);
        Assert.Contains("Task 2", result);
        Assert.Contains("(HIGH)", result);
        Assert.Contains("[>]", result);
    }

    [Fact]
    public void Write_ThenRead_ShowsItems()
    {
        var todo = new TodoList();
        todo.Write("""[{"content":"Do thing","status":"completed","priority":"low"}]""");
        var result = todo.Read();
        Assert.Contains("[x]", result);
        Assert.Contains("Do thing", result);
        Assert.Contains("(low)", result);
        Assert.Contains("1/1 completed", result);
    }

    [Fact]
    public void Write_InvalidJson_ReturnsError()
    {
        var todo = new TodoList();
        var result = todo.Write("not json");
        Assert.Contains("Error", result);
    }

    [Fact]
    public void Write_ReplacesList()
    {
        var todo = new TodoList();
        todo.Write("""[{"content":"Old","status":"pending","priority":"medium"}]""");
        todo.Write("""[{"content":"New","status":"completed","priority":"high"}]""");
        var result = todo.Read();
        Assert.DoesNotContain("Old", result);
        Assert.Contains("New", result);
    }

    [Fact]
    public void Read_ShowsSummary()
    {
        var todo = new TodoList();
        todo.Write("""
            [
                {"content":"A","status":"completed","priority":"high"},
                {"content":"B","status":"in_progress","priority":"medium"},
                {"content":"C","status":"pending","priority":"low"},
                {"content":"D","status":"cancelled","priority":"medium"}
            ]
            """);
        var result = todo.Read();
        Assert.Contains("1/4 completed", result);
        Assert.Contains("1 in progress", result);
        Assert.Contains("1 pending", result);
        Assert.Contains("[-]", result); // cancelled
    }
}
