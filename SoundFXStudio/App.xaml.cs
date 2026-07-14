using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Views.Dialogs;
using System.Windows;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SoundFXStudio;

public partial class App : Application
{
    private readonly FileLogService _logService = new();
    private readonly ConfigService _configService;

    public App()
    {
        _configService = new ConfigService(_logService);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        Exit += App_Exit;
        _logService.Info("Application Starting");
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        _logService.Info($"App Version: {Assembly.GetExecutingAssembly().GetName().Version}");
        _logService.Info($"Operating System: {RuntimeInformation.OSDescription}");

        var config = _configService.Load();
        _logService.Enabled = config.Settings.EnableLogging;

        if (config.Settings.ShowSetupWizardOnStartup)
        {
            var wizard = new SetupWizardWindow();
            wizard.ShowDialog();
        }

        var mainWindow = new MainWindow(_logService);
        MainWindow = mainWindow;
        mainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        _logService.Info("Application Started");
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        try
        {
            _logService.Info("Application Shutting Down");

            if (MainWindow?.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            var config = _configService.Load();
            
            // Clean up virtual cable routing on app exit
            // Reset VirtualCableDeviceId so the cable is no longer in use
            if (!string.IsNullOrEmpty(config.Settings.VirtualCableDeviceId))
            {
                config.Settings.VirtualCableDeviceId = string.Empty;
                _configService.Save(config);
            }

            _logService.Info("Application Shutdown Complete");
        }
        catch
        {
            // Best-effort cleanup
        }
        finally
        {
            _logService.Dispose();
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logService.Critical("DispatcherUnhandledException", e.Exception);
    }

    private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logService.Critical("AppDomain.CurrentDomain.UnhandledException", exception);
        }
        else
        {
            _logService.Critical($"AppDomain.CurrentDomain.UnhandledException: {e.ExceptionObject}");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logService.Critical("TaskScheduler.UnobservedTaskException", e.Exception);
    }

}
