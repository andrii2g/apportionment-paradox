using System.Numerics;

namespace Apportionment.Core.Methods;

public sealed class WebsterMethod : IApportionmentMethod
{
    public string Name => "Webster";

    public Allocation Allocate(IReadOnlyList<Group> groups, int houseSize, CancellationToken cancellationToken = default) =>
        // Multiplying all p/(s+1/2) priorities by 1/2 leaves their order unchanged.
        DivisorAllocator.Allocate(Name, groups, houseSize, s => 2 * (BigInteger)s + 1, cancellationToken);
}
