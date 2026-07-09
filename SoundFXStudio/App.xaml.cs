using SoundFXStudio.Services;
using SoundFXStudio.Views.Dialogs;
using System.Windows;

namespace SoundFXStudio;

public partial class App : Application
{
    private readonly ConfigService _configService = new();

    private void App_Startup(object sender, StartupEventArgs e)
    {
        if (!_configService.HasSavedConfig())
        {
            var wizard = new SetupWizardWindow();
            wizard.ShowDialog();
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
