using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Weather;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>Composes a <see cref="MonitorService"/> from settings and host ports.</summary>
public sealed class MonitorServiceBuilder
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });

    /// <summary>Builds a stopped service with the real image sources and weather API. <paramref name="settings"/> is read every cycle.</summary>
    public MonitorService Build(Func<MonitorSettings> settings, MonitorPorts ports)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ports);

        ImageSourceSettings ImageSettings() => settings().ImageSource;
        var sources = new ImageSourceFactory(
            new WebImageSource(Http, ImageSettings),
            new SftpImageSource(new SftpConnector(ports.Credentials, ports.Log), ImageSettings));

        return Build(settings, ports, sources, new PwsWeatherClient(Http, () => settings().Weather));
    }

    /// <summary>Builds with given image sources and weather provider (used by tests and the offline harness).</summary>
    public MonitorService Build(Func<MonitorSettings> settings, MonitorPorts ports, ImageSourceFactory sources, IWeatherProvider weatherProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(weatherProvider);

        var siteResolver = new SiteLocationResolver(ports.Site, () => settings().Site);
        var window = new OperatingWindow(ports.Astro);
        var acquirer = new ImageAcquirer(sources, new JpegIntegrityChecker(), ports.Clock, ports.Log, () => settings().ImageSource);
        var weather = new WeatherPoller(weatherProvider, ports.Clock, ports.Log, () => settings().Weather);
        var cycle = new MonitoringCycle(settings, ports.Clock, siteResolver, window, acquirer, new ImageFreshnessEvaluator(), weather, new WeatherEvaluator());

        var guard = new FailSafeGuard(ports.Clock, ports.Log, () => CycleTimeout(settings()));
        return new MonitorService(cycle, guard, () => TimeSpan.FromSeconds(settings().ImageSource.IntervalS), ports.Clock, ports.Log);
    }

    /// <summary>
    /// Image and weather are fetched in parallel, so a cycle may take the longer of: the image timeout
    /// plus incomplete-image retries, or the weather timeout; plus margin.
    /// </summary>
    private static TimeSpan CycleTimeout(MonitorSettings s)
    {
        ImageSourceSettings img = s.ImageSource;
        int image = (img.TimeoutS * img.IncompleteRetries) + (img.IncompleteRetryS * img.IncompleteRetries);
        return TimeSpan.FromSeconds(Math.Max(image, s.Weather.TimeoutS) + 15);
    }
}
