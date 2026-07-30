using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace SoundFXStudio.Views.Dialogs;

public sealed class ToastWindow : Window
{
    private readonly DispatcherTimer _fadeTimer;
    private DateTime _startTime;

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

    public ToastWindow(string message)
    {
        Width = 280;
        Height = 56;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;

        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 24;
        Top = screen.Bottom - Height - 24;

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x08, 0x14, 0x28)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x80, 0xCF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x14, 0x10, 0xB9, 0x81)),
                CornerRadius = new CornerRadius(10),
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 0),
                }
            }
        };

        _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _startTime = DateTime.UtcNow;
        _fadeTimer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
            if (elapsed > 3.5)
            {
                _fadeTimer.Stop();
                Close();
            }
            else if (elapsed > 2.5)
            {
                Opacity = 1.0 - (elapsed - 2.5) * 1.0;
            }
        };
        _fadeTimer.Start();

        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int p = 2;
            DwmSetWindowAttribute(h, 33, ref p, Marshal.SizeOf<int>());
        };
    }
}
