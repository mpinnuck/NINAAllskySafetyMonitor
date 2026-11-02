using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Maps alt/az to Allsky image pixels and back using a <see cref="LensCalibration"/>: zenith distance z
/// (radians) → radius r = c1·z + c2·z² + … pixels from the zenith pixel; azimuth → angle from image "up",
/// turned by the north rotation, clockwise or anticlockwise.
/// </summary>
[Requirement("REQ-19", "REQ-24")]
public sealed class LensProjection
{
    private const double Rad = Math.PI / 180.0;
    private readonly LensCalibration lens;

    public LensProjection(LensCalibration lens)
    {
        this.lens = lens ?? throw new ArgumentNullException(nameof(lens));
        if (lens.RadialCoefficients.Length == 0 || lens.RadialCoefficients[0] <= 0)
        {
            throw new ArgumentException("The lens model needs a positive first radial coefficient.", nameof(lens));
        }
    }

    public LensCalibration Calibration => lens;

    /// <summary>
    /// Rough model until the lens is calibrated: zenith at the image centre, horizon on the inscribed circle,
    /// north up, east to the left (the view looking up, as Allsky shows it).
    /// </summary>
    public static LensCalibration Approximate(int width, int height) => new()
    {
        CentreX = width / 2.0,
        CentreY = height / 2.0,
        NorthRotationDeg = 0,
        EastIsClockwise = false,
        RadialCoefficients = new[] { Math.Min(width, height) / 2.0 / (Math.PI / 2) },
        Manual = true,
    };

    /// <summary>Lens model from a horizon radius in pixels (equidistant fisheye), as entered by hand.</summary>
    public static LensCalibration FromHorizonRadius(double centreX, double centreY, double horizonRadiusPx, double northRotationDeg, bool eastIsClockwise) => new()
    {
        CentreX = centreX,
        CentreY = centreY,
        NorthRotationDeg = northRotationDeg,
        EastIsClockwise = eastIsClockwise,
        RadialCoefficients = new[] { horizonRadiusPx / (Math.PI / 2) },
        Manual = true,
    };

    /// <summary>Radius of the horizon (altitude 0) in pixels.</summary>
    public double HorizonRadiusPx => Radius(Math.PI / 2);

    public PixelPoint ToPixel(HorizontalCoordinate h)
    {
        double r = Radius((90.0 - h.AltDeg) * Rad);
        double theta = (lens.NorthRotationDeg + (lens.EastIsClockwise ? h.AzDeg : -h.AzDeg)) * Rad;
        return new PixelPoint(lens.CentreX + (r * Math.Sin(theta)), lens.CentreY - (r * Math.Cos(theta)));
    }

    public HorizontalCoordinate ToHorizontal(PixelPoint p)
    {
        double dx = p.X - lens.CentreX, dy = p.Y - lens.CentreY;
        double r = Math.Sqrt((dx * dx) + (dy * dy));
        double theta = Math.Atan2(dx, -dy) / Rad;
        double az = lens.EastIsClockwise ? theta - lens.NorthRotationDeg : lens.NorthRotationDeg - theta;
        return new HorizontalCoordinate(90.0 - (ZenithDistance(r) / Rad), SkyGeometry.Normalise(az));
    }

    private double Radius(double z)
    {
        double r = 0, zn = 1;
        foreach (double c in lens.RadialCoefficients)
        {
            zn *= z;
            r += c * zn;
        }

        return r;
    }

    /// <summary>Inverts the radial polynomial by Newton's method.</summary>
    private double ZenithDistance(double r)
    {
        double z = r / lens.RadialCoefficients[0];
        for (int i = 0; i < 20; i++)
        {
            double f = Radius(z) - r, df = 0, zn = 1;
            for (int k = 0; k < lens.RadialCoefficients.Length; k++)
            {
                df += (k + 1) * lens.RadialCoefficients[k] * zn;
                zn *= z;
            }

            if (Math.Abs(df) < 1e-12) break;
            double step = f / df;
            z -= step;
            if (Math.Abs(step) < 1e-10) break;
        }

        return z;
    }
}
