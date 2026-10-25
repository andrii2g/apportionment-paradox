namespace Apportionment.Core.Exploration;

public sealed record SeatLoss(string GroupId, int Before, int After)
{
    public int SeatsLost => Before - After;
}

public sealed class ParadoxTransition
{
    public Allocation Before { get; }
    public Allocation After { get; }
    public IReadOnlyList<SeatLoss> Losses { get; }
    public bool HasParadox => Losses.Count > 0;

    public ParadoxTransition(Allocation before, Allocation after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if ((long)before.HouseSize + 1 != after.HouseSize || before.Method != after.Method ||
            !before.Groups.Select(g => g.Group).SequenceEqual(after.Groups.Select(g => g.Group)))
            throw new ArgumentException("Compare the same method and ordered populations at consecutive house sizes.");

        Before = before;
        After = after;
        Losses = Array.AsReadOnly(before.Groups.Zip(after.Groups)
            .Where(pair => pair.Second.Seats < pair.First.Seats)
            .Select(pair => new SeatLoss(pair.First.Group.Id, pair.First.Seats, pair.Second.Seats)).ToArray());
    }
}
