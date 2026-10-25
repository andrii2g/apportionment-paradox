using System.Numerics;
using Apportionment.Core;
using Apportionment.Core.Arithmetic;
using Apportionment.Core.Methods;

namespace Apportionment.Tests;

public class HamiltonTests
{
    internal static Group[] Groups(params int[] populations) => populations.Select((p, i) => new Group($"G{i + 1}", p)).ToArray();

    [Fact]
    public void AlabamaExampleHasNoTiesAndLosesASeat()
    {
        var method = new HamiltonMethod();
        var groups = Groups(5, 3, 1);
        var before = method.Allocate(groups, 4);
        var after = method.Allocate(groups, 5);
        Assert.Equal([2, 1, 1], before.Groups.Select(g => g.Seats));
        Assert.Equal([3, 2, 0], after.Groups.Select(g => g.Seats));
        Assert.Equal(new Rational(4, 9), before.Groups[2].Quota);
        Assert.Equal(new Rational(5, 9), after.Groups[2].Quota);
        Assert.Equal(new Rational(10, 9), before.TotalAbsoluteQuotaDeviation);
    }

    [Fact]
    public void CensusHamiltonExampleMatches()
    {
        var result = new HamiltonMethod().Allocate(Groups(2560, 3315, 995, 5012), 20);
        Assert.Equal([4, 6, 2, 8], result.Groups.Select(g => g.Seats));
    }

    [Fact]
    public void SmallCasesConserveSeatsAndRespectQuota()
    {
        var method = new HamiltonMethod();
        for (var a = 0; a <= 5; a++)
            for (var b = 0; b <= 5; b++)
                for (var c = 0; c <= 5; c++)
                {
                    if (a + b + c == 0) continue;
                    for (var house = 0; house <= 25; house++)
                    {
                        var result = method.Allocate(Groups(a, b, c), house);
                        Assert.Equal(house, result.Groups.Sum(g => g.Seats));
                        foreach (var g in result.Groups)
                        {
                            var floor = BigInteger.DivRem(g.Quota.Numerator, g.Quota.Denominator, out var remainder);
                            Assert.InRange(new BigInteger(g.Seats), floor, floor + (remainder.IsZero ? 0 : 1));
                            if (g.Group.Population.IsZero) Assert.Equal(0, g.Seats);
                        }
                    }
                }
    }

    [Fact]
    public void TiesUseInputOrderAndResultsSnapshotInputs()
    {
        var groups = Groups(1, 1, 1);
        var result = new HamiltonMethod().Allocate(groups, 2);
        groups[0] = new Group("changed", 500);
        Assert.Equal([1, 1, 0], result.Groups.Select(g => g.Seats));
        Assert.Equal("G1", result.Groups[0].Group.Id);
    }

    [Fact]
    public void LargePopulationsRemainExactAndScaleInvariant()
    {
        var scale = BigInteger.Pow(10, 100);
        var groups = new[] { new Group("A", scale + 1), new Group("B", scale) };
        Assert.Equal([1, 0], new HamiltonMethod().Allocate(groups, 1).Groups.Select(g => g.Seats));
        var original = Groups(5, 3, 1);
        var scaled = original.Select(g => g with { Population = g.Population * scale }).ToArray();
        Assert.Equal(new HamiltonMethod().Allocate(original, 17).Groups.Select(g => g.Seats),
            new HamiltonMethod().Allocate(scaled, 17).Groups.Select(g => g.Seats));
    }

    [Fact]
    public void InvalidInputsAreRejected()
    {
        var method = new HamiltonMethod();
        Assert.Throws<ArgumentException>(() => method.Allocate([], 1));
        Assert.Throws<ArgumentException>(() => method.Allocate(Groups(0, 0), 1));
        Assert.Throws<ArgumentException>(() => method.Allocate(Groups(-1, 2), 1));
        Assert.Throws<ArgumentException>(() => method.Allocate([new("A", 1), new("A", 2)], 1));
        Assert.Throws<ArgumentException>(() => method.Allocate([new(" ", 1)], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => method.Allocate(Groups(1), -1));
        Assert.Throws<OperationCanceledException>(() => method.Allocate(Groups(1), 5, new CancellationToken(true)));
    }

    [Fact]
    public void OneGroupCanReceiveIntMaxSeats()
    {
        Assert.Equal(int.MaxValue, new HamiltonMethod().Allocate(Groups(1), int.MaxValue).Groups[0].Seats);
    }
}
