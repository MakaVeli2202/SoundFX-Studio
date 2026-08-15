using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SoundFXStudio.Models;
using SoundFXStudio.ViewModels;

namespace SoundFXStudio.Controls;

public partial class KeyboardControl : UserControl
{
    public static readonly DependencyProperty ButtonScaleProperty = DependencyProperty.Register(
        nameof(ButtonScale),
        typeof(double),
        typeof(KeyboardControl),
        new PropertyMetadata(0.8d));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(KeyboardControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty KeyClickedCommandProperty = DependencyProperty.Register(
        nameof(KeyClickedCommand),
        typeof(ICommand),
        typeof(KeyboardControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty CalibrationSelectionHighlightProperty = DependencyProperty.Register(
        nameof(CalibrationSelectionHighlight),
        typeof(bool),
        typeof(KeyboardControl),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ShowSelectionHighlightProperty = DependencyProperty.Register(
        nameof(ShowSelectionHighlight),
        typeof(bool),
        typeof(KeyboardControl),
        new PropertyMetadata(true));

    public static readonly DependencyProperty InnerInsetXPercentProperty = DependencyProperty.Register(
        nameof(InnerInsetXPercent),
        typeof(double),
        typeof(KeyboardControl),
        new PropertyMetadata(20d));

    public static readonly DependencyProperty InnerInsetYPercentProperty = DependencyProperty.Register(
        nameof(InnerInsetYPercent),
        typeof(double),
        typeof(KeyboardControl),
        new PropertyMetadata(20d));

    public static readonly DependencyProperty InnerOffsetXPercentProperty = DependencyProperty.Register(
        nameof(InnerOffsetXPercent),
        typeof(double),
        typeof(KeyboardControl),
        new PropertyMetadata(0d));

    public static readonly DependencyProperty InnerOffsetYPercentProperty = DependencyProperty.Register(
        nameof(InnerOffsetYPercent),
        typeof(double),
        typeof(KeyboardControl),
        new PropertyMetadata(0d));

    public static readonly DependencyProperty IsCapsLockOnProperty = DependencyProperty.Register(
        nameof(IsCapsLockOn),
        typeof(bool),
        typeof(KeyboardControl),
        new PropertyMetadata(false));

    public static readonly DependencyProperty IsNumLockOnProperty = DependencyProperty.Register(
        nameof(IsNumLockOn),
        typeof(bool),
        typeof(KeyboardControl),
        new PropertyMetadata(false));

    public static readonly DependencyProperty IsScrollLockOnProperty = DependencyProperty.Register(
        nameof(IsScrollLockOn),
        typeof(bool),
        typeof(KeyboardControl),
        new PropertyMetadata(false));

    public static readonly DependencyProperty IsCalibrationModeProperty = DependencyProperty.Register(
        nameof(IsCalibrationMode),
        typeof(bool),
        typeof(KeyboardControl),
        new PropertyMetadata(false));

    public KeyboardControl()
    {
        InitializeComponent();
    }

    private void KeyButton_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(SoundEntry)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void KeyButton_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not KeyboardKey key)
        {
            return;
        }

        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (e.Data.GetData(typeof(SoundEntry)) is not SoundEntry sound)
        {
            return;
        }

        viewModel.AssignSoundToKeyFromUi(sound, key);
        e.Handled = true;
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public double ButtonScale
    {
        get => (double)GetValue(ButtonScaleProperty);
        set => SetValue(ButtonScaleProperty, value);
    }

    public ICommand? KeyClickedCommand
    {
        get => (ICommand?)GetValue(KeyClickedCommandProperty);
        set => SetValue(KeyClickedCommandProperty, value);
    }

    public bool CalibrationSelectionHighlight
    {
        get => (bool)GetValue(CalibrationSelectionHighlightProperty);
        set => SetValue(CalibrationSelectionHighlightProperty, value);
    }

    public bool ShowSelectionHighlight
    {
        get => (bool)GetValue(ShowSelectionHighlightProperty);
        set => SetValue(ShowSelectionHighlightProperty, value);
    }

    public double InnerInsetXPercent
    {
        get => (double)GetValue(InnerInsetXPercentProperty);
        set => SetValue(InnerInsetXPercentProperty, value);
    }

    public double InnerInsetYPercent
    {
        get => (double)GetValue(InnerInsetYPercentProperty);
        set => SetValue(InnerInsetYPercentProperty, value);
    }

    public double InnerOffsetXPercent
    {
        get => (double)GetValue(InnerOffsetXPercentProperty);
        set => SetValue(InnerOffsetXPercentProperty, value);
    }

    public double InnerOffsetYPercent
    {
        get => (double)GetValue(InnerOffsetYPercentProperty);
        set => SetValue(InnerOffsetYPercentProperty, value);
    }

    public bool IsCapsLockOn
    {
        get => (bool)GetValue(IsCapsLockOnProperty);
        set => SetValue(IsCapsLockOnProperty, value);
    }

    public bool IsNumLockOn
    {
        get => (bool)GetValue(IsNumLockOnProperty);
        set => SetValue(IsNumLockOnProperty, value);
    }

    public bool IsScrollLockOn
    {
        get => (bool)GetValue(IsScrollLockOnProperty);
        set => SetValue(IsScrollLockOnProperty, value);
    }

    public bool IsCalibrationMode
    {
        get => (bool)GetValue(IsCalibrationModeProperty);
        set => SetValue(IsCalibrationModeProperty, value);
    }
}