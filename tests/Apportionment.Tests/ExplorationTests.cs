using Apportionment.Core;
using Apportionment.Core.Exploration;
using Apportionment.Core.Methods;

namespace Apportionment.Tests;

public class ExplorationTests
{
    private static IApportionmentMethod[] AllMethods() => [new HamiltonMethod(), new JeffersonMethod(), new WebsterMethod()];

    [Fact]
    public void GeneratorIsLexicographicLabeledAndReturnsIndependentSnapshots()
    {
        var generated = PopulationDistributions.Generate(2, 2).ToArray();
        Assert.Equal(4, generated.Length);
        Assert.Equal(["1,1", "1,2", "2,1", "2,2"], generated.Select(g => string.Join(',', g.Select(x => x.Population))));
        Assert.All(generated, g => Assert.Equal(["G1", "G2"], g.Select(x => x.Id)));
        Assert.Single(PopulationDistributions.Generate(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PopulationDistributions.Generate(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => PopulationDistributions.Generate(2, 0));
    }

    [Fact]
    public void SweepFindsExampleAndCountsEachMetricPrecisely()
    {
        var events = new List<ParadoxTransition>();
        var summary = Experiment.Run([HamiltonTests.Groups(5, 3, 1)], AllMethods(), 4, 5, onParadox: events.Add);
        Assert.True(summary.Completed);
        Assert.Equal(1, summary.DistributionsExamined);
        Assert.Equal(6, summary.ConfigurationsExamined);
        Assert.Equal(3, summary.TransitionsExamined);
        var transition = Assert.Single(events);
        var loss = Assert.Single(transition.Losses);
        Assert.Equal(new SeatLoss("G3", 1, 0), loss);
        Assert.Equal(1, loss.SeatsLost);
        Assert.Equal(1, summary.Methods[0].ParadoxTransitions);
        Assert.Equal(1, summary.Methods[0].LosingGroupOccurrences);
        Assert.Equal(1, summary.Methods[0].SeatsLost);
        Assert.All(summary.Methods, m => Assert.Equal(9, m.SeatsAllocated));
        Assert.All(summary.Methods.Skip(1), m => Assert.Equal(0, m.ParadoxTransitions));
    }

    [Fact]
    public void MultipleLosingGroupsAreOneParadoxTransition()
    {
        var summary = Experiment.Run([HamiltonTests.Groups(1500, 1500, 900, 500, 500, 200)],
            [new HamiltonMethod()], 25, 26);
        var metric = Assert.Single(summary.Methods);
        Assert.Equal(1, metric.ParadoxTransitions);
        Assert.Equal(2, metric.LosingGroupOccurrences);
        Assert.Equal(2, metric.SeatsLost);
    }

    [Fact]
    public void SearchIsDeterministicAndDivisorMethodsHaveNoParadoxes()
    {
        var first = new List<string>();
        var second = new List<string>();
        ExperimentSummary Run(List<string> events) => Experiment.Run(PopulationDistributions.Generate(3, 5),
            AllMethods(), 0, 20, onParadox: t => events.Add(
                $"{string.Join(',', t.Before.Groups.Select(g => g.Group.Population))}:{t.Before.HouseSize}"));
        var summary = Run(first);
        Run(second);
        Assert.Equal(first, second);
        Assert.NotEmpty(first);
        Assert.True(summary.Completed);
        Assert.Equal(125, summary.DistributionsExamined);
        Assert.Equal(125 * 21 * 3, summary.ConfigurationsExamined);
        Assert.Equal(125 * 20 * 3, summary.TransitionsExamined);
        Assert.All(summary.Methods.Skip(1), m => Assert.Equal(0, m.ParadoxTransitions));
    }

    [Fact]
    public void BudgetStopsBeforeExtraAllocationAndReportsPartialWork()
    {
        var allocations = 0;
        var summary = Experiment.Run(PopulationDistributions.Generate(2, 3), AllMethods(), 1, 3,
            maxConfigurations: 5, onAllocation: _ => allocations++);
        Assert.False(summary.Completed);
        Assert.Equal(5, allocations);
        Assert.Equal(5, summary.ConfigurationsExamined);
        Assert.Equal(1, summary.DistributionsExamined);
        Assert.Equal([3L, 2L, 0L], summary.Methods.Select(m => m.ConfigurationsExamined));
        Assert.Equal(3, summary.TransitionsExamined);

        var exact = Experiment.Run([HamiltonTests.Groups(1)], AllMethods(), 0, 0, maxConfigurations: 3);
        Assert.True(exact.Completed);
        Assert.Equal(0, exact.TransitionsExamined);

        // Exhausting the budget must not validate an unexamined distribution.
        var boundary = Experiment.Run([HamiltonTests.Groups(1), HamiltonTests.Groups(-1)],
            [new HamiltonMethod()], 0, 0, maxConfigurations: 1);
        Assert.False(boundary.Completed);
        Assert.Equal(1, boundary.DistributionsExamined);
    }

    [Fact]
    public void TransitionRejectsUnrelatedProblems()
    {
        var h = new HamiltonMethod();
        var before = h.Allocate(HamiltonTests.Groups(5, 3, 1), 4);
        Assert.Throws<ArgumentException>(() => new ParadoxTransition(before, h.Allocate(HamiltonTests.Groups(5, 3, 1), 6)));
        Assert.Throws<ArgumentException>(() => new ParadoxTransition(before, h.Allocate(HamiltonTests.Groups(3, 5, 1), 5)));
        Assert.Throws<ArgumentException>(() => new ParadoxTransition(before, new JeffersonMethod().Allocate(HamiltonTests.Groups(5, 3, 1), 5)));
    }

    [Fact]
    public void ExplorationValidatesRangesAndSupportsCancellationDuringWork()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Experiment.Run([], AllMethods(), 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Experiment.Run([], AllMethods(), -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Experiment.Run([], AllMethods(), 0, 1, 0));
        Assert.Throws<ArgumentException>(() => Experiment.Run([], [], 0, 1));
        Assert.Throws<ArgumentException>(() => Experiment.Run([], [new HamiltonMethod(), new HamiltonMethod()], 0, 1));
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => Experiment.Run(PopulationDistributions.Generate(2, 2),
            AllMethods(), 0, 20, onAllocation: _ => cancellation.Cancel(), cancellationToken: cancellation.Token));
    }
}
