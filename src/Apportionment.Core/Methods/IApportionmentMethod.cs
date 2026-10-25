namespace Apportionment.Core.Methods;

public interface IApportionmentMethod
{
    string Name { get; }

    Allocation Allocate(IReadOnlyList<Group> groups, int houseSize, CancellationToken cancellationToken = default);
}
