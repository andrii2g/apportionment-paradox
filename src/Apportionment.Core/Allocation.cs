using System.Numerics;
using Apportionment.Core.Arithmetic;

namespace Apportionment.Core;

public sealed record GroupAllocation(Group Group, Rational Quota, int Seats)
{
    public Rational QuotaDeviation => new Rational(Seats, 1) - Quota;
}

public sealed class Allocation
{
    public string Method { get; }
    public int HouseSize { get; }
    public IReadOnlyList<GroupAllocation> Groups { get; }
    public Rational TotalAbsoluteQuotaDeviation { get; }
    public Rational MaxAbsoluteQuotaDeviation { get; }

    internal Allocation(string method, Group[] groups, int houseSize, int[] seats)
    {
        if (seats.Length != groups.Length || seats.Any(s => s < 0) || seats.Sum(s => (long)s) != houseSize)
            throw new InvalidOperationException("An allocation must contain exactly the requested number of seats.");

        Method = method;
        HouseSize = houseSize;
        var population = groups.Aggregate(BigInteger.Zero, (sum, g) => sum + g.Population);
        Groups = Array.AsReadOnly(groups.Select((g, i) => new GroupAllocation(
            g, new Rational(houseSize * g.Population, population), seats[i])).ToArray());

        var deviations = Groups.Select(g => g.QuotaDeviation.Abs()).ToArray();
        TotalAbsoluteQuotaDeviation = deviations.Aggregate(Rational.Zero, (sum, d) => sum + d);
        MaxAbsoluteQuotaDeviation = deviations.Max()!;
    }
}
