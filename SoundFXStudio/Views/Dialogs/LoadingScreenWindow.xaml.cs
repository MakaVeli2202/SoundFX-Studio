using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SoundFXStudio.Views.Dialogs;

public partial class LoadingScreenWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);

    public LoadingScreenWindow()
    {
        InitializeComponent();
        Loaded += LoadingScreenWindow_Loaded;
        PreviewMouseLeftButtonDown += TryDragWindow;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void LoadingScreenWindow_Loaded(object sender, RoutedEventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int noRound = 1;
        DwmSetWindowAttribute(hwnd, 33, ref noRound, Marshal.SizeOf<int>());
    }

    private void TryDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }
}
