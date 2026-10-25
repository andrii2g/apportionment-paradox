using System.Globalization;
using System.Numerics;
using Apportionment.Core;
using Apportionment.Core.Exploration;
using Apportionment.Core.Methods;

namespace Apportionment.Cli;

public static class CliApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Length == 0 || args is ["--help"] or ["help"])
            {
                output.WriteLine("Apportionment Paradox — exact proportional allocation");
                output.WriteLine("allocate --populations 5,3,1 --seats 4 --method all");
                output.WriteLine("sweep --populations 5,3,1 --from 1 --to 30 --method all");
                output.WriteLine("search --groups 3 --max-population 12 --from 1 --to 30 --method all");
                output.WriteLine("Exploration options: --max-configurations 100000 --examples 10 (search only).");
                output.WriteLine("Methods: hamilton, jefferson, webster, all (default: all).");
                output.WriteLine("Exact ties favor the first input group. No minimum-seat guarantee.");
                return 0;
            }

            var options = Options.Parse(args[1..]);
            switch (args[0])
            {
                case "allocate":
                    options.Allow("populations", "seats", "method");
                    var groups = options.Populations();
                    var house = options.Integer("seats", 0, 100_000);
                    foreach (var method in SelectMethods(options.Optional("method", "all")))
                        WriteAllocation(output, method.Allocate(groups, house, cancellationToken));
                    break;
                case "sweep":
                case "search":
                    Explore(args[0], options, output, cancellationToken);
                    break;
                default:
                    throw new ArgumentException($"Unknown command '{args[0]}'. Use --help.");
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    internal static IApportionmentMethod[] SelectMethods(string name) => name switch
    {
        "hamilton" => [new HamiltonMethod()],
        "jefferson" => [new JeffersonMethod()],
        "webster" => [new WebsterMethod()],
        "all" => [new HamiltonMethod(), new JeffersonMethod(), new WebsterMethod()],
        _ => throw new ArgumentException("Method must be hamilton, jefferson, webster, or all.")
    };

    private static void Explore(string command, Options options, TextWriter output, CancellationToken cancellationToken)
    {
        var search = command == "search";
        options.Allow(search
            ? ["groups", "max-population", "from", "to", "method", "max-configurations", "examples"]
            : ["populations", "from", "to", "method", "max-configurations"]);
        var from = options.Integer("from", 0, 100_000);
        var to = options.Integer("to", from, 100_000);
        var budget = options.Integer("max-configurations", 1, 1_000_000, 100_000);
        var examples = search ? options.Integer("examples", 0, 100, 10) : int.MaxValue;
        var methods = SelectMethods(options.Optional("method", "all"));
        IEnumerable<IReadOnlyList<Group>> distributions = search
            ? PopulationDistributions.Generate(options.Integer("groups", 1, 8),
                options.Integer("max-population", 1, 1000), cancellationToken)
            : new[] { options.Populations() };
        var shown = 0;
        var summary = Experiment.Run(distributions, methods, from, to, budget,
            onAllocation: search ? null : allocation => output.WriteLine(FormattableString.Invariant(
                $"{allocation.Method} H={allocation.HouseSize} seats=[{string.Join(',', allocation.Groups.Select(g => g.Seats.ToString(CultureInfo.InvariantCulture)))}] max|deviation|={allocation.MaxAbsoluteQuotaDeviation} sum|deviation|={allocation.TotalAbsoluteQuotaDeviation}")),
            onParadox: transition =>
            {
                if (shown++ < examples) WriteParadox(output, transition);
            }, cancellationToken: cancellationToken);
        output.WriteLine(summary.Completed ? "Status: complete" : "Status: configuration limit reached; results are partial");
        output.WriteLine(FormattableString.Invariant($"Population distributions examined: {summary.DistributionsExamined}"));
        output.WriteLine(FormattableString.Invariant($"Allocation configurations examined: {summary.ConfigurationsExamined}"));
        output.WriteLine(FormattableString.Invariant($"Adjacent transitions examined: {summary.TransitionsExamined}"));
        foreach (var m in summary.Methods)
        {
            output.WriteLine(FormattableString.Invariant(
                $"{m.Method}: configurations={m.ConfigurationsExamined}, seats allocated={m.SeatsAllocated}, transitions={m.TransitionsExamined}, paradox transitions={m.ParadoxTransitions}, losing group occurrences={m.LosingGroupOccurrences}, seats lost={m.SeatsLost}"));
            output.WriteLine($"  max|deviation|={m.MaxAbsoluteQuotaDeviation}, max sum|deviation|={m.MaxTotalAbsoluteQuotaDeviation}");
        }
    }

    private static void WriteParadox(TextWriter output, ParadoxTransition transition)
    {
        var populations = string.Join(',', transition.Before.Groups.Select(g => g.Group.Population.ToString(CultureInfo.InvariantCulture)));
        var losses = string.Join(", ", transition.Losses.Select(l => FormattableString.Invariant($"{l.GroupId}: {l.Before}->{l.After} (-{l.SeatsLost})")));
        output.WriteLine(FormattableString.Invariant(
            $"PARADOX {transition.Before.Method} populations=[{populations}] H={transition.Before.HouseSize}->{transition.After.HouseSize}: {losses}"));
    }

    internal static void WriteAllocation(TextWriter output, Allocation allocation)
    {
        output.WriteLine(FormattableString.Invariant($"{allocation.Method}: H={allocation.HouseSize}"));
        output.WriteLine("Group\tPopulation\tQuota (exact)\tSeats\tDeviation (exact)");
        foreach (var g in allocation.Groups)
            output.WriteLine(FormattableString.Invariant($"{g.Group.Id}\t{g.Group.Population}\t{g.Quota}\t{g.Seats}\t{g.QuotaDeviation}"));
        output.WriteLine(FormattableString.Invariant($"Seats allocated: {allocation.Groups.Sum(g => g.Seats)}"));
        output.WriteLine($"Total absolute quota deviation: {allocation.TotalAbsoluteQuotaDeviation}");
        output.WriteLine($"Maximum absolute quota deviation: {allocation.MaxAbsoluteQuotaDeviation}");
    }
}

internal sealed class Options(Dictionary<string, string> values)
{
    internal static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || args[i].Length == 2 ||
                i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Options must be pairs: --name value.");
            if (!values.TryAdd(args[i][2..], args[i + 1]))
                throw new ArgumentException($"Duplicate option '{args[i]}'.");
        }
        return new Options(values);
    }

    internal void Allow(params string[] names)
    {
        foreach (var name in values.Keys)
            if (!names.Contains(name, StringComparer.Ordinal))
                throw new ArgumentException($"Unknown option '--{name}'.");
    }

    internal string Required(string name) => values.TryGetValue(name, out var value)
        ? value : throw new ArgumentException($"Missing option '--{name}'.");

    internal string Optional(string name, string fallback) => values.GetValueOrDefault(name, fallback);

    internal int Integer(string name, int min, int max, int? fallback = null)
    {
        var raw = fallback is null ? Required(name) : Optional(name, fallback.Value.ToString(CultureInfo.InvariantCulture));
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            throw new ArgumentException(FormattableString.Invariant($"--{name} must be an integer in {min}..{max}."));
        return value;
    }

    internal Group[] Populations()
    {
        var parts = Required("populations").Split(',');
        if (parts.Length > 256)
            throw new ArgumentException("At most 256 groups are supported by the CLI.");
        return parts.Select((part, i) =>
        {
            if (part.Length > 256 || !BigInteger.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var population) || population.Sign < 0)
                throw new ArgumentException("Populations must be nonnegative integers of at most 256 characters.");
            return new Group(FormattableString.Invariant($"G{i + 1}"), population);
        }).ToArray();
    }
}
