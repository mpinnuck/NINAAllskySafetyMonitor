using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Primary cloud test: the fraction of expected stars that were found. Low expected-star counts use
/// the same fraction first, then a binomial probability test when the fraction is below its limit.
/// </summary>
[Requirement("REQ-29")]
public sealed class StarFractionTest
{
    private const int MinimumFloor = 3;
    private const double DetectionProbability = 0.95;
    private const double CloudProbability = 0.01;
    private readonly Func<CloudDetectionSettings> settings;

    public StarFractionTest(Func<CloudDetectionSettings> settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public StarTestResult Evaluate(int expected, int matched)
    {
        if (expected < 0) throw new ArgumentOutOfRangeException(nameof(expected));
        if (matched < 0 || matched > expected) throw new ArgumentOutOfRangeException(nameof(matched));

        CloudDetectionSettings s = settings();
        double? fraction = expected > 0 ? (double)matched / expected : null;
        if (expected < MinimumFloor)
            return new StarTestResult(expected, matched, fraction, Inconclusive: true, Cloud: false);

        bool belowLimit = fraction!.Value < s.MinStarFraction;
        if (expected >= Math.Max(s.MinExpectedStars, MinimumFloor))
            return new StarTestResult(expected, matched, fraction, Inconclusive: false, Cloud: belowLimit);

        if (!belowLimit)
            return new StarTestResult(expected, matched, fraction, Inconclusive: false, Cloud: false);

        bool cloud = ProbabilityOfAtMostMatched(expected, matched) < CloudProbability;
        return new StarTestResult(expected, matched, fraction, Inconclusive: !cloud, Cloud: cloud);
    }

    private static double ProbabilityOfAtMostMatched(int expected, int matched)
    {
        double logTerm = expected * Math.Log(1 - DetectionProbability);
        double cumulative = 0;
        for (int i = 0; i <= matched; i++)
        {
            cumulative += Math.Exp(logTerm);
            if (i < matched)
            {
                logTerm += Math.Log(expected - i) - Math.Log(i + 1)
                    + Math.Log(DetectionProbability) - Math.Log(1 - DetectionProbability);
            }
        }

        return Math.Min(cumulative, 1);
    }
}
