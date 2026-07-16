using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

public class KeyboardKey : ObservableObject
{
    private string _id = string.Empty;
    private string _keyName = string.Empty;
    private string _displayLabel = string.Empty;
    private double _widthUnits = 1;
    private double _heightUnits = 1;
    private int _rowIndex;
    private double _columnIndex;
    private string? _imagePath;
    private string? _assignedSoundId;
    private string? _assignedSoundName;
    private string? _assignmentName;
    private string _categoryAccentColor = "#00000000";
    private KeyState _state = KeyState.Empty;
    private bool _isEnabled = true;
    private bool _isSelected;
    private bool _isHovered;
    private double _innerInsetAdjustmentPercent;
    private double _innerInsetXAdjustmentPercent;
    private double _innerInsetYAdjustmentPercent;
    private double _innerOffsetXAdjustmentPercent;
    private double _innerOffsetYAdjustmentPercent;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string KeyName
    {
        get => _keyName;
        set
        {
            if (SetProperty(ref _keyName, value))
            {
                OnPropertyChanged(nameof(AutomationName));
            }
        }
    }

    public string DisplayLabel
    {
        get => string.IsNullOrWhiteSpace(_displayLabel) ? _keyName : _displayLabel;
        set
        {
            if (SetProperty(ref _displayLabel, value))
            {
                OnPropertyChanged(nameof(AutomationName));
            }
        }
    }

    public string AutomationName
    {
        get
        {
            if (string.Equals(KeyName, "ESC", StringComparison.OrdinalIgnoreCase))
            {
                return "Escape";
            }

            if (string.Equals(KeyName, "SPACE", StringComparison.OrdinalIgnoreCase))
            {
                return "Space";
            }

            if (string.Equals(KeyName, "ENTER", StringComparison.OrdinalIgnoreCase))
            {
                return "Enter";
            }

            return string.IsNullOrWhiteSpace(DisplayLabel) ? KeyName : DisplayLabel;
        }
    }

    public double WidthUnits
    {
        get => _widthUnits;
        set => SetProperty(ref _widthUnits, value);
    }

    public double HeightUnits
    {
        get => _heightUnits;
        set => SetProperty(ref _heightUnits, value);
    }

    public int RowIndex
    {
        get => _rowIndex;
        set => SetProperty(ref _rowIndex, value);
    }

    public double ColumnIndex
    {
        get => _columnIndex;
        set => SetProperty(ref _columnIndex, value);
    }

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            if (SetProperty(ref _imagePath, value))
            {
                OnPropertyChanged(nameof(HasAssignment));
            }
        }
    }

    public string? AssignedSoundId
    {
        get => _assignedSoundId;
        set
        {
            if (SetProperty(ref _assignedSoundId, value))
            {
                OnPropertyChanged(nameof(HasAssignment));
            }
        }
    }

    public string? AssignedSoundName
    {
        get => _assignedSoundName;
        set
        {
            if (SetProperty(ref _assignedSoundName, value))
            {
                OnPropertyChanged(nameof(HasAssignment));
            }
        }
    }

    public string? AssignmentName
    {
        get => _assignmentName;
        set
        {
            if (SetProperty(ref _assignmentName, value))
            {
                OnPropertyChanged(nameof(HasAssignment));
            }
        }
    }

    public string CategoryAccentColor
    {
        get => _categoryAccentColor;
        set => SetProperty(ref _categoryAccentColor, value);
    }

    public KeyState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsHovered
    {
        get => _isHovered;
        set => SetProperty(ref _isHovered, value);
    }

    public double InnerInsetAdjustmentPercent
    {
        get => _innerInsetAdjustmentPercent;
        set => SetProperty(ref _innerInsetAdjustmentPercent, value);
    }

    public double InnerInsetXAdjustmentPercent
    {
        get => _innerInsetXAdjustmentPercent;
        set => SetProperty(ref _innerInsetXAdjustmentPercent, value);
    }

    public double InnerInsetYAdjustmentPercent
    {
        get => _innerInsetYAdjustmentPercent;
        set => SetProperty(ref _innerInsetYAdjustmentPercent, value);
    }

    public double InnerOffsetXAdjustmentPercent
    {
        get => _innerOffsetXAdjustmentPercent;
        set => SetProperty(ref _innerOffsetXAdjustmentPercent, value);
    }

    public double InnerOffsetYAdjustmentPercent
    {
        get => _innerOffsetYAdjustmentPercent;
        set => SetProperty(ref _innerOffsetYAdjustmentPercent, value);
    }

    public bool HasAssignment => !string.IsNullOrWhiteSpace(AssignedSoundId)
                                 || !string.IsNullOrWhiteSpace(ImagePath)
                                 || !string.IsNullOrWhiteSpace(AssignmentName);
}
