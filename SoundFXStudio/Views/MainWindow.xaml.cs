using SoundFXStudio.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SoundFXStudio;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += MainWindow_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
        AllowDrop = true;
        Drop += MainWindow_Drop;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        DragMove();
    }

    private void WindowShell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsInteractiveElement(source))
        {
            return;
        }

        DragMove();
    }

    private static bool IsInteractiveElement(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ButtonBase || current is TextBoxBase || current is ComboBox || current is ListBox || current is ListView || current is MenuItem || current is CheckBox || current is TabItem || current is Slider || current is PasswordBox || current is ScrollBar || current is Thumb)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e) => ToggleWindowState();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Title = ViewModel.WindowTitle;
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.WindowTitle))
            {
                Title = ViewModel.WindowTitle;
            }
        };

        ViewModel.AttachWindow(this);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        ViewModel.HandlePreviewKeyDown(e);
    }

    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        ViewModel.HandlePreviewKeyUp(e);
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        ViewModel.HandleDropFiles(files);
    }

    private void MainWindow_OpenSoundSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("control", "mmsys.cpl,,1") { UseShellExecute = true });
        }
        catch
        {
            ViewModel.StatusText = "Could not open Windows Sound settings.";
        }
    }
}