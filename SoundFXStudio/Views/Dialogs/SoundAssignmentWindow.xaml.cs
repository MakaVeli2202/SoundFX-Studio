using System.Windows;
using System.ComponentModel;
using Microsoft.Win32;

namespace SoundFXStudio.Views.Dialogs;

public partial class SoundAssignmentWindow : Window
{
    public SoundAssignmentWindow()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Audio File",
            Filter = "Audio Files|*.mp3;*.wav;*.ogg;*.flac;*.m4a|All Files|*.*",
            DefaultExt = ".mp3"
        };

        if (dialog.ShowDialog() == true)
        {
            if (DataContext is SoundAssignmentViewModel vm)
            {
                vm.FilePath = dialog.FileName;
                
                // Auto-fill name from filename
                if (string.IsNullOrEmpty(vm.Name))
                {
                    vm.Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            }
        }
    }

    private void ImageBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Image",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All Files|*.*",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog() == true)
        {
            if (DataContext is SoundAssignmentViewModel vm)
            {
                vm.ImagePath = dialog.FileName;
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SoundAssignmentViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                MessageBox.Show("Please enter a sound name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.FilePath))
            {
                MessageBox.Show("Please select an audio file.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}

/// <summary>
/// ViewModel for sound assignment dialog
/// </summary>
public class SoundAssignmentViewModel : INotifyPropertyChanged
{
    private string _filePath = string.Empty;
    private string _imagePath = string.Empty;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _selectedKey = string.Empty;
    private double _volumePercent = 100;
    private bool _isFavorite;
    private bool _loop;

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public string ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string SelectedKey
    {
        get => _selectedKey;
        set => SetProperty(ref _selectedKey, value);
    }

    public double VolumePercent
    {
        get => _volumePercent;
        set => SetProperty(ref _volumePercent, Math.Clamp(value, 0, 100));
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public bool Loop
    {
        get => _loop;
        set => SetProperty(ref _loop, value);
    }

    public List<string> AvailableKeys { get; } = new()
    {
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
    };

    public List<string> Categories { get; } = new()
    {
        "Games", "Memes", "Notifications", "Music", "Effects", "Other"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value)
    {
        if (!Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(field)));
        }
    }
}
