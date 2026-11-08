using System.ComponentModel.Composition;
using System.Windows;
using NINAAllskySafetyMonitor.Plugin.ViewModels;

namespace NINAAllskySafetyMonitor.Plugin.Views;

/// <summary>Exports the status dock panel template to NINA.</summary>
[Export(typeof(ResourceDictionary))]
public partial class StatusTemplates : ResourceDictionary
{
    public StatusTemplates()
    {
        InitializeComponent();
    }

    private void OnStatusPanelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: StatusViewModel vm }) vm.AttachView();
    }

    private void OnStatusPanelUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: StatusViewModel vm }) vm.DetachView();
    }
}
