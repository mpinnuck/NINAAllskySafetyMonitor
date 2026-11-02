using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Analysis;

namespace NINAAllskySafetyMonitor.Core.Monitoring;

/// <summary>The host-provided services the monitor needs (implemented by the plugin or the harness).</summary>
/// <param name="Clock">Time source.</param>
/// <param name="Site">Site location from the host.</param>
/// <param name="Astro">Astronomical calculations.</param>
/// <param name="Log">Log destination.</param>
/// <param name="Credentials">Secure storage for the SFTP password.</param>
/// <param name="Mount">Mount state from the host, or null when there is none.</param>
/// <param name="FrameDecoder">JPEG decoder for cloud detection, or null when the host has none (cloud is then unknown, so unsafe).</param>
/// <param name="ReferenceCaptured">Saves a newly captured clear-sky reference with the settings, or null to keep it for this session only.</param>
public sealed record MonitorPorts(
    IClock Clock,
    ISiteLocationSource Site,
    IAstroCalculator Astro,
    ILogSink Log,
    ICredentialStore Credentials,
    IMountStateSource? Mount = null,
    IFrameDecoder? FrameDecoder = null,
    Action<ClearSkyReference>? ReferenceCaptured = null);
