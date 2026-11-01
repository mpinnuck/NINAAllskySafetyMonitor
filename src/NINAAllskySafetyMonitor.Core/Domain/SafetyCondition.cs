namespace NINAAllskySafetyMonitor.Core.Domain;

/// <summary>The individual conditions that must all pass for a safe status (REQ-37).</summary>
public enum SafetyCondition
{
    ActiveWindow,
    FreshImage,
    NoCloud,
    NoRain,
    WindBelowLimit,
    WeatherCurrent,
}
