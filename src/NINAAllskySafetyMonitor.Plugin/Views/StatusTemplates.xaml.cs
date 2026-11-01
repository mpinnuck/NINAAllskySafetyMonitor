using System.ComponentModel.Composition;
using System.Windows;

namespace NINAAllskySafetyMonitor.Plugin.Views;

/// <summary>Exports the status dock panel template to NINA.</summary>
[Export(typeof(ResourceDictionary))]
public partial class StatusTemplates : ResourceDictionary
{
    public StatusTemplates()
    {
        InitializeComponent();
    }
}
