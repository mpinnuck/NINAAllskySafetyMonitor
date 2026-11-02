using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Analysis;

[Requirement("REQ-20", "REQ-29")]
public sealed class StarCatalogueTests
{
    private readonly StarCatalogue catalogue = StarCatalogue.LoadEmbedded();

    [Fact]
    public void EmbeddedCatalogueHoldsAboutNineHundredStarsToMagnitudeFourAndAHalf()
    {
        Assert.InRange(catalogue.Stars.Count, 880, 930);
        Assert.All(catalogue.Stars, s => Assert.True(s.Magnitude <= 4.5));
    }

    [Fact]
    public void BrightStarsHaveNamesAndJ2000Positions()
    {
        CatalogueStar sirius = catalogue.Stars.Single(s => s.Name == "Sirius");
        Assert.Equal(2491, sirius.Id);
        Assert.Equal(6.7525, sirius.RaHours, 3);
        Assert.Equal(-16.716, sirius.DecDeg, 2);
        Assert.Equal(-1.46, sirius.Magnitude, 2);
        Assert.Equal("Sirius", catalogue.Named().First().Name);
        Assert.Contains(catalogue.Named(), s => s.Name == "Canopus");
        Assert.Contains(catalogue.Named(), s => s.Name == "Achernar");
    }

    [Fact]
    public void ParseSkipsCommentsAndHeader()
    {
        StarCatalogue c = StarCatalogue.Parse("# note\nHR,RaHours,DecDeg,Vmag,Name\n1,1.5,-20.25,3.10,\n2,2.0,10,1.00,Test\n");
        Assert.Equal(2, c.Stars.Count);
        Assert.Null(c.Stars[0].Name);
        Assert.Equal("Test", c.Stars[1].Name);
        Assert.Single(c.BrighterThan(2.0));
    }
}
