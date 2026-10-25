using Apportionment.Core.Arithmetic;
using Apportionment.Core.Methods;

namespace Apportionment.Core.Exploration;

public sealed record MethodMetrics(
    string Method,
    long ConfigurationsExamined,
    long SeatsAllocated,
    long TransitionsExamined,
    long ParadoxTransitions,
    long LosingGroupOccurrences,
    long SeatsLost,
    Rational MaxAbsoluteQuotaDeviation,
    Rational MaxTotalAbsoluteQuotaDeviation);

public sealed record ExperimentSummary(
    long DistributionsExamined,
    bool Completed,
    IReadOnlyList<MethodMetrics> Methods)
{
    public long ConfigurationsExamined => Methods.Sum(m => m.ConfigurationsExamined);
    public long TransitionsExamined => Methods.Sum(m => m.TransitionsExamined);
}

public static class Experiment
{
    /// <summary>Streams allocations and paradox events; retains counters rather than all results.</summary>
    public static ExperimentSummary Run(
        IEnumerable<IReadOnlyList<Group>> distributions,
        IReadOnlyList<IApportionmentMethod> methods,
        int fromHouse, int toHouse, long maxConfigurations = 100_000,
        Action<Allocation>? onAllocation = null,
        Action<ParadoxTransition>? onParadox = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distributions);
        ArgumentNullException.ThrowIfNull(methods);
        ArgumentOutOfRangeException.ThrowIfNegative(fromHouse);
        ArgumentOutOfRangeException.ThrowIfLessThan(toHouse, fromHouse);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConfigurations, 1);
        var strategies = methods.ToArray();
        if (strategies.Length == 0 || strategies.Any(m => m is null || string.IsNullOrWhiteSpace(m.Name)) ||
            strategies.Select(m => m.Name).Distinct(StringComparer.Ordinal).Count() != strategies.Length)
            throw new ArgumentException("Provide at least one method with a unique, nonempty name.", nameof(methods));

        var metrics = strategies.Select(m => new Counters(m.Name)).ToArray();
        long configurations = 0, distributionsExamined = 0;
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var distribution in distributions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groups = Problem.Validate(distribution, fromHouse);
            var started = false;
            for (var methodIndex = 0; methodIndex < strategies.Length; methodIndex++)
            {
                var counter = metrics[methodIndex];
                Allocation? previous = null;
                // A long loop variable also handles an inclusive int.MaxValue endpoint.
                for (long house = fromHouse; house <= toHouse; house++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (configurations == maxConfigurations) return Summary(completed: false);
                    if (!started)
                    {
                        distributionsExamined = checked(distributionsExamined + 1);
                        started = true;
                    }
                    var allocation = strategies[methodIndex].Allocate(groups, (int)house, cancellationToken);
                    configurations++;
                    counter.Add(allocation);
                    onAllocation?.Invoke(allocation);
                    if (previous is not null)
                    {
                        var transition = new ParadoxTransition(previous, allocation);
                        counter.Transitions = checked(counter.Transitions + 1);
                        if (transition.HasParadox)
                        {
                            counter.Paradoxes = checked(counter.Paradoxes + 1);
                            counter.LosingGroups = checked(counter.LosingGroups + transition.Losses.Count);
                            counter.SeatsLost = checked(counter.SeatsLost + transition.Losses.Sum(l => (long)l.SeatsLost));
                            onParadox?.Invoke(transition);
                        }
                    }
                    previous = allocation;
                }
            }
        }
        return Summary(completed: true);

        ExperimentSummary Summary(bool completed) => new(distributionsExamined, completed,
            Array.AsReadOnly(metrics.Select(m => m.Snapshot()).ToArray()));
    }

    private sealed class Counters(string method)
    {
        private long configurations, seats;
        internal long Transitions, Paradoxes, LosingGroups, SeatsLost;
        private Rational maxDeviation = Rational.Zero, maxTotalDeviation = Rational.Zero;

        internal void Add(Allocation allocation)
        {
            configurations = checked(configurations + 1);
            seats = checked(seats + allocation.HouseSize);
            if (allocation.MaxAbsoluteQuotaDeviation.CompareTo(maxDeviation) > 0)
                maxDeviation = allocation.MaxAbsoluteQuotaDeviation;
            if (allocation.TotalAbsoluteQuotaDeviation.CompareTo(maxTotalDeviation) > 0)
                maxTotalDeviation = allocation.TotalAbsoluteQuotaDeviation;
        }

        internal MethodMetrics Snapshot() => new(method, configurations, seats, Transitions, Paradoxes,
            LosingGroups, SeatsLost, maxDeviation, maxTotalDeviation);
    }
}
