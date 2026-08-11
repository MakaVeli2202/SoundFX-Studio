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
using System.Windows.Threading;
using System.Reflection;
using System.Threading.Tasks;

namespace SoundFXStudio;

public partial class App : Application
{
    private readonly FileLogService _logService = new();
    private readonly ConfigService _configService;
    private TaskbarIcon? _trayIcon;
    private static readonly Mutex _singleInstanceMutex;
    private static readonly bool _isFirstInstance;
    private static EventWaitHandle? _showRequested;

    static App()
    {
        _singleInstanceMutex = new Mutex(true, @"SoundFXStudio_SingleInstance", out _isFirstInstance);
    }

    public static bool IsShuttingDown { get; private set; }

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
        var vm = new VoicemeeterRemote();
        if (!vm.TryConnect())
        {
            vm.Dispose();
            return null;
        }
        return vm;
    }

    public static bool EnsureVirtualInputB1()
    {
        using var vm = Vm();
        if (vm is null) return false;

        var stripCount = vm.StripCount();
        var firstVirtual = vm.FirstVirtualStrip(stripCount);
        if (stripCount <= 0 || firstVirtual < 0) return false;

        if (vm.GetFloat($"Strip[{firstVirtual}].B1") >= 0.5f) return true;

        vm.SetFloat($"Strip[{firstVirtual}].B1", 1);
        vm.IsDirty();
        return true;
    }

    public static bool ToggleMuteAll()
    {
        using var vm = Vm(); if (vm is null) return false;
        bool mute = vm.AnyStripUnmuted();
        vm.SetAllStripsMute(mute);
        return mute;
    }

    public static bool ToggleMuteHear()
    {
        using var vm = Vm(); if (vm is null) return false;
        bool m = !vm.GetBusMute(vm.A1Bus);
        vm.SetBusMute(vm.A1Bus, m);
        return m;
    }

    public static bool ToggleMuteTeam()
    {
        using var vm = Vm(); if (vm is null) return false;
        bool m = !vm.GetBusMute(vm.B1Bus);
        vm.SetBusMute(vm.B1Bus, m);
        return m;
    }

    public static bool SetInputStripB1(bool on)
    {
        using var vm = Vm(); if (vm is null) return false;
        vm.SetStripB1(0, on);
        vm.IsDirty();
        return true;
    }

    public static bool IsVoiceChangerRunning { get; set; }

    private async void App_Startup(object sender, StartupEventArgs e)
    {
        var splash = new LoadingScreenWindow();
        splash.Show();
        await Dispatcher.Yield(DispatcherPriority.Background);

        _logService.Info($"App Version: {Assembly.GetExecutingAssembly().GetName().Version}");
        _logService.Info($"Operating System: {RuntimeInformation.OSDescription}");

        if (!_isFirstInstance)
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, @"SoundFXStudio_ShowRequested");
            for (int i = 0; i < 20; i++)
            {
                evt.Set();
                if (!_singleInstanceMutex.WaitOne(0))
                {
                    Thread.Sleep(50);
                    continue;
                }
                break;
            }
            _logService.Info("Second instance detected - signaling existing instance and exiting");
            Shutdown();
            return;
        }

        _showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, @"SoundFXStudio_ShowRequested");
        _ = Task.Run(() =>
        {
            while (true)
            {
                try { _showRequested.WaitOne(); } catch { break; }
                Dispatcher.Invoke(() => ShowMainWindow());
            }
        });

        splash.SetStatus("Loading settings…");
        var config = _configService.Load();
        _logService.Enabled = config.Settings.EnableLogging;

        if (config.Settings.ShowSetupWizardOnStartup)
        {
            splash.Close();
            var wizard = new SetupWizardWindow();
            wizard.ShowDialog();
        }
        else
        {
            splash.SetStatus("Preparing dashboard…");
        }

        await Dispatcher.Yield(DispatcherPriority.Background);

        var mainWindow = new MainWindow(_logService);
        MainWindow = mainWindow;

        if (splash.IsVisible)
        {
            splash.Close();
        }

        mainWindow.Show();

        if (config.Settings.StartMinimized)
        {
            mainWindow.WindowState = WindowState.Minimized;
        }

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
        catch (Exception ex)
        {
            _logService.Error("Application shutdown failed", ex);
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
            var cyan = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF));
            var cyanPen = new Pen(cyan, 2.0);
            var darkFill = new SolidColorBrush(Color.FromRgb(0x0A, 0x1B, 0x30));

            var band = new StreamGeometry();
            using (var ctx = band.Open())
            {
                ctx.BeginFigure(new Point(5, 14), isFilled: false, isClosed: false);
                ctx.ArcTo(new Point(27, 14), new Size(11, 11), 0,
                    isLargeArc: false, SweepDirection.Counterclockwise,
                    isStroked: true, isSmoothJoin: true);
            }
            band.Freeze();
            dc.DrawGeometry(null, cyanPen, band);

            dc.DrawRoundedRectangle(darkFill, cyanPen, new Rect(3, 13, 8, 12), 3, 3);
            dc.DrawRoundedRectangle(darkFill, cyanPen, new Rect(21, 13, 8, 12), 3, 3);
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
