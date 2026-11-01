using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Core.Settings;

/// <summary>Web image source: the Allsky web page folder and the image file in it.</summary>
[Requirement("REQ-11")]
public sealed class WebSourceSettings
{
    public string BaseUrl { get; set; } = "https://oci.microconcepts.com.au/allsky/";
    public string FileName { get; set; } = "allsky.jpg";
}
