using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Acquisition;
using NINAAllskySafetyMonitor.Core.Analysis;
using NINAAllskySafetyMonitor.Core.Decision;
using NINAAllskySafetyMonitor.Core.Geometry;
using NINAAllskySafetyMonitor.Core.Logging;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Tuning;
using NINAAllskySafetyMonitor.Core.Weather;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>Composes a <see cref="MonitorService"/> from settings and host ports.</summary>
public sealed class MonitorServiceBuilder
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });

    /// <summary>Builds a stopped service with the real image sources and weather API. <paramref name="settings"/> is read every cycle.</summary>
    public MonitorService Build(Func<MonitorSettings> settings, MonitorPorts ports, ManualOverride? manualOverride = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ports);

        ILogSink log = RedactedLog(settings, ports);
        ImageSourceSettings ImageSettings() => settings().ImageSource;
        var sources = new ImageSourceFactory(
            new WebImageSource(Http, ImageSettings),
            new SftpImageSource(new SftpConnector(ports.Credentials, log), ImageSettings));

        return Build(settings, ports, sources, new PwsWeatherClient(Http, () => settings().Weather), manualOverride);
    }

    /// <summary>Builds with given image sources and weather provider (used by tests and the offline harness).</summary>
    public MonitorService Build(
        Func<MonitorSettings> settings,
        MonitorPorts ports,
        ImageSourceFactory sources,
        IWeatherProvider weatherProvider,
        ManualOverride? manualOverride = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(weatherProvider);

        SecretRedactor redactor = Redactor(settings, ports);
        ILogSink log = new RedactingLogSink(ports.Log, redactor);

        var siteResolver = new SiteLocationResolver(ports.Site, () => settings().Site);
        var window = new OperatingWindow(ports.Astro);
        var acquirer = new ImageAcquirer(sources, new JpegIntegrityChecker(), ports.Clock, log, () => settings().ImageSource);
        var weather = new WeatherPoller(weatherProvider, ports.Clock, log, () => settings().Weather);
        var roiCalculator = new RoiCalculator(ports.Astro, new MountStateInterpreter());
        var cycle = new MonitoringCycle(
            settings, ports.Clock, siteResolver, window, acquirer, new ImageFreshnessEvaluator(), weather, new WeatherEvaluator(),
            ports.Mount, roiCalculator, new RoiBuilder(),
            ports.FrameDecoder, new CloudDetector(() => settings().Cloud, StarCatalogue.LoadEmbedded()), ports.Astro,
            ReferenceSaver(ports, log));

        var guard = new FailSafeGuard(ports.Clock, log, () => CycleTimeout(settings()));
        var engine = new SafetyDecisionEngine(ports.Clock, () => TimeSpan.FromMinutes(settings().Decision.SafeAfterMin), new SlewHoldPolicy(), () => settings().Decision.UndeterminedIsSafe);
        MountWatcher? watcher = ports.Mount is null
            ? null
            : new MountWatcher(ports.Mount, () => TimeSpan.FromSeconds(settings().Mount.PollIntervalS), log);
        var service = new MonitorService(cycle, guard, () => TimeSpan.FromSeconds(settings().ImageSource.IntervalS), ports.Clock, log, engine, manualOverride, watcher);

        var decisions = new DecisionLogger(() => settings().Logging, ports.Clock, redactor, log);
        var tuning = new TuningRecorder(() => settings().Tuning, ports.Clock, log, redactor);
        service.SnapshotAvailable += (_, s) => decisions.Write(s);
        service.SnapshotAvailable += (_, s) => tuning.Record(s);
        return service;
    }

    /// <summary>The secrets currently in use: the weather API key and the SFTP password, if stored.</summary>
    /// <summary>Logs a captured clear-sky reference and hands it to the host to save.</summary>
    private static Action<ClearSkyReference> ReferenceSaver(MonitorPorts ports, ILogSink log) => reference =>
    {
        log.Write(LogLevel.Info, $"Clear-sky reference captured ({reference.CapturedUtc:yyyy-MM-dd HH:mm} UTC)");
        try
        {
            ports.ReferenceCaptured?.Invoke(reference);
        }
        catch (Exception e)
        {
            log.Write(LogLevel.Warning, $"Clear-sky reference could not be saved: {e.Message}");
        }
    };

    private static SecretRedactor Redactor(Func<MonitorSettings> settings, MonitorPorts ports) =>
        new(() => new[] { settings().Weather.ApiKey, ports.Credentials.GetSecret(CredentialKeys.SftpPassword) });

    private static ILogSink RedactedLog(Func<MonitorSettings> settings, MonitorPorts ports) =>
        new RedactingLogSink(ports.Log, Redactor(settings, ports));

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
