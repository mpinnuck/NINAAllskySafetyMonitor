namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>
/// Small Levenberg–Marquardt least-squares solver for a handful of parameters, with a numerical
/// Jacobian. Minimises the sum of squared residuals over the parameters marked free.
/// </summary>
public static class LevenbergMarquardt
{
    /// <summary>Returns the parameters that minimise Σ residuals², starting from <paramref name="start"/>.</summary>
    public static double[] Minimise(Func<double[], double[]> residuals, double[] start, bool[] free, int maxIterations = 100)
    {
        ArgumentNullException.ThrowIfNull(residuals);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(free);
        int[] idx = Enumerable.Range(0, start.Length).Where(i => free[i]).ToArray();
        int n = idx.Length;
        var p = (double[])start.Clone();
        double[] r = residuals(p);
        double cost = SumSq(r);
        double lambda = 1e-3;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            var jac = new double[r.Length, n];
            for (int k = 0; k < n; k++)
            {
                int j = idx[k];
                double h = 1e-6 * Math.Max(1.0, Math.Abs(p[j]));
                var q = (double[])p.Clone();
                q[j] += h;
                double[] rq = residuals(q);
                for (int i = 0; i < r.Length; i++) jac[i, k] = (rq[i] - r[i]) / h;
            }

            var a = new double[n, n];
            var g = new double[n];
            for (int i = 0; i < r.Length; i++)
            {
                for (int k = 0; k < n; k++)
                {
                    g[k] += jac[i, k] * r[i];
                    for (int l = 0; l < n; l++) a[k, l] += jac[i, k] * jac[i, l];
                }
            }

            bool improved = false;
            double[] step = new double[n];
            while (lambda < 1e12)
            {
                var m = (double[,])a.Clone();
                for (int k = 0; k < n; k++) m[k, k] += lambda * (a[k, k] + 1e-12);
                step = Solve(m, g.Select(v => -v).ToArray());
                var q = (double[])p.Clone();
                for (int k = 0; k < n; k++) q[idx[k]] += step[k];
                double[] rq = residuals(q);
                double c = SumSq(rq);
                if (c < cost)
                {
                    p = q;
                    r = rq;
                    double drop = cost - c;
                    cost = c;
                    lambda = Math.Max(lambda / 10, 1e-12);
                    improved = true;
                    if (drop < 1e-12 * (1 + cost)) return p;
                    break;
                }

                lambda *= 10;
            }

            if (!improved || step.All(s => Math.Abs(s) < 1e-9)) break;
        }

        return p;
    }

    private static double SumSq(double[] r)
    {
        double s = 0;
        foreach (double v in r) s += v * v;
        return s;
    }

    /// <summary>Solves m·x = b by Gaussian elimination with partial pivoting.</summary>
    internal static double[] Solve(double[,] m, double[] b)
    {
        int n = b.Length;
        var a = (double[,])m.Clone();
        var x = (double[])b.Clone();
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < n; row++)
            {
                if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col])) pivot = row;
            }

            if (Math.Abs(a[pivot, col]) < 1e-300) return new double[n];
            if (pivot != col)
            {
                for (int k = 0; k < n; k++) (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                (x[col], x[pivot]) = (x[pivot], x[col]);
            }

            for (int row = col + 1; row < n; row++)
            {
                double f = a[row, col] / a[col, col];
                for (int k = col; k < n; k++) a[row, k] -= f * a[col, k];
                x[row] -= f * x[col];
            }
        }

        for (int row = n - 1; row >= 0; row--)
        {
            double s = x[row];
            for (int k = row + 1; k < n; k++) s -= a[row, k] * x[k];
            x[row] = s / a[row, row];
        }

        return x;
    }
}
