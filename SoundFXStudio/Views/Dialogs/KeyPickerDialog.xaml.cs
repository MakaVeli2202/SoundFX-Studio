using SoundFXStudio.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

namespace SoundFXStudio.Views.Dialogs;

public partial class KeyPickerDialog : Window, INotifyPropertyChanged
{
    private readonly ICollectionView _filteredKeys;
    private string _searchText = string.Empty;
    private KeyboardKey? _selectedKey;

    public KeyPickerDialog(IEnumerable<KeyboardKey> keys, string? soundName = null)
    {
        InitializeComponent();
        Keys = new ObservableCollection<KeyboardKey>(keys.OrderBy(k => k.DisplayLabel));
        _filteredKeys = CollectionViewSource.GetDefaultView(Keys);
        _filteredKeys.Filter = FilterKey;
        DataContext = this;
        Title = $"Assign to Key{(soundName is not null ? $" — {soundName}" : "")}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<KeyboardKey> Keys { get; }

    public ICollectionView FilteredKeys => _filteredKeys;

    public KeyboardKey? SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (_selectedKey == value) return;
            _selectedKey = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            _filteredKeys.Refresh();
            OnPropertyChanged();
        }
    }

    private bool FilterKey(object candidate)
    {
        if (candidate is not KeyboardKey key) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return key.DisplayLabel.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || key.KeyName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void AssignButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedKey is null) return;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
