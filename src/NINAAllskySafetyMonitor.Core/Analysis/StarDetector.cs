using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Finds stars in a frame. A 3×3 median removes single hot pixels; the background (median of 32 px
/// blocks, interpolated) is subtracted; pixels more than <see cref="SigmaThreshold"/> × the noise above it
/// are grouped into spots, and spots of 2–80 pixels that are roughly round are kept. Each spot's
/// brightness-weighted centre, flux and peak are measured. Results are brightest peak first. Spots that
/// are not stars (gaps in trees, lights) are left for star matching to ignore.
/// </summary>
[Requirement("REQ-20", "REQ-29")]
public sealed class StarDetector
{
    public const double SigmaThreshold = 5.0;
    private const int Block = 32;
    private const int MinArea = 2;
    private const int MaxArea = 80;

    /// <summary>
    /// Detects stars, optionally only inside <paramref name="mask"/>. With <paramref name="bounds"/>, only
    /// that part of the frame is searched, which is much quicker for a small ROI.
    /// </summary>
    public IReadOnlyList<DetectedStar> Detect(GrayFrame frame, PixelMask? mask = null, PixelRect? bounds = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (bounds is not PixelRect r || (r.X == 0 && r.Y == 0 && r.Width == frame.Width && r.Height == frame.Height))
        {
            return DetectIn(frame, mask);
        }

        if (r.Width < 3 || r.Height < 3) return Array.Empty<DetectedStar>();
        var crop = new float[r.Width * r.Height];
        for (int y = 0; y < r.Height; y++) Array.Copy(frame.Pixels, ((r.Y + y) * frame.Width) + r.X, crop, y * r.Width, r.Width);
        var stars = new List<DetectedStar>();
        foreach (DetectedStar s in DetectIn(new GrayFrame(r.Width, r.Height, crop), null))
        {
            var p = new PixelPoint(s.Position.X + r.X, s.Position.Y + r.Y);
            if (mask is not null && !mask.Contains((int)Math.Round(p.X), (int)Math.Round(p.Y))) continue;
            stars.Add(s with { Position = p });
        }

        return stars;
    }

    private static IReadOnlyList<DetectedStar> DetectIn(GrayFrame frame, PixelMask? mask)
    {
        int w = frame.Width, h = frame.Height;
        float[] med = Median3(frame);
        float[] bg = Background(med, w, h);
        var d = new float[w * h];
        for (int i = 0; i < d.Length; i++) d[i] = med[i] - bg[i];

        double threshold = Math.Max(SigmaThreshold * Noise(d), 3.0);
        var label = new int[w * h];
        var stars = new List<DetectedStar>();
        var queue = new Queue<int>();
        int next = 0;

        for (int start = 0; start < d.Length; start++)
        {
            if (label[start] != 0 || d[start] <= threshold) continue;
            next++;
            label[start] = next;
            queue.Enqueue(start);
            int area = 0, minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
            double flux = 0, sx = 0, sy = 0, peak = 0;
            while (queue.Count > 0)
            {
                int p = queue.Dequeue();
                int x = p % w, y = p / w;
                double v = d[p];
                area++;
                flux += v;
                sx += v * x;
                sy += v * y;
                peak = Math.Max(peak, v);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= w) continue;
                        int q = (ny * w) + nx;
                        if (label[q] == 0 && d[q] > threshold)
                        {
                            label[q] = next;
                            queue.Enqueue(q);
                        }
                    }
                }
            }

            int bw = maxX - minX + 1, bh = maxY - minY + 1;
            if (area < MinArea || area > MaxArea) continue;
            if (Math.Max(bw, bh) > (3 * Math.Min(bw, bh)) + 1) continue;
            var centre = new PixelPoint(sx / flux, sy / flux);
            if (mask is not null && !mask.Contains((int)Math.Round(centre.X), (int)Math.Round(centre.Y))) continue;
            stars.Add(new DetectedStar(centre, flux, peak, area));
        }

        stars.Sort((a, b) => b.Peak.CompareTo(a.Peak));
        return stars;
    }

    /// <summary>3×3 median; edge pixels are copied.</summary>
    internal static float[] Median3(GrayFrame f)
    {
        int w = f.Width, h = f.Height;
        var src = f.Pixels;
        var dst = (float[])src.Clone();
        Span<float> v = stackalloc float[9];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int row = (y + dy) * w;
                    for (int dx = -1; dx <= 1; dx++) v[k++] = src[row + x + dx];
                }

                v.Sort();
                dst[(y * w) + x] = v[4];
            }
        }

        return dst;
    }

    /// <summary>Median of each 32×32 block, bilinearly interpolated between block centres.</summary>
    internal static float[] Background(float[] img, int w, int h)
    {
        int gw = Math.Max(1, (w + Block - 1) / Block), gh = Math.Max(1, (h + Block - 1) / Block);
        var grid = new float[gw * gh];
        var buf = new List<float>(Block * Block);
        for (int by = 0; by < gh; by++)
        {
            for (int bx = 0; bx < gw; bx++)
            {
                buf.Clear();
                for (int y = by * Block; y < Math.Min(h, (by + 1) * Block); y++)
                {
                    for (int x = bx * Block; x < Math.Min(w, (bx + 1) * Block); x++) buf.Add(img[(y * w) + x]);
                }

                buf.Sort();
                grid[(by * gw) + bx] = buf[buf.Count / 2];
            }
        }

        var bg = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            double gy = Math.Clamp(((y + 0.5) / Block) - 0.5, 0, gh - 1);
            int y0 = (int)gy, y1 = Math.Min(y0 + 1, gh - 1);
            double fy = gy - y0;
            for (int x = 0; x < w; x++)
            {
                double gx = Math.Clamp(((x + 0.5) / Block) - 0.5, 0, gw - 1);
                int x0 = (int)gx, x1 = Math.Min(x0 + 1, gw - 1);
                double fx = gx - x0;
                double top = (grid[(y0 * gw) + x0] * (1 - fx)) + (grid[(y0 * gw) + x1] * fx);
                double bottom = (grid[(y1 * gw) + x0] * (1 - fx)) + (grid[(y1 * gw) + x1] * fx);
                bg[(y * w) + x] = (float)((top * (1 - fy)) + (bottom * fy));
            }
        }

        return bg;
    }

    /// <summary>Robust noise: 1.4826 × median absolute deviation of a sample of pixels.</summary>
    internal static double Noise(float[] d)
    {
        var sample = new List<float>(d.Length / 7 + 1);
        for (int i = 0; i < d.Length; i += 7) sample.Add(d[i]);
        sample.Sort();
        float median = sample[sample.Count / 2];
        for (int i = 0; i < sample.Count; i++) sample[i] = Math.Abs(sample[i] - median);
        sample.Sort();
        return Math.Max(1.4826 * sample[sample.Count / 2], 0.5);
    }
}
