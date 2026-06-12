using System.Windows;

namespace WinDS9.App;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var window = new MainWindow(e.Args);
        window.Show();
    }
}
