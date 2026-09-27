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

    public void Complete(string finalStep = "Everything ready — have fun!", bool succeeded = true)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => Complete(finalStep, succeeded));
            return;
        }

        Spinner.Visibility = Visibility.Collapsed;
        SpinnerGlow.Visibility = Visibility.Collapsed;
        SpinnerBright.Visibility = Visibility.Collapsed;
        CheckIcon.Visibility = succeeded ? Visibility.Visible : Visibility.Collapsed;
        FailureIcon.Visibility = succeeded ? Visibility.Collapsed : Visibility.Visible;
        StepText.Text = finalStep;
    }
}
