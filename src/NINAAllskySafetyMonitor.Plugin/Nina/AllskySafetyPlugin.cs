using System.ComponentModel.Composition;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Plugin.ViewModels;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>
/// Plugin manifest. NINA reads the metadata from Properties/AssemblyInfo.cs and uses this
/// instance as the data context of the plugin's options page (Views/OptionsTemplates.xaml).
/// </summary>
[Export(typeof(IPluginManifest))]
[Requirement("REQ-46", "REQ-48")]
public sealed class AllskySafetyPlugin : PluginBase
{
    [ImportingConstructor]
    public AllskySafetyPlugin()
    {
        Settings = new SettingsViewModel(PluginSettingsStore.Instance);
    }

    /// <summary>Settings editor bound by the options page.</summary>
    public SettingsViewModel Settings { get; }

    public override async Task Teardown()
    {
        await MonitorHost.Instance.StopAsync().ConfigureAwait(false);
        await base.Teardown().ConfigureAwait(false);
    }
}
