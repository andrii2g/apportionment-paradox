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
    [InlineData("visualize --populations 5,3,1 --seats 4 --output example.txt")]
    [InlineData("visualize --populations 5,3,1 --seats 100000 --output example.svg")]
    [InlineData("sweep --populations 5,3,1 --from 5 --to 4")]
    [InlineData("search --groups 0 --max-population 5 --from 1 --to 5")]
    [InlineData("search --groups 3 --max-population 5 --from 1 --to 5 --max-configurations 0")]
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

    [Fact]
    public void SweepAndBoundedSearchExplainTheirResults()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, CliApplication.Run("sweep --populations 5,3,1 --from 4 --to 5".Split(' '), output, error));
        Assert.Contains("PARADOX Hamilton populations=[5,3,1] H=4->5: G3: 1->0 (-1)", output.ToString());
        Assert.Contains("Allocation configurations examined: 6", output.ToString());
        output.GetStringBuilder().Clear();
        Assert.Equal(0, CliApplication.Run("search --groups 3 --max-population 5 --from 1 --to 20 --max-configurations 5 --examples 0".Split(' '), output, error));
        Assert.Contains("results are partial", output.ToString());
        Assert.Contains("Allocation configurations examined: 5", output.ToString());
        Assert.DoesNotContain("PARADOX", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void MethodSelectionAndCancellationHaveExplicitResults()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = "allocate --populations 5,3,1 --seats 4 --method webster".Split(' ');
        Assert.Equal(0, CliApplication.Run(args, output, error));
        Assert.Contains("Webster: H=4", output.ToString());
        Assert.DoesNotContain("Hamilton:", output.ToString());
        Assert.DoesNotContain("Jefferson:", output.ToString());
        Assert.Equal(130, CliApplication.Run(args, output, error, new CancellationToken(true)));
        Assert.Equal("Cancelled.", error.ToString().Trim());
    }
}
