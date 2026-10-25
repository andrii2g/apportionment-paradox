namespace Apportionment.Core.Methods;

internal static class Problem
{
    internal static Group[] Validate(IReadOnlyList<Group> groups, int houseSize)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentOutOfRangeException.ThrowIfNegative(houseSize);
        if (groups.Count == 0)
            throw new ArgumentException("At least one group is required.", nameof(groups));

        var snapshot = groups.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in snapshot)
        {
            if (group is null || string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id))
                throw new ArgumentException("Group IDs must be nonempty and unique.", nameof(groups));
            if (group.Population.Sign < 0)
                throw new ArgumentException("Populations cannot be negative.", nameof(groups));
        }
        if (!snapshot.Any(g => g.Population.Sign > 0))
            throw new ArgumentException("The total population must be positive.", nameof(groups));

        return snapshot;
    }
}
