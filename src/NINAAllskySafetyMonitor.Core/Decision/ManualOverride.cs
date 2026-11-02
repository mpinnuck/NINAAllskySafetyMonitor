using NINAAllskySafetyMonitor.Core.Abstractions;
using NINAAllskySafetyMonitor.Core.Domain;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Decision;

/// <summary>
/// Force Safe / Force Unsafe / Auto, for testing. Held in memory only, so it is Auto again after
/// NINA restarts. Every change is logged, and the status reason says when an override is in effect.
/// </summary>
[Requirement("REQ-40")]
public sealed class ManualOverride
{
    private readonly ILogSink log;
    private volatile OverrideMode mode = OverrideMode.Auto;

    public ManualOverride(ILogSink log)
    {
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Raised after the mode changes.</summary>
    public event EventHandler? Changed;

    public OverrideMode Mode
    {
        get => mode;
        set
        {
            if (mode == value) return;
            mode = value;
            log.Write(value == OverrideMode.Auto ? LogLevel.Info : LogLevel.Warning, $"Manual override set to {Describe(value)}");
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The status as reported, with the override applied.</summary>
    public SafetyStatus Apply(SafetyStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        OverrideMode m = mode;
        return m switch
        {
            OverrideMode.ForceSafe => status with
            {
                State = SafetyState.Safe, IsSafe = true, Override = m, SecondsToSafe = null,
                Reason = $"MANUAL OVERRIDE: Force Safe (monitor says {status.State}: {status.Reason})",
            },
            OverrideMode.ForceUnsafe => status with
            {
                State = SafetyState.Unsafe, IsSafe = false, Override = m, SecondsToSafe = null,
                Reason = $"MANUAL OVERRIDE: Force Unsafe (monitor says {status.State}: {status.Reason})",
            },
            _ => status,
        };
    }

    private static string Describe(OverrideMode m) => m switch
    {
        OverrideMode.ForceSafe => "Force Safe",
        OverrideMode.ForceUnsafe => "Force Unsafe",
        _ => "Auto",
    };
}
