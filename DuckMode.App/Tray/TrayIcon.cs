using System;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace DuckMode.App.Tray;

public class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;
    public TrayIcon(MainWindow window)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "DuckMode",
            Visibility = Visibility.Visible
        };
        var ctx = new System.Windows.Controls.ContextMenu();
        ctx.Items.Add(new System.Windows.Controls.MenuItem { Header = "Open Chat", Command = new Relay(() => window.Show()) });
        ctx.Items.Add(new System.Windows.Controls.MenuItem { Header = "Exit", Command = new Relay(() => Application.Current.Shutdown()) });
        _icon.ContextMenu = ctx;
    }

    public void Dispose() => _icon.Dispose();

    private sealed class Relay : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public Relay(Action action) { _action = action; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
        public event EventHandler? CanExecuteChanged;
    }
}





