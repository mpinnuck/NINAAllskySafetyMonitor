using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>Composes a <see cref="MonitorService"/> from settings and host ports.</summary>
public sealed class MonitorServiceBuilder
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });

    /// <summary>Builds a stopped service. <paramref name="settings"/> is read every cycle.</summary>
    public MonitorService Build(Func<MonitorSettings> settings, MonitorPorts ports)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ports);

        ImageSourceSettings ImageSettings() => settings().ImageSource;
        var sources = new ImageSourceFactory(
            new WebImageSource(Http, ImageSettings),
            new SftpImageSource(new SftpConnector(ports.Credentials, ports.Log), ImageSettings));

        return Build(settings, ports, sources);
    }

    /// <summary>Builds with given image sources (used by tests and the offline harness).</summary>
    public MonitorService Build(Func<MonitorSettings> settings, MonitorPorts ports, ImageSourceFactory sources)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(sources);

        var siteResolver = new SiteLocationResolver(ports.Site, () => settings().Site);
        var window = new OperatingWindow(ports.Astro);
        var acquirer = new ImageAcquirer(sources, new JpegIntegrityChecker(), ports.Clock, ports.Log, () => settings().ImageSource);
        var cycle = new MonitoringCycle(settings, ports.Clock, siteResolver, window, acquirer, new ImageFreshnessEvaluator());

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
