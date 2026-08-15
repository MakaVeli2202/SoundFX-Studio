using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace SoundFXStudio.Views.Dialogs;

public sealed class ToastWindow : Window
{
    private const double ToastWidth = 340;
    private const double ToastHeight = 66;
    private const double ScreenMargin = 16;
    private const double ToastGap = 10;
    private static readonly List<ToastWindow> ActiveToasts = new();
    private bool _closed;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);

    public ToastWindow(string title, string message, string status, Color accent)
    {
        Width = ToastWidth;
        Height = ToastHeight;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Opacity = 0;

        Content = BuildCard(title, message, status, accent);

        ActiveToasts.Insert(0, this);
        foreach (var toast in ActiveToasts)
        {
            toast.Position();
        }

        Closed += (_, _) =>
        {
            _closed = true;
            ActiveToasts.Remove(this);
            foreach (var toast in ActiveToasts)
            {
                toast.Position();
            }
        };

        Loaded += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int isDark = 1;
            DwmSetWindowAttribute(handle, 33, ref isDark, Marshal.SizeOf<int>());
            AnimateIn();
            _ = ScheduleClose();
        };
    }

    public ToastWindow(string message)
        : this("SoundFX Studio", message, "INFO", Color.FromRgb(0x5A, 0xD8, 0xFF))
    {
    }

    public static void Show(string title, string message, bool isOn)
    {
        var accent = isOn
            ? Color.FromRgb(0x10, 0xB9, 0x81)
            : Color.FromRgb(0xF4, 0x3F, 0x5E);
        Show(title, message, isOn ? "ON" : "OFF", accent);
    }

    public static void ShowDiscordStudioTip()
    {
        Show(
            "Discord Input Profile",
            "Set Discord > Settings > Voice & Video > Input Profile to Studio for cleaner audio and fewer glitches.",
            "TIP",
            Color.FromRgb(0x00, 0xD4, 0xFF));
    }

    public static void Show(string title, string message, string status, Color accent)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Show(title, message, status, accent));
            return;
        }

        try
        {
            new ToastWindow(title, message, status, accent).Show();
        }
        catch
        {
        }
    }

    private static Border BuildCard(string title, string message, string status, Color accent)
    {
        var accentBrush = new SolidColorBrush(accent);

        var bar = new Border
        {
            Background = accentBrush,
            Width = 5,
            HorizontalAlignment = HorizontalAlignment.Left,
            Effect = new DropShadowEffect { Color = accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.55 }
        };

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC2, 0xD2, 0xEC)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 12, 0)
        };
        textStack.Children.Add(titleText);
        textStack.Children.Add(messageText);

        var pill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B)),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = status,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(bar, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(pill, 2);
        grid.Children.Add(bar);
        grid.Children.Add(textStack);
        grid.Children.Add(pill);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x0B, 0x15, 0x28)),
            BorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x00, 0xD4, 0xFF), 0),
                    new GradientStop(Color.FromRgb(0x7B, 0x3F, 0xFF), 0.55),
                    new GradientStop(Color.FromRgb(0xFF, 0x2D, 0xAF), 1)
                }
            },
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x00, 0xD4, 0xFF),
                BlurRadius = 16,
                ShadowDepth = 0,
                Opacity = 0.45
            },
            Child = grid
        };
    }

    private void Position()
    {
        var index = ActiveToasts.IndexOf(this);
        if (index < 0)
        {
            return;
        }

        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - ScreenMargin;
        var top = work.Bottom - (Height + ScreenMargin) * (index + 1) - ToastGap * index;
        if (top < work.Top)
        {
            top = work.Top;
        }

        if (IsLoaded)
        {
            var animation = new DoubleAnimation(Top, top, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(TopProperty, animation);
        }
        else
        {
            Top = top;
        }
    }

    private void AnimateIn()
    {
        if (Content is not Border root)
        {
            return;
        }

        var transform = new TranslateTransform { X = ToastWidth };
        root.RenderTransform = transform;
        root.RenderTransformOrigin = new Point(1, 0.5);

        var slide = new DoubleAnimation(ToastWidth, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240));
        transform.BeginAnimation(TranslateTransform.XProperty, slide);
        BeginAnimation(OpacityProperty, fade);
    }

    private async Task ScheduleClose()
    {
        try
        {
            await Task.Delay(3000);
            if (_closed)
            {
                return;
            }

            await AnimateOut();
        }
        finally
        {
            if (!_closed)
            {
                Close();
            }
        }
    }

    private async Task AnimateOut()
    {
        if (Content is not Border root || root.RenderTransform is not TranslateTransform transform)
        {
            return;
        }

        var slide = new DoubleAnimation(transform.X, ToastWidth, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(260));

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            slide.Completed -= handler;
            tcs.TrySetResult(true);
        };
        slide.Completed += handler;

        transform.BeginAnimation(TranslateTransform.XProperty, slide);
        BeginAnimation(OpacityProperty, fade);
        await tcs.Task;
    }
}
