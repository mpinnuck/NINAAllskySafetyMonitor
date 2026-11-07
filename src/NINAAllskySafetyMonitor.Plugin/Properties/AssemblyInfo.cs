using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// Plugin manifest metadata read by NINA (see the official NINA plugin template).

// [MANDATORY] Unique plugin identifier. Never change once released.
[assembly: Guid("742b962c-1719-47e3-83af-437673080fb1")]

// [MANDATORY] Increment for each release build. Keep in step with VersionPrefix in Directory.Build.props.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]

// [MANDATORY] Plugin name and short description.
[assembly: AssemblyTitle("Allsky Safety Monitor")]
[assembly: AssemblyDescription("Safety monitor that detects cloud in the telescope's region of sky from Allsky camera images, combined with rain and wind from a weather station.")]

[assembly: AssemblyCompany("Mark Pinnuck")]
[assembly: AssemblyProduct("NINAAllskySafetyMonitor")]
[assembly: AssemblyCopyright("Copyright © 2026 Mark Pinnuck")]

// Minimum supported NINA line; compile-time API baseline is NINA.Plugin 3.2.0.9001.
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.0")]

// TODO: confirm licence and repository URL before first release.
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "")]
[assembly: AssemblyMetadata("Homepage", "")]
[assembly: AssemblyMetadata("Tags", "Safety Monitor,Allsky,Cloud,Weather")]
[assembly: AssemblyMetadata("ChangelogURL", "")]
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("LongDescription", @"Analyses the latest Allsky camera image for cloud in a circle around the mount's current position, checks rain and wind gusts from a Weather.com personal weather station, and reports safe or unsafe to NINA as a safety monitor device. Unsafe is reported immediately; safe only after a configurable clear period.")]

[assembly: ComVisible(false)]

// Needed because GenerateAssemblyInfo is off: tells the analyser this assembly is Windows-only.
[assembly: SupportedOSPlatform("windows7.0")]
