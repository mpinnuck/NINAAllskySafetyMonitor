using NINA.Astrometry;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>
/// Reads the mount state from NINA's telescope mediator each cycle. Any problem reading it gives
/// "no mount information", which places the ROI on the whole working area rather than failing (REQ-07).
/// </summary>
[Requirement("REQ-05", "REQ-06", "REQ-07")]
public sealed class NinaMountStateSource : IMountStateSource
{
    private static readonly NinaLogSink Log = new();
    private readonly ITelescopeMediator telescope;

    public NinaMountStateSource(ITelescopeMediator telescope)
    {
        this.telescope = telescope ?? throw new ArgumentNullException(nameof(telescope));
    }

    /// <inheritdoc />
    public MountState? GetCurrent()
    {
        try
        {
            TelescopeInfo info = telescope.GetInfo();
            if (info is null) return null;
            if (!info.Connected)
            {
                return new MountState(false, false, false, false, null, null, CoordinateEpoch.JNow, null, null, DateTime.UtcNow);
            }

            return new MountState(
                Connected: true,
                AtPark: info.AtPark,
                Tracking: info.TrackingEnabled,
                Slewing: info.Slewing,
                RaHours: info.RightAscension,
                DecDeg: info.Declination,
                Epoch: info.EquatorialSystem == Epoch.J2000 ? CoordinateEpoch.J2000 : CoordinateEpoch.JNow,
                ReportedAltDeg: info.Altitude,
                ReportedAzDeg: info.Azimuth,
                TimestampUtc: DateTime.UtcNow);
        }
        catch (Exception e)
        {
            Log.Write(LogLevel.Warning, $"Could not read the mount state: {e.Message}");
            return null;
        }
    }
}
