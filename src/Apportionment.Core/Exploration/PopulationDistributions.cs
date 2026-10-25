using System.Globalization;

namespace Apportionment.Core.Exploration;

public static class PopulationDistributions
{
    /// <summary>Enumerates labeled tuples in lexicographic order, each population in 1..maxPopulation.</summary>
    public static IEnumerable<IReadOnlyList<Group>> Generate(int groupCount, int maxPopulation,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(groupCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPopulation, 1);
        return Enumerate();

        IEnumerable<IReadOnlyList<Group>> Enumerate()
        {
            var values = Enumerable.Repeat(1, groupCount).ToArray();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Array.AsReadOnly(values.Select((p, i) =>
                    new Group($"G{(i + 1).ToString(CultureInfo.InvariantCulture)}", p)).ToArray());
                var index = values.Length - 1;
                while (index >= 0 && values[index] == maxPopulation)
                    values[index--] = 1;
                if (index < 0) yield break;
                values[index]++;
            }
        }
    }
}
