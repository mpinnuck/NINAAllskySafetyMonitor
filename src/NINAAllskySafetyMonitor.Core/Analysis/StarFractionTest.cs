using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Primary cloud test: the fraction of expected stars that were found. Below the minimum fraction there is
/// cloud; with fewer expected stars than the minimum the test is inconclusive.
/// </summary>
[Requirement("REQ-29")]
public sealed class StarFractionTest
{
    private readonly Func<CloudDetectionSettings> settings;

    public StarFractionTest(Func<CloudDetectionSettings> settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public StarTestResult Evaluate(int expected, int matched)
    {
        CloudDetectionSettings s = settings();
        double? fraction = expected > 0 ? (double)matched / expected : null;
        bool inconclusive = expected < s.MinExpectedStars;
        return new StarTestResult(expected, matched, fraction, inconclusive, !inconclusive && fraction < s.MinStarFraction);
    }
}
