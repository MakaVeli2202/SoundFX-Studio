using SoundFXStudio.Controls;
using SoundFXStudio.Infrastructure;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SoundFXStudio.Views.Dialogs;

public partial class KeyboardCalibrationWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _configService = new();
    private readonly AppConfig _config;
    private readonly KeyboardLayoutService _keyboardLayoutService = new();
    private readonly RelayCommand _noopCommand;
    private readonly ObservableCollection<ClusterCalibrationItem> _clusterItems = new();
    private readonly ObservableCollection<SpecialKeyOverrideItem> _specialItems = new();
    private readonly ObservableCollection<KeyCalibrationItem> _keyItems = new();

    private bool _suppressUpdates;
    private bool _suppressJsonSync;

    private double _previewKeyUnit = 43;
    private double _previewGapX = 3;
    private double _previewGapY = 3;
    private double _previewOffsetX = 65;
    private double _previewOffsetY = 72;
    private double _previewButtonScale = 1.0;
    private double _zoomLevel = 100;

    private double _previewInnerInsetXPercent = 20;
    private double _previewInnerInsetYPercent = 20;
    private double _previewInnerOffsetXPercent;
    private double _previewInnerOffsetYPercent;

    private double _previewCapsLockIndicatorOffsetX = 1235;
    private double _previewCapsLockIndicatorOffsetY = 252;
    private double _previewNumLockIndicatorOffsetX = 1297;
    private double _previewNumLockIndicatorOffsetY = 252;
    private double _previewScrollLockIndicatorOffsetX = 1359;
    private double _previewScrollLockIndicatorOffsetY = 252;

    private KeyCalibrationItem? _selectedKeyItem;
    private string _perKeyOverridesJson = "{}";
    private string _jsonEditorStatus = "Ready";

    public KeyboardCalibrationWindow()
    {
        InitializeComponent();
        DataContext = this;

        _config = _configService.Load();
        _noopCommand = new RelayCommand(SelectPreviewKey);

        BuildKeyboard();
        BuildClusterItems();
        BuildSpecialItems();
        BuildKeyItems();
        LoadFromSettings();
        ApplyAllCalibration();
        RefreshPreview();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CalibrationSaved;

    public ObservableCollection<KeyboardKey> KeyboardKeys { get; } = new();
    public ObservableCollection<ClusterCalibrationItem> ClusterItems => _clusterItems;
    public ObservableCollection<SpecialKeyOverrideItem> SpecialItems => _specialItems;
    public ObservableCollection<KeyCalibrationItem> KeyItems => _keyItems;
    public ICommand KeyClickedCommand => _noopCommand;

    public double PreviewKeyUnit
    {
        get => _previewKeyUnit;
        set => SetAndApply(ref _previewKeyUnit, value);
    }

    public double PreviewGapX
    {
        get => _previewGapX;
        set => SetAndApply(ref _previewGapX, value);
    }

    public double PreviewGapY
    {
        get => _previewGapY;
        set => SetAndApply(ref _previewGapY, value);
    }

    public double PreviewOffsetX
    {
        get => _previewOffsetX;
        set => SetAndApply(ref _previewOffsetX, value);
    }

    public double PreviewOffsetY
    {
        get => _previewOffsetY;
        set => SetAndApply(ref _previewOffsetY, value);
    }

    public double PreviewButtonScale
    {
        get => _previewButtonScale;
        set => SetAndApply(ref _previewButtonScale, value);
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            if (Math.Abs(_zoomLevel - value) < double.Epsilon)
                return;
            _zoomLevel = value;
            OnPropertyChanged();
            ApplyZoom();
        }
    }

    public double PreviewInnerInsetXPercent
    {
        get => _previewInnerInsetXPercent;
        set => SetAndApply(ref _previewInnerInsetXPercent, Math.Clamp(value, 0, 45));
    }

    public double PreviewInnerInsetYPercent
    {
        get => _previewInnerInsetYPercent;
        set => SetAndApply(ref _previewInnerInsetYPercent, Math.Clamp(value, 0, 45));
    }

    public double PreviewInnerOffsetXPercent
    {
        get => _previewInnerOffsetXPercent;
        set => SetAndApply(ref _previewInnerOffsetXPercent, Math.Clamp(value, -30, 30));
    }

    public double PreviewInnerOffsetYPercent
    {
        get => _previewInnerOffsetYPercent;
        set => SetAndApply(ref _previewInnerOffsetYPercent, Math.Clamp(value, -30, 30));
    }

    public double CapsLockIndicatorOffsetX
    {
        get => _previewCapsLockIndicatorOffsetX;
        set => SetAndApply(ref _previewCapsLockIndicatorOffsetX, value);
    }

    public double CapsLockIndicatorOffsetY
    {
        get => _previewCapsLockIndicatorOffsetY;
        set => SetAndApply(ref _previewCapsLockIndicatorOffsetY, value);
    }

    public double NumLockIndicatorOffsetX
    {
        get => _previewNumLockIndicatorOffsetX;
        set => SetAndApply(ref _previewNumLockIndicatorOffsetX, value);
    }

    public double NumLockIndicatorOffsetY
    {
        get => _previewNumLockIndicatorOffsetY;
        set => SetAndApply(ref _previewNumLockIndicatorOffsetY, value);
    }

    public double ScrollLockIndicatorOffsetX
    {
        get => _previewScrollLockIndicatorOffsetX;
        set => SetAndApply(ref _previewScrollLockIndicatorOffsetX, value);
    }

    public double ScrollLockIndicatorOffsetY
    {
        get => _previewScrollLockIndicatorOffsetY;
        set => SetAndApply(ref _previewScrollLockIndicatorOffsetY, value);
    }

    public KeyCalibrationItem? SelectedKeyItem
    {
        get => _selectedKeyItem;
        set
        {
            if (ReferenceEquals(_selectedKeyItem, value))
            {
                return;
            }

            _selectedKeyItem = value;
            UpdateSelectedKeyboardKey();
            OnPropertyChanged();
        }
    }

    public string PerKeyOverridesJson
    {
        get => _perKeyOverridesJson;
        set
        {
            if (_perKeyOverridesJson == value)
            {
                return;
            }

            _perKeyOverridesJson = value;
            OnPropertyChanged();
        }
    }

    public string JsonEditorStatus
    {
        get => _jsonEditorStatus;
        set
        {
            if (_jsonEditorStatus == value)
            {
                return;
            }

            _jsonEditorStatus = value;
            OnPropertyChanged();
        }
    }

    private void BuildKeyboard()
    {
        KeyboardKeys.Clear();
        foreach (var key in _keyboardLayoutService.CreateKeyboard(GetPreviewLayoutMode()))
        {
            KeyboardKeys.Add(key);
        }
    }

    private void BuildClusterItems()
    {
        _clusterItems.Clear();
        AddClusterItem("Esc key", KeyboardCluster.EscCluster);
        AddClusterItem("F1 to F4 keys", KeyboardCluster.F1ToF4Cluster);
        AddClusterItem("F5-F8", KeyboardCluster.F5ToF8Cluster);
        AddClusterItem("F9-F12", KeyboardCluster.F9ToF12Cluster);
        AddClusterItem("Print/Scroll/Pause", KeyboardCluster.PrintScrollPauseCluster);
        AddClusterItem("Main Typing", KeyboardCluster.MainTypingCluster);
        AddClusterItem("Navigation (Ins/Home/PgUp/PgDn/Del/End)", KeyboardCluster.NavigationCluster);
        AddClusterItem("Arrows", KeyboardCluster.ArrowCluster);
        AddClusterItem("Numpad", KeyboardCluster.NumpadCluster);
    }

    private void BuildSpecialItems()
    {
        _specialItems.Clear();
        AddSpecialItem("Spacebar", "SPACE");
        AddSpecialItem("Backspace", "BACKSPACE");
        AddSpecialItem("Enter", "ENTER");
        AddSpecialItem("ISO Enter", "OEM102");
        AddSpecialItem("Left Shift", "SHIFT-L");
        AddSpecialItem("Right Shift", "SHIFT-R");
        AddSpecialItem("Numpad Enter", "ENTER-NUMPAD");
        AddSpecialItem("Tab", "TAB");
        AddSpecialItem("Caps Lock", "CAPS LOCK");
    }

    private KeyboardLayoutMode GetPreviewLayoutMode()
    {
        var layoutMode = _config.Settings.KeyboardLayout;
        if (layoutMode != KeyboardLayoutMode.Automatic)
        {
            return layoutMode;
        }

        var language = InputLanguageManager.Current.CurrentInputLanguage?.Name;
        return language switch
        {
            "de-DE" => KeyboardLayoutMode.German,
            "en-GB" => KeyboardLayoutMode.EnglishUK,
            _ => KeyboardLayoutMode.EnglishUS
        };
    }

    private void BuildKeyItems()
    {
        _keyItems.Clear();
        foreach (var key in KeyboardKeys.OrderBy(item => item.RowIndex).ThenBy(item => item.ColumnIndex))
        {
            var label = string.IsNullOrWhiteSpace(key.DisplayLabel) ? key.KeyName : key.DisplayLabel;
            var item = new KeyCalibrationItem(key.Id, $"{label} ({key.Id})");
            item.Changed += OnKeyItemChanged;
            _keyItems.Add(item);
        }
    }

    private void LoadFromSettings()
    {
        var settings = _config.Settings.KeyboardCalibration ?? new KeyboardCalibrationSettings();

        LoadFromCalibration(settings);
    }

    private void LoadFromCalibration(KeyboardCalibrationSettings settings)
    {

        _suppressUpdates = true;
        try
        {
            _previewKeyUnit = settings.KeyUnit;
            _previewGapX = Math.Abs(settings.GapX) > double.Epsilon ? settings.GapX : settings.Gap;
            _previewGapY = Math.Abs(settings.GapY) > double.Epsilon ? settings.GapY : settings.Gap;
            _previewOffsetX = settings.OffsetX;
            _previewOffsetY = settings.OffsetY;
            _previewButtonScale = settings.ButtonScale;

            _previewInnerInsetXPercent = Math.Abs(settings.InnerSectionInsetXPercent) > double.Epsilon ? settings.InnerSectionInsetXPercent : settings.InnerSectionInsetPercent;
            _previewInnerInsetYPercent = Math.Abs(settings.InnerSectionInsetYPercent) > double.Epsilon ? settings.InnerSectionInsetYPercent : settings.InnerSectionInsetPercent;
            _previewInnerOffsetXPercent = settings.InnerSectionOffsetXPercent;
            _previewInnerOffsetYPercent = settings.InnerSectionOffsetYPercent;
            _previewCapsLockIndicatorOffsetX = settings.CapsLockIndicatorOffsetX;
            _previewCapsLockIndicatorOffsetY = NormalizeLampY(settings.CapsLockIndicatorOffsetY);
            _previewNumLockIndicatorOffsetX = settings.NumLockIndicatorOffsetX;
            _previewNumLockIndicatorOffsetY = NormalizeLampY(settings.NumLockIndicatorOffsetY);
            _previewScrollLockIndicatorOffsetX = settings.ScrollLockIndicatorOffsetX;
            _previewScrollLockIndicatorOffsetY = NormalizeLampY(settings.ScrollLockIndicatorOffsetY);

            GetClusterItem(KeyboardCluster.EscCluster).OffsetX = settings.EscOffsetX;
            GetClusterItem(KeyboardCluster.EscCluster).OffsetY = settings.EscOffsetY;
            GetClusterItem(KeyboardCluster.F1ToF4Cluster).OffsetX = settings.F1ToF4OffsetX;
            GetClusterItem(KeyboardCluster.F1ToF4Cluster).OffsetY = settings.F1ToF4OffsetY;
            GetClusterItem(KeyboardCluster.F5ToF8Cluster).OffsetX = settings.F5ToF8OffsetX;
            GetClusterItem(KeyboardCluster.F5ToF8Cluster).OffsetY = settings.F5ToF8OffsetY;
            GetClusterItem(KeyboardCluster.F9ToF12Cluster).OffsetX = settings.F9ToF12OffsetX;
            GetClusterItem(KeyboardCluster.F9ToF12Cluster).OffsetY = settings.F9ToF12OffsetY;
            GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).OffsetX = settings.PrintScrollPauseOffsetX;
            GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).OffsetY = settings.PrintScrollPauseOffsetY;
            GetClusterItem(KeyboardCluster.MainTypingCluster).OffsetX = settings.MainTypingOffsetX;
            GetClusterItem(KeyboardCluster.MainTypingCluster).OffsetY = settings.MainTypingOffsetY;
            GetClusterItem(KeyboardCluster.NavigationCluster).OffsetX = settings.NavigationOffsetX;
            GetClusterItem(KeyboardCluster.NavigationCluster).OffsetY = settings.NavigationOffsetY;
            GetClusterItem(KeyboardCluster.ArrowCluster).OffsetX = settings.ArrowOffsetX;
            GetClusterItem(KeyboardCluster.ArrowCluster).OffsetY = settings.ArrowOffsetY;
            GetClusterItem(KeyboardCluster.NumpadCluster).OffsetX = settings.NumpadOffsetX;
            GetClusterItem(KeyboardCluster.NumpadCluster).OffsetY = settings.NumpadOffsetY;

            SetSpecialValue("SPACE", settings.SpacebarWidthAdjustment);
            SetSpecialValue("BACKSPACE", settings.BackspaceWidthAdjustment);
            SetSpecialValue("ENTER", settings.EnterWidthAdjustment);
            SetSpecialValue("OEM102", settings.IsoEnterWidthAdjustment);
            SetSpecialValue("SHIFT-L", settings.LeftShiftWidthAdjustment);
            SetSpecialValue("SHIFT-R", settings.RightShiftWidthAdjustment);
            SetSpecialValue("ENTER-NUMPAD", settings.NumpadEnterWidthAdjustment);
            SetSpecialValue("TAB", settings.TabWidthAdjustment);
            SetSpecialValue("CAPS LOCK", settings.CapsLockWidthAdjustment);

            foreach (var entry in settings.KeyOverrides)
            {
                var item = _keyItems.FirstOrDefault(i => string.Equals(i.KeyId, entry.Key, StringComparison.OrdinalIgnoreCase));
                if (item is null)
                {
                    continue;
                }

                var value = entry.Value;
                item.OffsetX = value.OffsetX;
                item.OffsetY = value.OffsetY;
                item.WidthAdjustment = value.WidthAdjustment;
                item.HeightAdjustment = value.HeightAdjustment;
                item.InnerInsetAdjustmentPercent = value.InnerInsetAdjustmentPercent;
                item.InnerInsetXAdjustmentPercent = value.InnerInsetXAdjustmentPercent;
                item.InnerInsetYAdjustmentPercent = value.InnerInsetYAdjustmentPercent;
                item.InnerOffsetXAdjustmentPercent = value.InnerOffsetXAdjustmentPercent;
                item.InnerOffsetYAdjustmentPercent = value.InnerOffsetYAdjustmentPercent;
            }
        }
        finally
        {
            _suppressUpdates = false;
        }

        OnPropertyChanged(nameof(PreviewKeyUnit));
        OnPropertyChanged(nameof(PreviewGapX));
        OnPropertyChanged(nameof(PreviewGapY));
        OnPropertyChanged(nameof(PreviewOffsetX));
        OnPropertyChanged(nameof(PreviewOffsetY));
        OnPropertyChanged(nameof(PreviewButtonScale));
        OnPropertyChanged(nameof(PreviewInnerInsetXPercent));
        OnPropertyChanged(nameof(PreviewInnerInsetYPercent));
        OnPropertyChanged(nameof(PreviewInnerOffsetXPercent));
        OnPropertyChanged(nameof(PreviewInnerOffsetYPercent));
        OnPropertyChanged(nameof(CapsLockIndicatorOffsetX));
        OnPropertyChanged(nameof(CapsLockIndicatorOffsetY));
        OnPropertyChanged(nameof(NumLockIndicatorOffsetX));
        OnPropertyChanged(nameof(NumLockIndicatorOffsetY));
        OnPropertyChanged(nameof(ScrollLockIndicatorOffsetX));
        OnPropertyChanged(nameof(ScrollLockIndicatorOffsetY));

        RefreshPerKeyOverridesJsonFromItems();
    }

    private void AddClusterItem(string name, KeyboardCluster cluster)
    {
        var item = new ClusterCalibrationItem(name, cluster);
        item.Changed += OnClusterItemChanged;
        _clusterItems.Add(item);
    }

    private void AddSpecialItem(string name, string keyId)
    {
        var item = new SpecialKeyOverrideItem(name, keyId);
        item.Changed += OnSpecialItemChanged;
        _specialItems.Add(item);
    }

    private void SetAndApply(ref double field, double value, [CallerMemberName] string? propertyName = null)
    {
        if (Math.Abs(field - value) < double.Epsilon)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        ApplyAllCalibration();
        RefreshPreview();
        PersistCalibrationLive();
    }

    private void OnKeyItemChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        ApplyPerKeyOverrides();
        RefreshPreview();

        if (!_suppressJsonSync)
        {
            RefreshPerKeyOverridesJsonFromItems();
        }

        PersistCalibrationLive();
    }

    private void ApplyAllCalibration()
    {
        KeyboardLayoutPanel.SetLayoutCalibration(PreviewKeyUnit, PreviewGapX, PreviewGapY, PreviewOffsetX, PreviewOffsetY);
        KeyboardLayoutPanel.ButtonScale = PreviewButtonScale;
        ApplyClusterCalibration();
        ApplySpecialOverrides();
        ApplyPerKeyOverrides();
    }

    private void ApplyClusterCalibration()
    {
        KeyboardClusterLayout.ApplyPreset(
            GetClusterItem(KeyboardCluster.EscCluster).OffsetX,
            GetClusterItem(KeyboardCluster.EscCluster).OffsetY,
            GetClusterItem(KeyboardCluster.F1ToF4Cluster).OffsetX,
            GetClusterItem(KeyboardCluster.F1ToF4Cluster).OffsetY,
            GetClusterItem(KeyboardCluster.F5ToF8Cluster).OffsetX,
            GetClusterItem(KeyboardCluster.F5ToF8Cluster).OffsetY,
            GetClusterItem(KeyboardCluster.F9ToF12Cluster).OffsetX,
            GetClusterItem(KeyboardCluster.F9ToF12Cluster).OffsetY,
            GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).OffsetX,
            GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).OffsetY,
            GetClusterItem(KeyboardCluster.MainTypingCluster).OffsetX,
            GetClusterItem(KeyboardCluster.MainTypingCluster).OffsetY,
            GetClusterItem(KeyboardCluster.NavigationCluster).OffsetX,
            GetClusterItem(KeyboardCluster.NavigationCluster).OffsetY,
            GetClusterItem(KeyboardCluster.ArrowCluster).OffsetX,
            GetClusterItem(KeyboardCluster.ArrowCluster).OffsetY,
            GetClusterItem(KeyboardCluster.NumpadCluster).OffsetX,
            GetClusterItem(KeyboardCluster.NumpadCluster).OffsetY);
    }

    private void ApplySpecialOverrides()
    {
        KeyboardLayoutPanel.ClearAllSpecialKeyOverrides();

        foreach (var item in _specialItems)
        {
            KeyboardLayoutPanel.SetSpecialKeyOverride(item.KeyId, item.WidthAdjustment);
        }
    }

    private void ApplyPerKeyOverrides()
    {
        KeyboardLayoutPanel.ClearAllPerKeyOverrides();

        foreach (var key in KeyboardKeys)
        {
            key.InnerInsetAdjustmentPercent = 0;
            key.InnerInsetXAdjustmentPercent = 0;
            key.InnerInsetYAdjustmentPercent = 0;
            key.InnerOffsetXAdjustmentPercent = 0;
            key.InnerOffsetYAdjustmentPercent = 0;
        }

        foreach (var item in _keyItems.Where(item => !item.IsZero()))
        {
            KeyboardLayoutPanel.SetPerKeyOverride(item.KeyId, item.OffsetX, item.OffsetY, item.WidthAdjustment, item.HeightAdjustment);

            var key = KeyboardKeys.FirstOrDefault(entry => string.Equals(entry.Id, item.KeyId, StringComparison.OrdinalIgnoreCase));
            if (key is null)
            {
                continue;
            }

            key.InnerInsetAdjustmentPercent = item.InnerInsetAdjustmentPercent;
            key.InnerInsetXAdjustmentPercent = item.InnerInsetXAdjustmentPercent;
            key.InnerInsetYAdjustmentPercent = item.InnerInsetYAdjustmentPercent;
            key.InnerOffsetXAdjustmentPercent = item.InnerOffsetXAdjustmentPercent;
            key.InnerOffsetYAdjustmentPercent = item.InnerOffsetYAdjustmentPercent;
        }
    }

    private void OnClusterItemChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        ApplyClusterCalibration();
        RefreshPreview();
        PersistCalibrationLive();
    }

    private void OnSpecialItemChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        ApplySpecialOverrides();
        RefreshPreview();
        PersistCalibrationLive();
    }

    private void RefreshPreview()
    {
        PreviewKeyboard.InvalidateMeasure();
        PreviewKeyboard.InvalidateArrange();
        PreviewKeyboard.UpdateLayout();
    }

    private void SelectPreviewKey(object? parameter)
    {
        if (parameter is not KeyboardKey key)
        {
            return;
        }

        SelectedKeyItem = _keyItems.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateSelectedKeyboardKey()
    {
        foreach (var key in KeyboardKeys)
        {
            key.IsSelected = false;
        }

        if (_selectedKeyItem is null)
        {
            return;
        }

        var selectedKeyboardKey = KeyboardKeys.FirstOrDefault(item => string.Equals(item.Id, _selectedKeyItem.KeyId, StringComparison.OrdinalIgnoreCase));
        if (selectedKeyboardKey is not null)
        {
            selectedKeyboardKey.IsSelected = true;
        }
    }

    private void SaveCalibration(bool notifyMainViewModel)
    {
        var calibration = _config.Settings.KeyboardCalibration ?? new KeyboardCalibrationSettings();
        calibration.KeyUnit = PreviewKeyUnit;
        calibration.GapX = PreviewGapX;
        calibration.GapY = PreviewGapY;
        calibration.Gap = (PreviewGapX + PreviewGapY) / 2d;
        calibration.OffsetX = PreviewOffsetX;
        calibration.OffsetY = PreviewOffsetY;
        calibration.ButtonScale = PreviewButtonScale;
        calibration.InnerSectionInsetXPercent = PreviewInnerInsetXPercent;
        calibration.InnerSectionInsetYPercent = PreviewInnerInsetYPercent;
        calibration.InnerSectionInsetPercent = (PreviewInnerInsetXPercent + PreviewInnerInsetYPercent) / 2d;
        calibration.InnerSectionOffsetXPercent = PreviewInnerOffsetXPercent;
        calibration.InnerSectionOffsetYPercent = PreviewInnerOffsetYPercent;
        calibration.CapsLockIndicatorOffsetX = CapsLockIndicatorOffsetX;
        calibration.CapsLockIndicatorOffsetY = CapsLockIndicatorOffsetY;
        calibration.NumLockIndicatorOffsetX = NumLockIndicatorOffsetX;
        calibration.NumLockIndicatorOffsetY = NumLockIndicatorOffsetY;
        calibration.ScrollLockIndicatorOffsetX = ScrollLockIndicatorOffsetX;
        calibration.ScrollLockIndicatorOffsetY = ScrollLockIndicatorOffsetY;

        calibration.EscOffsetX = GetClusterItem(KeyboardCluster.EscCluster).OffsetX;
        calibration.EscOffsetY = GetClusterItem(KeyboardCluster.EscCluster).OffsetY;
        calibration.F1ToF4OffsetX = GetClusterItem(KeyboardCluster.F1ToF4Cluster).OffsetX;
        calibration.F1ToF4OffsetY = GetClusterItem(KeyboardCluster.F1ToF4Cluster).OffsetY;
        calibration.F5ToF8OffsetX = GetClusterItem(KeyboardCluster.F5ToF8Cluster).OffsetX;
        calibration.F5ToF8OffsetY = GetClusterItem(KeyboardCluster.F5ToF8Cluster).OffsetY;
        calibration.F9ToF12OffsetX = GetClusterItem(KeyboardCluster.F9ToF12Cluster).OffsetX;
        calibration.F9ToF12OffsetY = GetClusterItem(KeyboardCluster.F9ToF12Cluster).OffsetY;
        calibration.PrintScrollPauseOffsetX = GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).OffsetX;
        calibration.PrintScrollPauseOffsetY = GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).OffsetY;
        calibration.MainTypingOffsetX = GetClusterItem(KeyboardCluster.MainTypingCluster).OffsetX;
        calibration.MainTypingOffsetY = GetClusterItem(KeyboardCluster.MainTypingCluster).OffsetY;
        calibration.NavigationOffsetX = GetClusterItem(KeyboardCluster.NavigationCluster).OffsetX;
        calibration.NavigationOffsetY = GetClusterItem(KeyboardCluster.NavigationCluster).OffsetY;
        calibration.ArrowOffsetX = GetClusterItem(KeyboardCluster.ArrowCluster).OffsetX;
        calibration.ArrowOffsetY = GetClusterItem(KeyboardCluster.ArrowCluster).OffsetY;
        calibration.NumpadOffsetX = GetClusterItem(KeyboardCluster.NumpadCluster).OffsetX;
        calibration.NumpadOffsetY = GetClusterItem(KeyboardCluster.NumpadCluster).OffsetY;

        calibration.SpacebarWidthAdjustment = GetSpecialValue("SPACE");
        calibration.BackspaceWidthAdjustment = GetSpecialValue("BACKSPACE");
        calibration.EnterWidthAdjustment = GetSpecialValue("ENTER");
        calibration.IsoEnterWidthAdjustment = GetSpecialValue("OEM102");
        calibration.LeftShiftWidthAdjustment = GetSpecialValue("SHIFT-L");
        calibration.RightShiftWidthAdjustment = GetSpecialValue("SHIFT-R");
        calibration.NumpadEnterWidthAdjustment = GetSpecialValue("ENTER-NUMPAD");
        calibration.TabWidthAdjustment = GetSpecialValue("TAB");
        calibration.CapsLockWidthAdjustment = GetSpecialValue("CAPS LOCK");

        calibration.KeyOverrides = _keyItems
            .Where(item => !item.IsZero())
            .ToDictionary(
                item => item.KeyId,
                item => new KeyCalibrationOverrideSettings
                {
                    OffsetX = item.OffsetX,
                    OffsetY = item.OffsetY,
                    WidthAdjustment = item.WidthAdjustment,
                    HeightAdjustment = item.HeightAdjustment,
                    InnerInsetAdjustmentPercent = item.InnerInsetAdjustmentPercent,
                    InnerInsetXAdjustmentPercent = item.InnerInsetXAdjustmentPercent,
                    InnerInsetYAdjustmentPercent = item.InnerInsetYAdjustmentPercent,
                    InnerOffsetXAdjustmentPercent = item.InnerOffsetXAdjustmentPercent,
                    InnerOffsetYAdjustmentPercent = item.InnerOffsetYAdjustmentPercent
                },
                StringComparer.OrdinalIgnoreCase);

        _config.Settings.KeyboardCalibration = calibration;
        _configService.Save(_config);

        if (notifyMainViewModel)
        {
            CalibrationSaved?.Invoke(this, EventArgs.Empty);
        }
    }

    private void PersistCalibrationLive()
    {
        if (_suppressUpdates)
        {
            return;
        }

        SaveCalibration(notifyMainViewModel: false);
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        _suppressUpdates = true;
        try
        {
            _previewKeyUnit = 43;
            _previewGapX = 3;
            _previewGapY = 3;
            _previewOffsetX = 65;
            _previewOffsetY = 72;
            _previewButtonScale = 1.0;
            _previewInnerInsetXPercent = 20;
            _previewInnerInsetYPercent = 20;
            _previewInnerOffsetXPercent = 0;
            _previewInnerOffsetYPercent = 0;
            _previewCapsLockIndicatorOffsetX = 1235;
            _previewCapsLockIndicatorOffsetY = 252;
            _previewNumLockIndicatorOffsetX = 1297;
            _previewNumLockIndicatorOffsetY = 252;
            _previewScrollLockIndicatorOffsetX = 1359;
            _previewScrollLockIndicatorOffsetY = 252;

            foreach (var cluster in _clusterItems)
            {
                cluster.Reset();
            }

            foreach (var special in _specialItems)
            {
                special.Reset();
            }

            foreach (var key in _keyItems)
            {
                key.Reset();
            }
        }
        finally
        {
            _suppressUpdates = false;
        }

        OnPropertyChanged(nameof(PreviewKeyUnit));
        OnPropertyChanged(nameof(PreviewGapX));
        OnPropertyChanged(nameof(PreviewGapY));
        OnPropertyChanged(nameof(PreviewOffsetX));
        OnPropertyChanged(nameof(PreviewOffsetY));
        OnPropertyChanged(nameof(PreviewButtonScale));
        OnPropertyChanged(nameof(PreviewInnerInsetXPercent));
        OnPropertyChanged(nameof(PreviewInnerInsetYPercent));
        OnPropertyChanged(nameof(PreviewInnerOffsetXPercent));
        OnPropertyChanged(nameof(PreviewInnerOffsetYPercent));
        OnPropertyChanged(nameof(CapsLockIndicatorOffsetX));
        OnPropertyChanged(nameof(CapsLockIndicatorOffsetY));
        OnPropertyChanged(nameof(NumLockIndicatorOffsetX));
        OnPropertyChanged(nameof(NumLockIndicatorOffsetY));
        OnPropertyChanged(nameof(ScrollLockIndicatorOffsetX));
        OnPropertyChanged(nameof(ScrollLockIndicatorOffsetY));

        ApplyAllCalibration();
        RefreshPreview();
        PersistCalibrationLive();
    }

    private void SavePermanently_Click(object sender, RoutedEventArgs e)
    {
        SaveCalibration(notifyMainViewModel: true);
        JsonEditorStatus = "Saved";
    }

    private void ResetSelectedKey_Click(object sender, RoutedEventArgs e)
    {
        SelectedKeyItem?.Reset();
    }

    private void ResetSelectedCluster_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ClusterCalibrationItem item)
        {
            return;
        }

        item.Reset();
    }

    private void ResetSelectedSpecial_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not SpecialKeyOverrideItem item)
        {
            return;
        }

        item.Reset();
    }

    private void ApplyPerKeyJson_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParsePerKeyOverridesJson(out var parsed, out var error))
        {
            JsonEditorStatus = error;
            return;
        }

        _suppressUpdates = true;
        _suppressJsonSync = true;
        try
        {
            foreach (var item in _keyItems)
            {
                item.Reset();
            }

            foreach (var entry in parsed)
            {
                var item = _keyItems.FirstOrDefault(i => string.Equals(i.KeyId, entry.Key, StringComparison.OrdinalIgnoreCase));
                if (item is null)
                {
                    continue;
                }

                var value = entry.Value;
                item.OffsetX = value.OffsetX;
                item.OffsetY = value.OffsetY;
                item.WidthAdjustment = value.WidthAdjustment;
                item.HeightAdjustment = value.HeightAdjustment;
                item.InnerInsetAdjustmentPercent = value.InnerInsetAdjustmentPercent;
                item.InnerInsetXAdjustmentPercent = value.InnerInsetXAdjustmentPercent;
                item.InnerInsetYAdjustmentPercent = value.InnerInsetYAdjustmentPercent;
                item.InnerOffsetXAdjustmentPercent = value.InnerOffsetXAdjustmentPercent;
                item.InnerOffsetYAdjustmentPercent = value.InnerOffsetYAdjustmentPercent;
            }
        }
        finally
        {
            _suppressJsonSync = false;
            _suppressUpdates = false;
        }

        ApplyAllCalibration();
        RefreshPreview();
        RefreshPerKeyOverridesJsonFromItems();
        JsonEditorStatus = "Applied";
    }

    private void RevertPerKeyJson_Click(object sender, RoutedEventArgs e)
    {
        RefreshPerKeyOverridesJsonFromItems();
        JsonEditorStatus = "Reverted";
    }

    private bool TryParsePerKeyOverridesJson(out Dictionary<string, KeyCalibrationOverrideSettings> parsed, out string error)
    {
        parsed = new Dictionary<string, KeyCalibrationOverrideSettings>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        var text = PerKeyOverridesJson?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<Dictionary<string, KeyCalibrationOverrideSettings>>(text, options)
                         ?? new Dictionary<string, KeyCalibrationOverrideSettings>(StringComparer.OrdinalIgnoreCase);

            var keySet = _keyItems.Select(item => item.KeyId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in result)
            {
                if (!keySet.Contains(entry.Key))
                {
                    error = $"Unknown key id: {entry.Key}";
                    return false;
                }

                parsed[entry.Key] = entry.Value ?? new KeyCalibrationOverrideSettings();
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }

    private void RefreshPerKeyOverridesJsonFromItems()
    {
        var map = _keyItems
            .Where(item => !item.IsZero())
            .ToDictionary(
                item => item.KeyId,
                item => new KeyCalibrationOverrideSettings
                {
                    OffsetX = item.OffsetX,
                    OffsetY = item.OffsetY,
                    WidthAdjustment = item.WidthAdjustment,
                    HeightAdjustment = item.HeightAdjustment,
                    InnerInsetAdjustmentPercent = item.InnerInsetAdjustmentPercent,
                    InnerInsetXAdjustmentPercent = item.InnerInsetXAdjustmentPercent,
                    InnerInsetYAdjustmentPercent = item.InnerInsetYAdjustmentPercent,
                    InnerOffsetXAdjustmentPercent = item.InnerOffsetXAdjustmentPercent,
                    InnerOffsetYAdjustmentPercent = item.InnerOffsetYAdjustmentPercent
                },
                StringComparer.OrdinalIgnoreCase);

        PerKeyOverridesJson = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
    }

    private ClusterCalibrationItem GetClusterItem(KeyboardCluster cluster)
        => _clusterItems.First(item => item.Cluster == cluster);

    private void SetSpecialValue(string keyId, double value)
    {
        var item = _specialItems.FirstOrDefault(entry => string.Equals(entry.KeyId, keyId, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            item.WidthAdjustment = value;
        }
    }

    private double GetSpecialValue(string keyId)
        => _specialItems.FirstOrDefault(entry => string.Equals(entry.KeyId, keyId, StringComparison.OrdinalIgnoreCase))?.WidthAdjustment ?? 0;

    private void NudgeValue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var parts = tag.Split(':', 2);
        if (parts.Length != 2 || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var delta))
        {
            return;
        }

        var propertyName = parts[0].Trim();
        var target = (sender as FrameworkElement)?.DataContext ?? this;
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if ((property is null || property.PropertyType != typeof(double) || !property.CanRead || !property.CanWrite) && !ReferenceEquals(target, this))
        {
            target = this;
            property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        }

        if (property is null || property.PropertyType != typeof(double) || !property.CanRead || !property.CanWrite)
        {
            return;
        }

        if (property.GetValue(target) is not double current)
        {
            return;
        }

        property.SetValue(target, current + delta);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Shapes.Path or System.Windows.Controls.Canvas)
            return;
        if (e.ClickCount == 2)
            return;
        DragMove();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomLevel = Math.Min(300, ZoomLevel + 10);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomLevel = Math.Max(10, ZoomLevel - 10);

    private void ApplyZoom()
    {
        if (PreviewViewbox is null)
            return;
        PreviewViewbox.LayoutTransform = new System.Windows.Media.ScaleTransform(ZoomLevel / 100.0, ZoomLevel / 100.0);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCalibration(notifyMainViewModel: true);
        Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static double NormalizeLampY(double value)
        => value < 220 ? value + 70 : value;

    public sealed class ClusterCalibrationItem : INotifyPropertyChanged
    {
        private double _offsetX;
        private double _offsetY;

        public ClusterCalibrationItem(string name, KeyboardCluster cluster)
        {
            Name = name;
            Cluster = cluster;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? Changed;

        public string Name { get; }
        public KeyboardCluster Cluster { get; }

        public double OffsetX { get => _offsetX; set => Set(ref _offsetX, value); }
        public double OffsetY { get => _offsetY; set => Set(ref _offsetY, value); }

        public void Reset()
        {
            _offsetX = 0;
            _offsetY = 0;
            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            Changed?.Invoke();
        }

        private void Set(ref double field, double value, [CallerMemberName] string? propertyName = null)
        {
            if (Math.Abs(field - value) < double.Epsilon)
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
            Changed?.Invoke();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class SpecialKeyOverrideItem : INotifyPropertyChanged
    {
        private double _widthAdjustment;

        public SpecialKeyOverrideItem(string name, string keyId)
        {
            Name = name;
            KeyId = keyId;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? Changed;

        public string Name { get; }
        public string KeyId { get; }

        public double WidthAdjustment { get => _widthAdjustment; set => Set(ref _widthAdjustment, value); }

        public void Reset()
        {
            _widthAdjustment = 0;
            OnPropertyChanged(nameof(WidthAdjustment));
            Changed?.Invoke();
        }

        private void Set(ref double field, double value, [CallerMemberName] string? propertyName = null)
        {
            if (Math.Abs(field - value) < double.Epsilon)
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
            Changed?.Invoke();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class KeyCalibrationItem : INotifyPropertyChanged
    {
        private double _offsetX;
        private double _offsetY;
        private double _widthAdjustment;
        private double _heightAdjustment;
        private double _innerInsetAdjustmentPercent;
        private double _innerInsetXAdjustmentPercent;
        private double _innerInsetYAdjustmentPercent;
        private double _innerOffsetXAdjustmentPercent;
        private double _innerOffsetYAdjustmentPercent;

        public KeyCalibrationItem(string keyId, string name)
        {
            KeyId = keyId;
            Name = name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? Changed;

        public string KeyId { get; }
        public string Name { get; }

        public double OffsetX { get => _offsetX; set => Set(ref _offsetX, value); }
        public double OffsetY { get => _offsetY; set => Set(ref _offsetY, value); }
        public double WidthAdjustment { get => _widthAdjustment; set => Set(ref _widthAdjustment, value); }
        public double HeightAdjustment { get => _heightAdjustment; set => Set(ref _heightAdjustment, value); }
        public double InnerInsetAdjustmentPercent { get => _innerInsetAdjustmentPercent; set => Set(ref _innerInsetAdjustmentPercent, value); }
        public double InnerInsetXAdjustmentPercent { get => _innerInsetXAdjustmentPercent; set => Set(ref _innerInsetXAdjustmentPercent, value); }
        public double InnerInsetYAdjustmentPercent { get => _innerInsetYAdjustmentPercent; set => Set(ref _innerInsetYAdjustmentPercent, value); }
        public double InnerOffsetXAdjustmentPercent { get => _innerOffsetXAdjustmentPercent; set => Set(ref _innerOffsetXAdjustmentPercent, value); }
        public double InnerOffsetYAdjustmentPercent { get => _innerOffsetYAdjustmentPercent; set => Set(ref _innerOffsetYAdjustmentPercent, value); }

        public void Reset()
        {
            _offsetX = 0;
            _offsetY = 0;
            _widthAdjustment = 0;
            _heightAdjustment = 0;
            _innerInsetAdjustmentPercent = 0;
            _innerInsetXAdjustmentPercent = 0;
            _innerInsetYAdjustmentPercent = 0;
            _innerOffsetXAdjustmentPercent = 0;
            _innerOffsetYAdjustmentPercent = 0;

            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            OnPropertyChanged(nameof(WidthAdjustment));
            OnPropertyChanged(nameof(HeightAdjustment));
            OnPropertyChanged(nameof(InnerInsetAdjustmentPercent));
            OnPropertyChanged(nameof(InnerInsetXAdjustmentPercent));
            OnPropertyChanged(nameof(InnerInsetYAdjustmentPercent));
            OnPropertyChanged(nameof(InnerOffsetXAdjustmentPercent));
            OnPropertyChanged(nameof(InnerOffsetYAdjustmentPercent));
            Changed?.Invoke();
        }

        public bool IsZero()
        {
            return Math.Abs(OffsetX) < double.Epsilon
                && Math.Abs(OffsetY) < double.Epsilon
                && Math.Abs(WidthAdjustment) < double.Epsilon
                && Math.Abs(HeightAdjustment) < double.Epsilon
                && Math.Abs(InnerInsetAdjustmentPercent) < double.Epsilon
                && Math.Abs(InnerInsetXAdjustmentPercent) < double.Epsilon
                && Math.Abs(InnerInsetYAdjustmentPercent) < double.Epsilon
                && Math.Abs(InnerOffsetXAdjustmentPercent) < double.Epsilon
                && Math.Abs(InnerOffsetYAdjustmentPercent) < double.Epsilon;
        }

        private void Set(ref double field, double value, [CallerMemberName] string? propertyName = null)
        {
            if (Math.Abs(field - value) < double.Epsilon)
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
            Changed?.Invoke();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
