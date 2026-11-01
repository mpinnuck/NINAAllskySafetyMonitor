using NINAAllskySafetyMonitor.Core.Abstractions;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>The host-provided services the monitor needs (implemented by the plugin or the harness).</summary>
/// <param name="Clock">Time source.</param>
/// <param name="Site">Site location from the host.</param>
/// <param name="Astro">Astronomical calculations.</param>
/// <param name="Log">Log destination.</param>
/// <param name="Credentials">Secure storage for the SFTP password.</param>
public sealed record MonitorPorts(IClock Clock, ISiteLocationSource Site, IAstroCalculator Astro, ILogSink Log, ICredentialStore Credentials);
