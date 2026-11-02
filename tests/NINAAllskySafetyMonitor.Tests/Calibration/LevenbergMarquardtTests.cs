using NINAAllskySafetyMonitor.Core.Calibration;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Calibration;

public sealed class LevenbergMarquardtTests
{
    private static readonly double[] Xs = { 0, 1, 2, 3, 4, 5, 6 };

    private static double[] Residuals(double[] p, double[] ys) =>
        Xs.Select((x, i) => (p[0] + (p[1] * x) + (p[2] * Math.Sin(p[3] * x))) - ys[i]).ToArray();

    [Fact]
    public void RecoversNonLinearParameters()
    {
        double[] truth = { 2.0, -0.5, 1.5, 0.8 };
        double[] ys = Xs.Select(x => truth[0] + (truth[1] * x) + (truth[2] * Math.Sin(truth[3] * x))).ToArray();
        double[] p = LevenbergMarquardt.Minimise(q => Residuals(q, ys), new[] { 0.0, 0.0, 1.0, 0.7 }, new[] { true, true, true, true });
        for (int i = 0; i < 4; i++) Assert.Equal(truth[i], p[i], 4);
    }

    [Fact]
    public void FixedParametersAreNotChanged()
    {
        double[] ys = Xs.Select(x => 1 + (2 * x)).ToArray();
        double[] p = LevenbergMarquardt.Minimise(q => Residuals(q, ys), new[] { 0.0, 0.0, 0.0, 1.0 }, new[] { true, true, false, false });
        Assert.Equal(1, p[0], 6);
        Assert.Equal(2, p[1], 6);
        Assert.Equal(0, p[2]);
        Assert.Equal(1, p[3]);
    }

    [Fact]
    public void SolveHandlesPivoting()
    {
        double[] x = LevenbergMarquardt.Solve(new double[,] { { 0, 2 }, { 3, 1 } }, new[] { 4.0, 5.0 });
        Assert.Equal(1, x[0], 9);
        Assert.Equal(2, x[1], 9);
    }
}
