using System.Windows;
using System.Windows.Input;

namespace SoundFXStudio.Views.Dialogs;

public partial class CloseOptionsDialog : Window
{
    public CloseOptionsDialog()
    {
        InitializeComponent();
    }

    public bool ResetAudioOnClose { get; private set; } = true;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && DialogInteractionHelper.IsInteractiveElement(source))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void ResetCloseButton_Click(object sender, RoutedEventArgs e)
    {
        ResetAudioOnClose = true;
        Close();
    }

    private void JustCloseButton_Click(object sender, RoutedEventArgs e)
    {
        ResetAudioOnClose = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ResetAudioOnClose = false;
        Close();
    }
}
