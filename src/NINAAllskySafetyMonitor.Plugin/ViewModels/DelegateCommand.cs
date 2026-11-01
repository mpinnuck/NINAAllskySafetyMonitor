using System.Windows.Input;

namespace NINAAllskySafetyMonitor.Plugin.ViewModels;

/// <summary>Minimal <see cref="ICommand"/> that runs an action.</summary>
public sealed class DelegateCommand : ICommand
{
    private readonly Action execute;

    public DelegateCommand(Action execute)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
