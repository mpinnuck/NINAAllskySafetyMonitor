using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Geometry;

/// <summary>
/// Allsky lens model mapping alt/az to image pixels. The zenith distance z (radians) maps to radius
/// r = c1·z + c2·z² + c3·z³ … pixels from the centre, using <see cref="RadialCoefficients"/>.
/// </summary>
[Requirement("REQ-19")]
public sealed record LensCalibration
{
    /// <summary>Image column of the zenith, in pixels.</summary>
    public double CentreX { get; init; }

    /// <summary>Image row of the zenith, in pixels.</summary>
    public double CentreY { get; init; }

    /// <summary>Image angle of north, in degrees, measured from image "up" towards image "right".</summary>
    public double NorthRotationDeg { get; init; }

    /// <summary>True when azimuth increases clockwise in the image (east to the right of north).</summary>
    public bool EastIsClockwise { get; init; }

    /// <summary>Radial polynomial coefficients c1, c2, … in pixels per radian^n.</summary>
    public double[] RadialCoefficients { get; init; } = Array.Empty<double>();

    /// <summary>Number of catalogue stars used in the fit.</summary>
    public int StarsMatched { get; init; }

    /// <summary>RMS fit error of the calibration, in pixels.</summary>
    public double RmsErrorPx { get; init; }

    /// <summary>RMS fit error of the calibration on the sky, in degrees.</summary>
    public double RmsErrorDeg { get; init; }

    /// <summary>When the calibration was made (for a fit: when the frame was taken).</summary>
    public DateTime CalibratedUtc { get; init; }

    /// <summary>True for a rough model entered by hand or estimated from the image size, not fitted to stars.</summary>
    public bool Manual { get; init; }
}
