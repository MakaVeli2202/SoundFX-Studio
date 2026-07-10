using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Views.Dialogs;
using System.Windows;

namespace SoundFXStudio;

public partial class App : Application
{
    private readonly ConfigService _configService = new();

    public App()
    {
        Exit += App_Exit;
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        var config = _configService.Load();

        if (!config.Settings.SetupCompleted)
        {
            var wizard = new SetupWizardWindow();
            wizard.ShowDialog();
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        try
        {
            var config = _configService.Load();
            
            // Clean up virtual cable routing on app exit
            // Reset VirtualCableDeviceId so the cable is no longer in use
            if (!string.IsNullOrEmpty(config.Settings.VirtualCableDeviceId))
            {
                config.Settings.VirtualCableDeviceId = string.Empty;
                _configService.Save(config);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }

}
