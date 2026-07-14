using SoundFXStudio.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

namespace SoundFXStudio.Views.Dialogs;

public partial class AssignSoundToKeyDialog : Window, INotifyPropertyChanged
{
    private readonly ICollectionView _filteredSounds;
    private string _searchText = string.Empty;
    private SoundEntry? _selectedSound;

    public AssignSoundToKeyDialog(IEnumerable<SoundEntry> sounds)
    {
        InitializeComponent();
        Sounds = new ObservableCollection<SoundEntry>(sounds.OrderBy(sound => sound.Name));
        _filteredSounds = CollectionViewSource.GetDefaultView(Sounds);
        _filteredSounds.Filter = FilterSound;
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SoundEntry> Sounds { get; }

    public ICollectionView FilteredSounds => _filteredSounds;

    public Visibility EmptyStateVisibility => _filteredSounds.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    public SoundEntry? SelectedSound
    {
        get => _selectedSound;
        set
        {
            if (_selectedSound == value)
            {
                return;
            }

            _selectedSound = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            _filteredSounds.Refresh();
            OnPropertyChanged();
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }
    }

    private bool FilterSound(object candidate)
    {
        if (candidate is not SoundEntry sound)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return sound.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || sound.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || sound.FilePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void AssignButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSound is null)
        {
            return;
        }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}