using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SoundFXStudio.Views.Dialogs;

public partial class VolumeDialog : Window, INotifyPropertyChanged
{
    private double _volumePercent;

    public VolumeDialog(double initialVolumePercent)
    {
        _volumePercent = Math.Clamp(initialVolumePercent, 0, 100);
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public double VolumePercent
    {
        get => _volumePercent;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (Math.Abs(_volumePercent - clamped) < double.Epsilon)
            {
                return;
            }

            _volumePercent = clamped;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VolumePercent)));
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}