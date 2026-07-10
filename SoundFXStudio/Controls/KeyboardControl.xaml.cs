using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

    public KeyboardControl()
    {
        InitializeComponent();
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
}