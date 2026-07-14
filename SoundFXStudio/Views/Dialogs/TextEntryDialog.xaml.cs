using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SoundFXStudio.Views.Dialogs;

public partial class TextEntryDialog : Window, INotifyPropertyChanged
{
    private string _dialogTitle;
    private string _promptText;
    private string _value;

    public TextEntryDialog(string dialogTitle, string promptText, string initialValue, bool allowClear)
    {
        _dialogTitle = dialogTitle;
        _promptText = promptText;
        _value = initialValue;
        AllowClear = allowClear;

        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) =>
        {
            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool AllowClear { get; }

    public Visibility ClearButtonVisibility => AllowClear ? Visibility.Visible : Visibility.Collapsed;

    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetProperty(ref _dialogTitle, value);
    }

    public string PromptText
    {
        get => _promptText;
        set => SetProperty(ref _promptText, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public bool ClearRequested { get; private set; }

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

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearRequested = true;
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}