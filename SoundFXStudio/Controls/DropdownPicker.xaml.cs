using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SoundFXStudio.Controls;

public partial class DropdownPicker : UserControl
{
    private bool _programmatic;

    public event EventHandler? Opened;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(DropdownPicker),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(DropdownPicker),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedItemChanged));

    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(DropdownPicker),
            new PropertyMetadata(null, OnDisplayMemberPathChanged));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(DropdownPicker),
            new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public Brush? AccentBrush
    {
        get => (Brush?)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public bool IsDropdownOpen => DropPanel.Visibility == Visibility.Visible;

    public DropdownPicker()
    {
        InitializeComponent();
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DropdownPicker)d).UpdateSelectedText();

    private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DropdownPicker)d).UpdateSelectedText();

    private void ToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DropPanel.Visibility == Visibility.Visible)
        {
            ClosePanel();
            return;
        }

        OpenPanel();
    }

    private void OpenPanel()
    {
        _programmatic = true;
        ItemsList.ItemsSource = ItemsSource;
        ItemsList.SelectedItem = SelectedItem;
        _programmatic = false;

        DropPanel.Visibility = Visibility.Visible;
        Opened?.Invoke(this, EventArgs.Empty);
        ItemsList.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            ItemsList.Focus();
            if (ItemsList.Items.Count > 0)
                ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }));
    }

    private void ClosePanel()
    {
        DropPanel.Visibility = Visibility.Collapsed;
    }

    public void CloseDropdown()
    {
        ClosePanel();
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_programmatic)
            return;

        if (ItemsList.SelectedItem is not null)
            SelectedItem = ItemsList.SelectedItem;
    }

    private void ItemsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl itemsControl)
            return;

        if (itemsControl.ContainerFromElement((DependencyObject)e.OriginalSource) is not ListBoxItem item)
            return;

        if (itemsControl.ItemContainerGenerator.ItemFromContainer(item) is { } clicked
            && clicked != DependencyProperty.UnsetValue)
        {
            _programmatic = true;
            ItemsList.SelectedItem = clicked;
            _programmatic = false;
            if (clicked is not null)
                SelectedItem = clicked;
        }
    }

    private void ItemsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => ClosePanel();

    private void UpdateSelectedText()
    {
        if (ToggleBtn is null)
            return;

        var item = SelectedItem;
        string? text = null;

        if (item is not null)
        {
            if (!string.IsNullOrEmpty(DisplayMemberPath))
                text = GetPropertyValue(item, DisplayMemberPath) as string;
            text ??= item.ToString();
        }

        ToggleBtn.Content = text ?? "Select…";
    }

    private static object? GetPropertyValue(object target, string path)
    {
        var current = target;
        foreach (var segment in path.Split('.'))
        {
            if (current is null)
                return null;
            var prop = current.GetType().GetProperty(segment);
            current = prop?.GetValue(current);
        }

        return current;
    }
}
