using NINAAllskySafetyMonitor.Core.Domain;

namespace NINAAllskySafetyMonitor.Core.Calibration;

/// <summary>Grid index over detected stars for fast "nearest spot within r" queries.</summary>
internal sealed class SpotIndex
{
    private readonly IReadOnlyList<DetectedStar> spots;
    private readonly double cell;
    private readonly Dictionary<long, List<int>> grid = new();

    public SpotIndex(IReadOnlyList<DetectedStar> spots, double cell)
    {
        this.spots = spots;
        this.cell = Math.Max(1.0, cell);
        for (int i = 0; i < spots.Count; i++)
        {
            long key = Key((int)Math.Floor(spots[i].Position.X / this.cell), (int)Math.Floor(spots[i].Position.Y / this.cell));
            if (!grid.TryGetValue(key, out List<int>? list)) grid[key] = list = new List<int>();
            list.Add(i);
        }
    }

    /// <summary>Index of the nearest spot within <paramref name="radius"/> (≤ cell size), or −1.</summary>
    public int Nearest(double x, double y, double radius)
    {
        int cx = (int)Math.Floor(x / cell), cy = (int)Math.Floor(y / cell);
        int best = -1;
        double bestD = radius * radius;
        int reach = (int)Math.Ceiling(radius / cell);
        for (int gy = cy - reach; gy <= cy + reach; gy++)
        {
            for (int gx = cx - reach; gx <= cx + reach; gx++)
            {
                if (!grid.TryGetValue(Key(gx, gy), out List<int>? list)) continue;
                foreach (int i in list)
                {
                    double dx = spots[i].Position.X - x, dy = spots[i].Position.Y - y;
                    double d2 = (dx * dx) + (dy * dy);
                    if (d2 <= bestD)
                    {
                        bestD = d2;
                        best = i;
                    }
                }
            }
        }

        return best;
    }

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
}
