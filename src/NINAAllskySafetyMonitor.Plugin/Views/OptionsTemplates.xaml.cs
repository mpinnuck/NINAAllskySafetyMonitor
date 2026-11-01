using System.ComponentModel.Composition;
using System.Windows;

namespace NINAAllskySafetyMonitor.Plugin.Views;

/// <summary>Exports the plugin options page template to NINA.</summary>
[Export(typeof(ResourceDictionary))]
public partial class OptionsTemplates : ResourceDictionary
{
    public OptionsTemplates()
    {
        InitializeComponent();
    }
}
