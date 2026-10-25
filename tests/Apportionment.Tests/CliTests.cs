using Apportionment.Cli;

namespace Apportionment.Tests;

public class CliTests
{
    [Fact]
    public void AllocatePrintsExactResults()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, CliApplication.Run(["allocate", "--populations", "5,3,1", "--seats", "4"], output, error));
        Assert.Contains("G3\t1\t4/9\t1\t5/9", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Theory]
    [InlineData("allocate --populations 1 --seats 4 --method unknown")]
    [InlineData("allocate --populations 0,0 --seats 4")]
    [InlineData("allocate --populations 1 --seats -1")]
    [InlineData("allocate --populations 1 --seats 4 --oops x")]
    [InlineData("allocate --populations 1 --seats 4 --seats 5")]
    [InlineData("allocate --populations 1 --seats")]
    public void InvalidCommandsReturnAnError(string command)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, CliApplication.Run(command.Split(' '), output, error));
        Assert.StartsWith("Error:", error.ToString());
    }
}
