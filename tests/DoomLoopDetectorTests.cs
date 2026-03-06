using Morty.Agent;

namespace Morty.Tests;

public class DoomLoopDetectorTests
{
    [Fact]
    public void NoLoop_ReturnsFalse()
    {
        var detector = new DoomLoopDetector(3);
        Assert.False(detector.RecordAndCheck("tool1", new Dictionary<string, object?> { ["a"] = "1" }));
        Assert.False(detector.RecordAndCheck("tool2", new Dictionary<string, object?> { ["a"] = "2" }));
    }

    [Fact]
    public void SameCallThreeTimes_DetectsLoop()
    {
        var detector = new DoomLoopDetector(3);
        var args = new Dictionary<string, object?> { ["path"] = "file.cs" };
        Assert.False(detector.RecordAndCheck("read_file", args));
        Assert.False(detector.RecordAndCheck("read_file", args));
        Assert.True(detector.RecordAndCheck("read_file", args));
    }

    [Fact]
    public void DifferentArgs_NoLoop()
    {
        var detector = new DoomLoopDetector(3);
        Assert.False(detector.RecordAndCheck("read_file", new Dictionary<string, object?> { ["path"] = "a.cs" }));
        Assert.False(detector.RecordAndCheck("read_file", new Dictionary<string, object?> { ["path"] = "b.cs" }));
        Assert.False(detector.RecordAndCheck("read_file", new Dictionary<string, object?> { ["path"] = "c.cs" }));
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var detector = new DoomLoopDetector(3);
        var args = new Dictionary<string, object?> { ["path"] = "file.cs" };
        detector.RecordAndCheck("read_file", args);
        detector.RecordAndCheck("read_file", args);
        detector.Reset();
        Assert.False(detector.RecordAndCheck("read_file", args));
    }
}
