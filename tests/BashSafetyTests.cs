using Morty.Agent;

namespace Morty.Tests;

public class BashSafetyTests
{
    [Theory]
    [InlineData("ls", BashRiskLevel.Low)]
    [InlineData("cat file.txt", BashRiskLevel.Low)]
    [InlineData("grep -r pattern .", BashRiskLevel.Low)]
    [InlineData("git status", BashRiskLevel.Low)]
    [InlineData("git log --oneline", BashRiskLevel.Low)]
    [InlineData("pwd", BashRiskLevel.Low)]
    [InlineData("dotnet build", BashRiskLevel.Low)]
    [InlineData("dotnet test", BashRiskLevel.Low)]
    public void ReadOnlyCommands_Low(string command, BashRiskLevel expected)
    {
        Assert.Equal(expected, BashSafety.Analyze(command));
    }

    [Theory]
    [InlineData("rm file.txt", BashRiskLevel.High)]
    [InlineData("rm foo.txt", BashRiskLevel.High)]
    [InlineData("chmod 777 file", BashRiskLevel.High)]
    [InlineData("sudo anything", BashRiskLevel.High)]
    [InlineData("kill -9 1234", BashRiskLevel.High)]
    public void DangerousCommands_High(string command, BashRiskLevel expected)
    {
        Assert.Equal(expected, BashSafety.Analyze(command));
    }

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -rf ~")]
    [InlineData(":(){ :|:& };:")]
    public void CriticalPatterns_Critical(string command)
    {
        Assert.Equal(BashRiskLevel.Critical, BashSafety.Analyze(command));
    }

    [Theory]
    [InlineData("echo hello > file.txt", BashRiskLevel.Medium)]
    [InlineData("wget https://example.com", BashRiskLevel.Medium)]
    public void MediumRisk(string command, BashRiskLevel expected)
    {
        Assert.Equal(expected, BashSafety.Analyze(command));
    }

    [Fact]
    public void EmptyCommand_Low()
    {
        Assert.Equal(BashRiskLevel.Low, BashSafety.Analyze(""));
    }
}
