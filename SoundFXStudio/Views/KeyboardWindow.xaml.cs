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
    private const double BaseKeyboardWidth = 1306;
    private const double BaseKeyboardHeight = 870;
    private const double BaseChamferSize = 52;
    private const double KeyboardImageWidth = 1521;
    private const double KeyboardImageHeight = 618;

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

    public IReadOnlyList<ScaleOption> ScaleOptions { get; } = new[]
    {
        new ScaleOption("85%", 0.85),
        new ScaleOption("100%", 1.0),
        new ScaleOption("110%", 1.1),
        new ScaleOption("115%", 1.15),
        new ScaleOption("125%", 1.25),
        new ScaleOption("130%", 1.3),
        new ScaleOption("145%", 1.45)
    };

    public MainViewModel? ViewModel { get; private set; }

    public double SelectedWindowScale
    {
        get => _selectedWindowScale;
        set
        {
            if (Math.Abs(_selectedWindowScale - value) < double.Epsilon)
            {
                return;
            }

            _selectedWindowScale = value;
            OnPropertyChanged();

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

    private void KeyboardWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Close();
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CloseSettingsPanelButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
    }

    private void CloseKeyboardButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CalibrateKeyboardButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var window = new KeyboardCalibrationWindow
        {
            Owner = this
        };

        window.ShowDialog();
        ViewModel.RefreshCommand.Execute(null);
        ReloadWindowScale();
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

    private void NudgeButtonScale_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            var parts = tag.Split(':');
            if (parts.Length == 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var delta))
            {
                PreviewButtonScale += delta;
            }
        }
    }

    private void ApplyWindowScale(double scale)
    {
        var clampedScale = Math.Clamp(scale, 0.5, 2.0);
        Width = BaseKeyboardWidth * clampedScale;
        Height = BaseKeyboardHeight * clampedScale;

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + ((workArea.Height - Height) / 2);
        UpdateSettingsHostMargin();
        UpdateWindowClip();
    }

    private void RootSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowClip();
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

    private void UpdateSettingsHostMargin()
    {
        var scale = Math.Min(Width / BaseKeyboardWidth, Height / BaseKeyboardHeight);
        var fittedImageHeight = KeyboardImageHeight * (BaseKeyboardWidth / KeyboardImageWidth) * scale;
        var topInset = Math.Max(18, ((Height - fittedImageHeight) / 2) + (20 * scale));
        var rightInset = Math.Max(18, 28 * scale);

        SettingsHost.Margin = new Thickness(18, topInset, rightInset, 18);
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

    public sealed record ScaleOption(string Label, double Value);
}