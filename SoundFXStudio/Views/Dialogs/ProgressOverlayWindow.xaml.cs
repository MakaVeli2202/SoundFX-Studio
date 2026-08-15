using System.Windows;

namespace SoundFXStudio.Views.Dialogs;

public partial class ProgressOverlayWindow : Window
{
    public ProgressOverlayWindow(string title)
    {
        InitializeComponent();
        TitleText.Text = title;
        Owner ??= Application.Current?.MainWindow;
    }

    public void UpdateStep(string step)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateStep(step));
            return;
        }

        StepText.Text = step;
    }

    public void Complete(string finalStep = "Everything ready — have fun!")
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => Complete(finalStep));
            return;
        }

        Spinner.Visibility = Visibility.Collapsed;
        SpinnerGlow.Visibility = Visibility.Collapsed;
        SpinnerBright.Visibility = Visibility.Collapsed;
        CheckIcon.Visibility = Visibility.Visible;
        StepText.Text = finalStep;
    }
}
