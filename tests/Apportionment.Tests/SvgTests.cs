using System.Globalization;
using System.Xml.Linq;
using Apportionment.Cli;
using Apportionment.Cli.Rendering;
using Apportionment.Core;
using Apportionment.Core.Exploration;
using Apportionment.Core.Methods;

namespace Apportionment.Tests;

public class SvgTests
{
    private static ParadoxTransition Compare(IApportionmentMethod method, Group[]? groups = null, int house = 4)
    {
        groups ??= HamiltonTests.Groups(5, 3, 1);
        return new ParadoxTransition(method.Allocate(groups, house), method.Allocate(groups, house + 1));
    }

    [Fact]
    public void SvgShowsAllMethodsAndLossWithSharedScales()
    {
        var svg = AllocationComparisonSvg.Render([
            Compare(new HamiltonMethod()), Compare(new JeffersonMethod()), Compare(new WebsterMethod())]);
        var document = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Equal(ns + "svg", document.Root!.Name);
        Assert.Equal("img", document.Root.Attribute("role")!.Value);
        Assert.Equal(["Hamilton", "Jefferson", "Webster"], document.Descendants(ns + "g")
            .Attributes("data-method").Select(a => a.Value));
        Assert.Contains("1 → 0 (−1)", svg);
        Assert.Contains("Shared bar scale: 0–3 seats", svg);
        Assert.Contains("exact quota 4/9", svg);
        Assert.Contains("exact quota 5/9", svg);
        Assert.Equal(2, document.Descendants(ns + "text").Count(t => t.Value == "No seat losses in this transition"));
        Assert.DoesNotContain(document.Descendants(ns + "rect"), r => double.Parse(r.Attribute("width")!.Value, CultureInfo.InvariantCulture) < 0);
    }

    [Fact]
    public void SvgEscapesLabelsAndIsCultureIndependent()
    {
        var groups = new[] { new Group("A<&\"🙂🙂🙂🙂🙂🙂", 5), new Group("B", 3), new Group("C", 1) };
        var comparison = Compare(new HamiltonMethod(), groups);
        var first = AllocationComparisonSvg.Render([comparison]);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            Assert.Equal(first, AllocationComparisonSvg.Render([comparison]));
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Assert.Contains("A&lt;&amp;", first);
        Assert.Contains(groups[0].Id, XDocument.Parse(first).Descendants().First(e => e.Name.LocalName == "title" && e.Value.Contains("population")).Value);
    }

    [Fact]
    public void SvgHandlesZeroHouseAndRejectsIncompatibleComparisons()
    {
        var svg = AllocationComparisonSvg.Render([Compare(new HamiltonMethod(), HamiltonTests.Groups(0, 1), 0)]);
        XDocument.Parse(svg);
        Assert.Contains("Compare the allocations.", svg);
        Assert.DoesNotContain("Jefferson", svg);
        Assert.DoesNotContain("unexpected loss", svg);
        Assert.Throws<ArgumentException>(() => AllocationComparisonSvg.Render([]));
        Assert.Throws<ArgumentException>(() => AllocationComparisonSvg.Render([Compare(new HamiltonMethod()), Compare(new HamiltonMethod())]));
        Assert.Throws<ArgumentException>(() => AllocationComparisonSvg.Render([
            Compare(new HamiltonMethod()), Compare(new WebsterMethod(), house: 5)]));
        Assert.Throws<ArgumentException>(() => AllocationComparisonSvg.Render([
            Compare(new HamiltonMethod(), HamiltonTests.Groups(Enumerable.Repeat(1, 17).ToArray()))]));
    }

    [Fact]
    public void LargePopulationsAndSixteenGroupsRemainValidXml()
    {
        var groups = Enumerable.Range(1, 16).Select(i => new Group($"Group {i}", System.Numerics.BigInteger.Pow(10, 100) + i)).ToArray();
        var svg = AllocationComparisonSvg.Render([Compare(new HamiltonMethod(), groups)]);
        XDocument.Parse(svg);
        Assert.Contains(groups[0].Population.ToString(CultureInfo.InvariantCulture), svg);
        Assert.Contains("…", svg);
    }

    [Fact]
    public void CliWritesDeterministicSvgIntoNewDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "comparison.svg");
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var args = new[] { "visualize", "--populations", "5,3,1", "--seats", "4", "--output", path };
            Assert.Equal(0, CliApplication.Run(args, output, error));
            var first = File.ReadAllText(path);
            XDocument.Parse(first);
            Assert.Equal(0, CliApplication.Run(args, output, error));
            Assert.Equal(first, File.ReadAllText(path));
            Assert.Empty(error.ToString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }
}
