using System.Windows;
using System.Windows.Controls;

namespace SoundFXStudio.Controls;

public partial class WindowControlBar : UserControl
{
    public static readonly RoutedEvent MinimizeClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(MinimizeClicked),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(WindowControlBar));

    public static readonly RoutedEvent CloseClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(CloseClicked),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(WindowControlBar));

    public event RoutedEventHandler MinimizeClicked
    {
        add => AddHandler(MinimizeClickedEvent, value);
        remove => RemoveHandler(MinimizeClickedEvent, value);
    }

    public event RoutedEventHandler CloseClicked
    {
        add => AddHandler(CloseClickedEvent, value);
        remove => RemoveHandler(CloseClickedEvent, value);
    }

    public static readonly DependencyProperty ShowMinimizeProperty = DependencyProperty.Register(
        nameof(ShowMinimize),
        typeof(bool),
        typeof(WindowControlBar),
        new PropertyMetadata(true, OnShowMinimizeChanged));

    public bool ShowMinimize
    {
        get => (bool)GetValue(ShowMinimizeProperty);
        set => SetValue(ShowMinimizeProperty, value);
    }

    public WindowControlBar()
    {
        InitializeComponent();
    }

    private static void OnShowMinimizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WindowControlBar bar && bar.MinimizeButton is not null)
        {
            bar.MinimizeButton.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(MinimizeClickedEvent));

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(CloseClickedEvent));
}
