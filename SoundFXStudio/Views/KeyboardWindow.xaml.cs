using SoundFXStudio.Models;
using SoundFXStudio.ViewModels;
using SoundFXStudio.Views.Dialogs;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SoundFXStudio.Views;

public partial class KeyboardWindow : Window, INotifyPropertyChanged
{
    private const double BaseKeyboardWidth = 1512.6;
    private const double BaseKeyboardHeight = 608;
    private const double BaseChamferSize = 52;

    private bool _suppressSelectionEvents;
    private double _selectedWindowScale = 1.0;
    private double _previewButtonScale = 1.0;
    private double _previewInnerInsetXPercent = 20;
    private double _previewInnerInsetYPercent = 20;
    private double _previewInnerOffsetXPercent;
    private double _previewInnerOffsetYPercent;

    public KeyboardWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel? ViewModel { get; private set; }

    public double SelectedWindowScale
    {
        get => _selectedWindowScale;
        set
        {
            var clamped = Math.Clamp(value, 0.5, 2.0);
            if (Math.Abs(_selectedWindowScale - clamped) < double.Epsilon)
            {
                return;
            }

            _selectedWindowScale = clamped;
            OnPropertyChanged();

            if (ScaleDisplay is not null)
            {
                ScaleDisplay.Text = $"{clamped * 100:F0}%";
            }

            if (!_suppressSelectionEvents)
            {
                PersistWindowScale();
            }
        }
    }

    public double PreviewButtonScale
    {
        get => _previewButtonScale;
        set
        {
            var clamped = Math.Clamp(value, 0.1, 3.0);
            if (Math.Abs(_previewButtonScale - clamped) < double.Epsilon)
            {
                return;
            }

            _previewButtonScale = clamped;
            OnPropertyChanged();

            if (!_suppressSelectionEvents)
            {
                PersistButtonScale();
            }
        }
    }

    public double PreviewInnerInsetXPercent
    {
        get => _previewInnerInsetXPercent;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 45.0);
            if (Math.Abs(_previewInnerInsetXPercent - clamped) < double.Epsilon)
            {
                return;
            }

            _previewInnerInsetXPercent = clamped;
            OnPropertyChanged();

            if (!_suppressSelectionEvents)
            {
                PersistInnerSectionCalibration();
            }
        }
    }

    public double PreviewInnerInsetYPercent
    {
        get => _previewInnerInsetYPercent;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 45.0);
            if (Math.Abs(_previewInnerInsetYPercent - clamped) < double.Epsilon)
            {
                return;
            }

            _previewInnerInsetYPercent = clamped;
            OnPropertyChanged();

            if (!_suppressSelectionEvents)
            {
                PersistInnerSectionCalibration();
            }
        }
    }

    public double PreviewInnerOffsetXPercent
    {
        get => _previewInnerOffsetXPercent;
        set
        {
            var clamped = Math.Clamp(value, -30.0, 30.0);
            if (Math.Abs(_previewInnerOffsetXPercent - clamped) < double.Epsilon)
            {
                return;
            }

            _previewInnerOffsetXPercent = clamped;
            OnPropertyChanged();

            if (!_suppressSelectionEvents)
            {
                PersistInnerSectionCalibration();
            }
        }
    }

    public double PreviewInnerOffsetYPercent
    {
        get => _previewInnerOffsetYPercent;
        set
        {
            var clamped = Math.Clamp(value, -30.0, 30.0);
            if (Math.Abs(_previewInnerOffsetYPercent - clamped) < double.Epsilon)
            {
                return;
            }

            _previewInnerOffsetYPercent = clamped;
            OnPropertyChanged();

            if (!_suppressSelectionEvents)
            {
                PersistInnerSectionCalibration();
            }
        }
    }

    public void Initialize(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        OnPropertyChanged(nameof(ViewModel));
        ReloadWindowScale();
    }

    private void KeyboardWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        ViewModel?.HandlePreviewKeyDown(e);
    }

    private void KeyboardWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        ViewModel?.HandlePreviewKeyUp(e);
    }

    private void KeyboardWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && IsInteractiveElement(source))
        {
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SoundboardToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleSoundboardMode();
    }

    private void ZoomInScale_Click(object sender, RoutedEventArgs e)
    {
        SelectedWindowScale += 0.05;
    }

    private void ZoomOutScale_Click(object sender, RoutedEventArgs e)
    {
        SelectedWindowScale -= 0.05;
    }

    private void ReloadWindowScale()
    {
        if (ViewModel is null)
        {
            return;
        }

        var calibration = ViewModel.Settings.KeyboardCalibration;

        _suppressSelectionEvents = true;
        try
        {
            SelectedWindowScale = calibration.KeyboardWindowScale > 0 ? calibration.KeyboardWindowScale : 1.0;
            PreviewButtonScale = calibration.ButtonScale > 0 ? calibration.ButtonScale : 1.0;
            PreviewInnerInsetXPercent = Math.Abs(calibration.InnerSectionInsetXPercent) > double.Epsilon ? calibration.InnerSectionInsetXPercent : calibration.InnerSectionInsetPercent;
            PreviewInnerInsetYPercent = Math.Abs(calibration.InnerSectionInsetYPercent) > double.Epsilon ? calibration.InnerSectionInsetYPercent : calibration.InnerSectionInsetPercent;
            PreviewInnerOffsetXPercent = calibration.InnerSectionOffsetXPercent;
            PreviewInnerOffsetYPercent = calibration.InnerSectionOffsetYPercent;
        }
        finally
        {
            _suppressSelectionEvents = false;
        }

        ApplyWindowScale(SelectedWindowScale);
    }

    private void PersistWindowScale()
    {
        if (ViewModel is null)
        {
            return;
        }

        var calibration = ViewModel.Settings.KeyboardCalibration;
        calibration.KeyboardWindowScale = SelectedWindowScale;
        ViewModel.SaveKeyboardCalibrationSettings();
        ApplyWindowScale(SelectedWindowScale);
    }

    private void PersistButtonScale()
    {
        if (ViewModel is null)
        {
            return;
        }

        var calibration = ViewModel.Settings.KeyboardCalibration;
        calibration.ButtonScale = PreviewButtonScale;
        ViewModel.SaveKeyboardCalibrationSettings();
    }

    private void PersistInnerSectionCalibration()
    {
        if (ViewModel is null)
        {
            return;
        }

        var calibration = ViewModel.Settings.KeyboardCalibration;
        calibration.InnerSectionInsetXPercent = PreviewInnerInsetXPercent;
        calibration.InnerSectionInsetYPercent = PreviewInnerInsetYPercent;
        calibration.InnerSectionInsetPercent = (PreviewInnerInsetXPercent + PreviewInnerInsetYPercent) / 2d;
        calibration.InnerSectionOffsetXPercent = PreviewInnerOffsetXPercent;
        calibration.InnerSectionOffsetYPercent = PreviewInnerOffsetYPercent;
        ViewModel.SaveKeyboardCalibrationSettings();
    }

    private void ApplyWindowScale(double scale)
    {
        var clampedScale = Math.Clamp(scale, 0.5, 2.0);
        Width = BaseKeyboardWidth * clampedScale;
        Height = BaseKeyboardHeight * clampedScale;

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + ((workArea.Height - Height) / 2);
        UpdateTopControlsMargin();
        UpdateWindowClip();
    }

    private void RootSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowClip();
    }

    private void UpdateTopControlsMargin()
    {
        var scale = Math.Min(Width / BaseKeyboardWidth, Height / BaseKeyboardHeight);
        var inset = Math.Max(8, 10 * scale);
        TopControls.Margin = new Thickness(inset, inset, inset, inset);
    }

    private void UpdateWindowClip()
    {
        if (RootSurface.ActualWidth <= 0 || RootSurface.ActualHeight <= 0)
        {
            return;
        }

        var chamfer = Math.Min(RootSurface.ActualWidth, RootSurface.ActualHeight) * (BaseChamferSize / BaseKeyboardHeight);
        var width = RootSurface.ActualWidth;
        var height = RootSurface.ActualHeight;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(chamfer, 0), true, true);
            context.LineTo(new Point(width - chamfer, 0), true, false);
            context.LineTo(new Point(width, chamfer), true, false);
            context.LineTo(new Point(width, height - chamfer), true, false);
            context.LineTo(new Point(width - chamfer, height), true, false);
            context.LineTo(new Point(chamfer, height), true, false);
            context.LineTo(new Point(0, height - chamfer), true, false);
            context.LineTo(new Point(0, chamfer), true, false);
        }

        geometry.Freeze();
        RootSurface.Clip = geometry;
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}