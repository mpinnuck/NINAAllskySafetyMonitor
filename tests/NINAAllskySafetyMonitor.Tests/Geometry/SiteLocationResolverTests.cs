using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Tests.Fakes;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Geometry;

[Requirement("REQ-04")]
public sealed class SiteLocationResolverTests
{
    private readonly FakeSiteLocationSource profile = new();
    private readonly SiteSettings fallback = new();

    private SiteLocation Resolve() => new SiteLocationResolver(profile, () => fallback).Resolve();

    [Fact]
    public void UsesProfileWhenAvailable()
    {
        profile.Site = new SiteLocation(-33.71361, 151.09027, 0);
        Assert.Equal(profile.Site, Resolve());
    }

    [Fact]
    public void FallsBackWhenProfileUnavailable()
    {
        profile.Site = null;
        Assert.Equal(new SiteLocation(-33.713562, 151.09024, 134.0), Resolve());
    }

    [Fact]
    public void FallsBackWhenProfileIsZeroZero()
    {
        profile.Site = new SiteLocation(0, 0, 0);
        Assert.Equal(-33.713562, Resolve().LatitudeDeg);
    }

    [Fact]
    public void FallsBackWhenProfileIsInvalid()
    {
        profile.Site = new SiteLocation(double.NaN, 151, 0);
        Assert.Equal(-33.713562, Resolve().LatitudeDeg);
    }
}
