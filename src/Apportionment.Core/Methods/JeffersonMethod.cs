using System.Numerics;

namespace Apportionment.Core.Methods;

public sealed class JeffersonMethod : IApportionmentMethod
{
    public string Name => "Jefferson";

    public Allocation Allocate(IReadOnlyList<Group> groups, int houseSize, CancellationToken cancellationToken = default) =>
        DivisorAllocator.Allocate(Name, groups, houseSize, s => (BigInteger)s + 1, cancellationToken);
}
