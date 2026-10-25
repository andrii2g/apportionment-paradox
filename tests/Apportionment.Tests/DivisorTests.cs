using System.Numerics;
using Apportionment.Core;
using Apportionment.Core.Arithmetic;
using Apportionment.Core.Methods;

namespace Apportionment.Tests;

public class DivisorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExhaustiveCasesMatchGlobalPriorityTableAndAreHouseMonotone(bool webster)
    {
        IApportionmentMethod method = webster ? new WebsterMethod() : new JeffersonMethod();
        for (var a = 0; a <= 4; a++)
            for (var b = 0; b <= 4; b++)
                for (var c = 0; c <= 4; c++)
                {
                    if (a + b + c == 0) continue;
                    var groups = HamiltonTests.Groups(a, b, c);
                    var previous = new int[3];
                    for (var house = 0; house <= 25; house++)
                    {
                        var result = method.Allocate(groups, house);
                        var seats = result.Groups.Select(g => g.Seats).ToArray();
                        Assert.Equal(house, seats.Sum());
                        Assert.Equal(PriorityTable(groups, house, webster), seats);
                        for (var i = 0; i < seats.Length; i++)
                        {
                            Assert.True(seats[i] >= previous[i]);
                            if (groups[i].Population.IsZero) Assert.Equal(0, seats[i]);
                        }
                        previous = seats;
                    }
                }
    }

    // An independent oracle: materialize every group's possible seats, globally sort
    // their exact priorities, and take H entries instead of updating a running winner.
    private static int[] PriorityTable(Group[] groups, int house, bool webster)
    {
        var table = groups.SelectMany((g, i) => Enumerable.Range(0, house)
                .Where(_ => g.Population.Sign > 0)
                .Select(s => (Index: i, Priority: new Rational(g.Population, webster ? 2 * s + 1 : s + 1))))
            .OrderByDescending(entry => entry.Priority).ThenBy(entry => entry.Index).Take(house);
        var result = new int[groups.Length];
        foreach (var entry in table) result[entry.Index]++;
        return result;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactTiesHugePopulationsAndScalingAreHandled(bool webster)
    {
        IApportionmentMethod method = webster ? new WebsterMethod() : new JeffersonMethod();
        Assert.Equal([1, 1, 0], method.Allocate(HamiltonTests.Groups(1, 1, 1), 2).Groups.Select(g => g.Seats));
        var huge = BigInteger.Pow(10, 100);
        Assert.Equal([0, 1], method.Allocate([new("A", huge), new("B", huge + 1)], 1).Groups.Select(g => g.Seats));
        var groups = HamiltonTests.Groups(7, 3, 2);
        Assert.Equal(method.Allocate(groups, 37).Groups.Select(g => g.Seats),
            method.Allocate(groups.Select(g => g with { Population = g.Population * huge }).ToArray(), 37).Groups.Select(g => g.Seats));
        Assert.Equal([0, 17, 0], method.Allocate(HamiltonTests.Groups(0, 9, 0), 17).Groups.Select(g => g.Seats));
    }

    [Fact]
    public void JeffersonCanViolateUpperQuota()
    {
        var result = new JeffersonMethod().Allocate(HamiltonTests.Groups(6, 1, 1), 4);
        Assert.Equal([4, 0, 0], result.Groups.Select(g => g.Seats));
        Assert.Equal(new Rational(3, 1), result.Groups[0].Quota);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidInputsAndCancellationAreRejected(bool webster)
    {
        IApportionmentMethod method = webster ? new WebsterMethod() : new JeffersonMethod();
        Assert.Throws<ArgumentException>(() => method.Allocate([], 0));
        Assert.Throws<ArgumentException>(() => method.Allocate(HamiltonTests.Groups(0, 0), 3));
        Assert.Throws<ArgumentException>(() => method.Allocate(HamiltonTests.Groups(-1, 2), 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => method.Allocate(HamiltonTests.Groups(1), -1));
        Assert.Throws<OperationCanceledException>(() => method.Allocate(HamiltonTests.Groups(1), 100, new CancellationToken(true)));
    }
}
