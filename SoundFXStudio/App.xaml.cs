using Hardcodet.Wpf.TaskbarNotification;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Views.Dialogs;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Threading.Tasks;

namespace SoundFXStudio;

public partial class App : Application
{
    private readonly FileLogService _logService = new();
    private readonly ConfigService _configService;
    private TaskbarIcon? _trayIcon;
    private static VoicemeeterRemote? _vmShared;

    public App()
    {
        _configService = new ConfigService(_logService);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        Exit += App_Exit;
        _logService.Info("Application Starting");
    }

    public static VoicemeeterRemote? Vm()
    {
        if (!VoicemeeterRemote.IsInstalled()) return null;
        _vmShared ??= new VoicemeeterRemote();
        if (!_vmShared.LoggedIn && !_vmShared.Login()) return null;
        return _vmShared;
    }

    public static bool ToggleMuteAll()
    {
        var vm = Vm(); if (vm is null) return false;
        bool mute = vm.AnyStripUnmuted();
        vm.SetAllStripsMute(mute);
        return mute;
    }

    public static bool ToggleMuteHear()
    {
        var vm = Vm(); if (vm is null) return false;
        bool m = !vm.GetBusMute(vm.A1Bus);
        vm.SetBusMute(vm.A1Bus, m);
        return m;
    }

    public static bool ToggleMuteTeam()
    {
        var vm = Vm(); if (vm is null) return false;
        bool m = !vm.GetBusMute(vm.B1Bus);
        vm.SetBusMute(vm.B1Bus, m);
        return m;
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        _logService.Info($"App Version: {Assembly.GetExecutingAssembly().GetName().Version}");
        _logService.Info($"Operating System: {RuntimeInformation.OSDescription}");

        var config = _configService.Load();
        _logService.Enabled = config.Settings.EnableLogging;

        if (config.Settings.ShowSetupWizardOnStartup && !config.Settings.SetupCompleted)
        {
            var wizard = new SetupWizardWindow();
            wizard.ShowDialog();
        }

        var mainWindow = new MainWindow(_logService);
        MainWindow = mainWindow;
        mainWindow.Show();

        _trayIcon = new TaskbarIcon
        {
            IconSource = CreateTrayIcon(),
            ToolTipText = "SoundFX Studio"
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
        var trayMenu = new ContextMenu();
        var openItem = new MenuItem { Header = "Open SoundFX Studio" };
        openItem.Click += (_, _) => ShowMainWindow();
        trayMenu.Items.Add(openItem);
        var stopAllItem = new MenuItem { Header = "Stop All Sounds" };
        stopAllItem.Click += (_, _) => (MainWindow?.DataContext as ViewModels.MainViewModel)?.StopAllSounds();
        trayMenu.Items.Add(stopAllItem);
        trayMenu.Items.Add(new Separator());
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Shutdown();
        trayMenu.Items.Add(exitItem);
        _trayIcon.ContextMenu = trayMenu;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _logService.Info("Application Started");
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        try
        {
            _logService.Info("Application Shutting Down");

            _trayIcon?.Dispose();
            _vmShared?.Dispose();

            if (MainWindow?.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            var config = _configService.Load();
            
            if (!string.IsNullOrEmpty(config.Settings.VirtualCableDeviceId))
            {
                config.Settings.VirtualCableDeviceId = string.Empty;
                _configService.Save(config);
            }

            _logService.Info("Application Shutdown Complete");
        }
        catch
        {
        }
        finally
        {
            _logService.Dispose();
        }
    }

    private void ShowMainWindow()
    {
        if (MainWindow is Window w)
        {
            w.Show();
            w.WindowState = WindowState.Normal;
            w.Activate();
        }
    }

    private static BitmapSource CreateTrayIcon()
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0xCC)),
                null,
                new System.Windows.Rect(0, 0, 32, 32), 7, 7);
            var ft = new FormattedText("SF",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                16,
                new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                96);
            dc.DrawText(ft, new System.Windows.Point(4, 6));
        }
        var rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logService.Critical("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
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
