using System.Numerics;

namespace Apportionment.Core.Methods;

public sealed class HamiltonMethod : IApportionmentMethod
{
    public string Name => "Hamilton";

    public Allocation Allocate(IReadOnlyList<Group> groups, int houseSize, CancellationToken cancellationToken = default)
    {
        var snapshot = Problem.Validate(groups, houseSize);
        cancellationToken.ThrowIfCancellationRequested();
        var total = snapshot.Aggregate(BigInteger.Zero, (sum, g) => sum + g.Population);
        var seats = new int[snapshot.Length];
        var remainders = new BigInteger[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            seats[i] = (int)BigInteger.DivRem(houseSize * snapshot[i].Population, total, out remainders[i]);
        }

        var remaining = houseSize - seats.Sum();
        // Every quota has the same denominator. Index explicitly breaks exact ties.
        foreach (var index in Enumerable.Range(0, snapshot.Length)
                     .OrderByDescending(i => remainders[i]).ThenBy(i => i).Take(remaining))
            seats[index]++;

        return new Allocation(Name, snapshot, houseSize, seats);
    }
}
