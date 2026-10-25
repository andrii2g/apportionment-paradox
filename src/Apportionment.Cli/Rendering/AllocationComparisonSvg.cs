using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Apportionment.Core;
using Apportionment.Core.Exploration;

namespace Apportionment.Cli.Rendering;

public static class AllocationComparisonSvg
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly string[] Colors = ["#315cce", "#008475", "#7952b3", "#b16b16", "#3a7b99", "#9b5685"];
    private const string LossColor = "#bc3048";

    public static string Render(IReadOnlyList<ParadoxTransition> comparisons)
    {
        ArgumentNullException.ThrowIfNull(comparisons);
        if (comparisons.Count is < 1 or > 3 || comparisons.Any(c => c is null))
            throw new ArgumentException("Provide one to three method comparisons.", nameof(comparisons));
        var first = comparisons[0];
        var groups = first.Before.Groups;
        if (groups.Count > 16)
            throw new ArgumentException("SVG comparisons support at most 16 groups.", nameof(comparisons));
        if (comparisons.Select(c => c.Before.Method).Distinct(StringComparer.Ordinal).Count() != comparisons.Count ||
            comparisons.Any(c => c.Before.HouseSize != first.Before.HouseSize ||
                !c.Before.Groups.Select(g => g.Group).SequenceEqual(groups.Select(g => g.Group))))
            throw new ArgumentException("Compare distinct methods with the same populations and house sizes.", nameof(comparisons));

        var maxSeats = Math.Max(1, comparisons.SelectMany(c => c.Before.Groups.Concat(c.After.Groups)).Max(g => g.Seats));
        var cardHeight = 100 + groups.Count * 58;
        var height = 152 + comparisons.Count * (cardHeight + 20) + 38;
        var root = new XElement(Svg + "svg",
            new XAttribute("width", 1200), new XAttribute("height", height),
            new XAttribute("viewBox", $"0 0 1200 {height.ToString(CultureInfo.InvariantCulture)}"),
            new XAttribute("role", "img"), new XAttribute("aria-labelledby", "title description"),
            new XAttribute("font-family", "Segoe UI, Arial, sans-serif"),
            new XElement(Svg + "title", new XAttribute("id", "title"), "Apportionment: one extra seat"),
            new XElement(Svg + "desc", new XAttribute("id", "description"),
                "Consecutive house sizes compared using exact allocation arithmetic. Red bars and explicit loss labels identify groups losing seats. Ties favor original input order."),
            Rect(0, 0, 1200, height, "#f3f5fa"),
            Text(32, 48, comparisons.Any(c => c.HasParadox)
                ? "One extra seat. An unexpected loss." : "One extra seat. Compare the allocations.", 30, "#17243c", "700"),
            Text(32, 80, $"{string.Join(", ", comparisons.Select(c => c.Before.Method))} · exact quotas · deterministic input-order ties", 16, "#56627a"),
            Text(32, 113, $"House {first.Before.HouseSize.ToString(CultureInfo.InvariantCulture)} → {first.After.HouseSize.ToString(CultureInfo.InvariantCulture)}   |   Shared bar scale: 0–{maxSeats.ToString(CultureInfo.InvariantCulture)} seats   |   Red + loss labels = fewer seats", 14, "#56627a"));

        for (var methodIndex = 0; methodIndex < comparisons.Count; methodIndex++)
        {
            var comparison = comparisons[methodIndex];
            var y = 152 + methodIndex * (cardHeight + 20);
            var card = new XElement(Svg + "g", new XAttribute("data-method", comparison.Before.Method),
                Rect(32, y, 1136, cardHeight, "#ffffff", 14),
                Text(56, y + 30, comparison.Before.Method, 21, "#17243c", "700"),
                Text(650, y + 30, comparison.HasParadox
                    ? $"{comparison.Losses.Count.ToString(CultureInfo.InvariantCulture)} group(s) lose seats"
                    : "No seat losses in this transition", 14, comparison.HasParadox ? LossColor : "#008475", "600"),
                Text(56, y + 58, $"BEFORE · {comparison.Before.HouseSize.ToString(CultureInfo.InvariantCulture)} seats", 12, "#56627a", "600"),
                Text(650, y + 58, $"AFTER · {comparison.After.HouseSize.ToString(CultureInfo.InvariantCulture)} seats", 12, "#56627a", "600"),
                new XElement(Svg + "line", new XAttribute("x1", 600), new XAttribute("x2", 600),
                    new XAttribute("y1", y + 48), new XAttribute("y2", y + cardHeight - 18), new XAttribute("stroke", "#e5e9f2")));

            for (var i = 0; i < groups.Count; i++)
            {
                var before = comparison.Before.Groups[i];
                var after = comparison.After.Groups[i];
                var losing = after.Seats < before.Seats;
                var rowY = y + 88 + i * 58;
                var color = losing ? LossColor : Colors[i % Colors.Length];
                if (losing)
                    card.Add(Rect(44, rowY - 17, 1112, 51, "#fff0f2", 6));
                DrawGroup(card, before, 56, rowY, maxSeats, color);
                DrawGroup(card, after, 650, rowY, maxSeats, color);
                if (losing)
                    card.Add(Text(987, rowY + 21,
                        FormattableString.Invariant($"{before.Seats} → {after.Seats} (−{before.Seats - after.Seats})"), 12, LossColor, "700"));
            }
            root.Add(card);
        }

        root.Add(Text(32, height - 20,
            "Populations are fixed. Quotas = house × population / total. No minimum-seat guarantee. Hover rows for full values.", 12, "#56627a"));
        return new XDocument(root).ToString() + "\n";
    }

    private static void DrawGroup(XElement parent, GroupAllocation allocation, int x, int y, int maxSeats, string color)
    {
        var row = new XElement(Svg + "g",
            new XElement(Svg + "title", FormattableString.Invariant(
                $"{allocation.Group.Id}; population {allocation.Group.Population}; exact quota {allocation.Quota}; seats {allocation.Seats}; deviation {allocation.QuotaDeviation}")),
            Text(x, y, Shorten(allocation.Group.Id, 10), 14, "#17243c", "600"),
            Text(x, y + 20, $"p={Shorten(allocation.Group.Population.ToString(CultureInfo.InvariantCulture), 10)}", 11, "#56627a"),
            Rect(x + 90, y - 12, 300, 16, "#e9edf5", 3),
            Rect(x + 90, y - 12, allocation.Seats * 300.0 / maxSeats, 16, color, 3),
            Text(x + 398, y + 1, allocation.Seats.ToString(CultureInfo.InvariantCulture), 15, color, "700"),
            Text(x + 90, y + 21, $"quota {Shorten(allocation.Quota.ToString(), 22)} ≈ {allocation.Quota.ToDecimalString(2)}", 11, "#56627a"));
        parent.Add(row);
    }

    private static string Shorten(string value, int length)
    {
        var runes = value.EnumerateRunes().ToArray();
        return runes.Length <= length ? value : string.Concat(runes.Take(length - 1).Select(r => r.ToString())) + "…";
    }

    private static XElement Text(double x, double y, string value, int size, string color, string weight = "400") =>
        new(Svg + "text", new XAttribute("x", F(x)), new XAttribute("y", F(y)),
            new XAttribute("font-size", size), new XAttribute("fill", color), new XAttribute("font-weight", weight), value);

    private static XElement Rect(double x, double y, double width, double height, string color, int radius = 0) =>
        new(Svg + "rect", new XAttribute("x", F(x)), new XAttribute("y", F(y)),
            new XAttribute("width", F(width)), new XAttribute("height", F(height)),
            new XAttribute("rx", radius), new XAttribute("fill", color));

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
