using System.Numerics;

namespace Apportionment.Core.Methods;

internal static class DivisorAllocator
{
    internal static Allocation Allocate(string name, IReadOnlyList<Group> groups, int houseSize,
        Func<int, BigInteger> nextDivisor, CancellationToken cancellationToken)
    {
        var snapshot = Problem.Validate(groups, houseSize);
        cancellationToken.ThrowIfCancellationRequested();
        var seats = new int[snapshot.Length];
        var divisors = Enumerable.Repeat(nextDivisor(0), snapshot.Length).ToArray();

        for (var seat = 0; seat < houseSize; seat++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var best = 0;
            for (var i = 1; i < snapshot.Length; i++)
            {
                // Strict improvement preserves input-order ties; no floating-point division.
                if (snapshot[i].Population * divisors[best] > snapshot[best].Population * divisors[i])
                    best = i;
            }
            seats[best]++;
            divisors[best] = nextDivisor(seats[best]);
        }

        return new Allocation(name, snapshot, houseSize, seats);
    }
}
