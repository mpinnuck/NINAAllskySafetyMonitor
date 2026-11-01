using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>Composes a <see cref="MonitorService"/> from settings and host ports.</summary>
public sealed class MonitorServiceBuilder
{
    /// <summary>Builds a stopped service. <paramref name="settings"/> is read every cycle.</summary>
    public MonitorService Build(Func<MonitorSettings> settings, MonitorPorts ports)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ports);

        var siteResolver = new SiteLocationResolver(ports.Site, () => settings().Site);
        var window = new OperatingWindow(ports.Astro);
        var cycle = new MonitoringCycle(settings, ports.Clock, siteResolver, window);

        var guard = new FailSafeGuard(ports.Clock, ports.Log, () => CycleTimeout(settings()));
        return new MonitorService(cycle, guard, () => TimeSpan.FromSeconds(settings().ImageSource.IntervalS), ports.Clock, ports.Log);
    }

    /// <summary>A cycle may take the image timeout plus incomplete-image retries plus the weather timeout, with margin.</summary>
    private static TimeSpan CycleTimeout(MonitorSettings s)
    {
        ImageSourceSettings img = s.ImageSource;
        int seconds = (img.TimeoutS * img.IncompleteRetries) + (img.IncompleteRetryS * img.IncompleteRetries) + s.Weather.TimeoutS + 15;
        return TimeSpan.FromSeconds(seconds);
    }
}
