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
    private const double CanvasContentOffsetX = 12.31;
    private const double CanvasContentOffsetY = 179.69;
    private const double EscWidthUnits = 1.25;
    private const double StatusPanelHeight = 34;
    private const double StatusPanelGapAboveEsc = 6;
    private const double CloseButtonLedOffsetRight = 15;
    private const double CloseButtonLedOffsetUp = 10;

    private bool _suppressSelectionEvents;
    private double _selectedWindowScale = 1.0;
    private double _previewButtonScale = 1.0;
    private double _previewInnerInsetXPercent = 20;
    private double _previewInnerInsetYPercent = 20;
    private double _previewInnerOffsetXPercent;
    private double _previewInnerOffsetYPercent;

    private FrameworkElement? _draggingPanel;
    private Point _dragStart;
    private double _panelStartX;
    private double _panelStartY;

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
            SelectedWindowScale = calibration.KeyboardWindowScale > 0 ? calibration.KeyboardWindowScale : 0.8;
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
        UpdateTopControlsMargin();
        UpdateWindowClip();
    }

    private void UpdateTopControlsMargin()
    {
        var scale = Math.Min(Width / BaseKeyboardWidth, Height / BaseKeyboardHeight);
        var calibration = ViewModel?.Settings.KeyboardCalibration;

        if (calibration is null)
        {
            var inset = Math.Max(8, 10 * scale);
            TopControls.Margin = new Thickness(inset);
            BottomControls.Margin = new Thickness(inset);
            return;
        }

        // Soundboard status panel: top-left, right above the ESC key.
        var (escX, escY) = ComputeEscTopLeft(calibration);
        var escWinX = (escX - CanvasContentOffsetX) * scale;
        var escWinY = (escY - CanvasContentOffsetY) * scale;
        BottomControls.HorizontalAlignment = HorizontalAlignment.Left;
        BottomControls.VerticalAlignment = VerticalAlignment.Top;
        var statusX = calibration.SoundboardStatusPanelX > 0
            ? calibration.SoundboardStatusPanelX
            : Math.Max(8, escWinX);
        var statusY = calibration.SoundboardStatusPanelY > 0
            ? calibration.SoundboardStatusPanelY
            : Math.Max(8, escWinY - StatusPanelGapAboveEsc - StatusPanelHeight);
        BottomControls.Margin = new Thickness(statusX, statusY, 0, 0);

        // Close button: anchored to the scroll lock LED, up 10 and right 15 (window px).
        var scrollLedY = calibration.ScrollLockIndicatorOffsetY;
        if (scrollLedY < 220)
        {
            scrollLedY += 70;
        }

        var ledWinX = (calibration.ScrollLockIndicatorOffsetX - CanvasContentOffsetX) * scale;
        var ledWinY = (scrollLedY - CanvasContentOffsetY) * scale;
        TopControls.HorizontalAlignment = HorizontalAlignment.Left;
        TopControls.VerticalAlignment = VerticalAlignment.Top;
        var closeX = calibration.CloseButtonPanelX > 0
            ? calibration.CloseButtonPanelX
            : ledWinX + CloseButtonLedOffsetRight;
        var closeY = calibration.CloseButtonPanelY > 0
            ? calibration.CloseButtonPanelY
            : ledWinY - CloseButtonLedOffsetUp;
        TopControls.Margin = new Thickness(closeX, closeY, 0, 0);
    }

    private void BottomControls_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => StartPanelDrag(BottomControls, e);

    private void TopControls_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => StartPanelDrag(TopControls, e);

    private void StartPanelDrag(FrameworkElement panel, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && IsInteractiveElement(source))
        {
            return;
        }

        _draggingPanel = panel;
        _dragStart = e.GetPosition(RootSurface);
        _panelStartX = panel.Margin.Left;
        _panelStartY = panel.Margin.Top;
        panel.CaptureMouse();
        e.Handled = true;
    }

    private void BottomControls_MouseMove(object sender, MouseEventArgs e)
        => DragPanel(BottomControls, e);

    private void TopControls_MouseMove(object sender, MouseEventArgs e)
        => DragPanel(TopControls, e);

    private void DragPanel(FrameworkElement panel, MouseEventArgs e)
    {
        if (_draggingPanel is null || !ReferenceEquals(_draggingPanel, panel))
        {
            return;
        }

        var pos = e.GetPosition(RootSurface);
        var x = Math.Max(0, _panelStartX + (pos.X - _dragStart.X));
        var y = Math.Max(0, _panelStartY + (pos.Y - _dragStart.Y));
        panel.Margin = new Thickness(x, y, 0, 0);
        e.Handled = true;
    }

    private void BottomControls_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => EndPanelDrag(e);

    private void TopControls_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => EndPanelDrag(e);

    private void EndPanelDrag(MouseEventArgs e)
    {
        if (_draggingPanel is null)
        {
            return;
        }

        if (_draggingPanel.IsMouseCaptured)
        {
            _draggingPanel.ReleaseMouseCapture();
        }

        _draggingPanel = null;
        PersistPanelPositions();
        e.Handled = true;
    }

    private void BottomControls_MouseLeave(object sender, MouseEventArgs e)
        => CancelPanelDrag();

    private void TopControls_MouseLeave(object sender, MouseEventArgs e)
        => CancelPanelDrag();

    private void CancelPanelDrag()
    {
        if (_draggingPanel is not null && !_draggingPanel.IsMouseCaptured)
        {
            _draggingPanel = null;
        }
    }

    private void PersistPanelPositions()
    {
        if (ViewModel?.Settings.KeyboardCalibration is not { } calibration)
        {
            return;
        }

        calibration.SoundboardStatusPanelX = Math.Max(0, BottomControls.Margin.Left);
        calibration.SoundboardStatusPanelY = Math.Max(0, BottomControls.Margin.Top);
        calibration.CloseButtonPanelX = Math.Max(0, TopControls.Margin.Left);
        calibration.CloseButtonPanelY = Math.Max(0, TopControls.Margin.Top);
        ViewModel.SaveKeyboardCalibrationSettings();
    }

    private static (double X, double Y) ComputeEscTopLeft(KeyboardCalibrationSettings calibration)
    {
        var keyUnit = calibration.KeyUnit > 0 ? calibration.KeyUnit : 43d;
        var buttonScale = calibration.ButtonScale > 0 ? calibration.ButtonScale : 1d;

        calibration.KeyOverrides.TryGetValue("ESC-0-0", out var escOverride);

        var baseWidth = (EscWidthUnits * keyUnit) + (escOverride?.WidthAdjustment ?? 0d);
        var baseHeight = keyUnit + (escOverride?.HeightAdjustment ?? 0d);
        var width = Math.Max(1d, (baseWidth * buttonScale) + calibration.EscWidthAdjustment);
        var height = Math.Max(1d, (baseHeight * buttonScale) + calibration.EscHeightAdjustment);

        var x = calibration.OffsetX + calibration.EscOffsetX + (escOverride?.OffsetX ?? 0d) + ((baseWidth - width) / 2d);
        var y = calibration.OffsetY + calibration.EscOffsetY + (escOverride?.OffsetY ?? 0d) + ((baseHeight - height) / 2d);
        return (x, y);
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