using SoundFXStudio.Controls;
using SoundFXStudio.Infrastructure;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
    private double _previewKeyUnit = 43;
    private double _previewGap = 3;
    private double _previewOffsetX = 65;
    private double _previewOffsetY = 72;
    private double _previewButtonScale = 1.0;
    private bool _debugCalibration;
    private KeyCalibrationItem? _selectedKeyItem;
    private string _perKeyOverridesJson = "{}";
    private string _jsonEditorStatus = "Ready";
    private bool _suppressJsonSync;

    public KeyboardCalibrationWindow()
    {
        InitializeComponent();
        DataContext = this;

        _config = _configService.Load();

        _noopCommand = new RelayCommand(_ => { });
        KeyboardLayoutPanel.DebugKeyboardCalibration = false;

        BuildKeyboard();
        BuildClusterItems();
        BuildSpecialItems();
        BuildKeyItems();
        LoadFromSettings();
        ApplyAllCalibration();
        RefreshPreview();

        Closed += KeyboardCalibrationWindow_Closed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CalibrationSaved;

    public ObservableCollection<KeyboardKey> KeyboardKeys { get; } = new();
    public ObservableCollection<ClusterCalibrationItem> ClusterItems => _clusterItems;
    public ObservableCollection<SpecialKeyOverrideItem> SpecialItems => _specialItems;
    public ObservableCollection<KeyCalibrationItem> KeyItems => _keyItems;

    public ICommand KeyClickedCommand => _noopCommand;

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

    public double PreviewKeyUnit
    {
        get => _previewKeyUnit;
        set
        {
            if (Math.Abs(_previewKeyUnit - value) < double.Epsilon)
            {
                return;
            }

            _previewKeyUnit = value;
            OnPropertyChanged();
            ApplyAllCalibration();
            RefreshPreview();
            PersistCalibrationLive();
        }
    }

    public double PreviewGap
    {
        get => _previewGap;
        set
        {
            if (Math.Abs(_previewGap - value) < double.Epsilon)
            {
                return;
            }

            _previewGap = value;
            OnPropertyChanged();
            ApplyAllCalibration();
            RefreshPreview();
            PersistCalibrationLive();
        }
    }

    public double PreviewOffsetX
    {
        get => _previewOffsetX;
        set
        {
            if (Math.Abs(_previewOffsetX - value) < double.Epsilon)
            {
                return;
            }

            _previewOffsetX = value;
            OnPropertyChanged();
            ApplyAllCalibration();
            RefreshPreview();
            PersistCalibrationLive();
        }
    }

    public double PreviewOffsetY
    {
        get => _previewOffsetY;
        set
        {
            if (Math.Abs(_previewOffsetY - value) < double.Epsilon)
            {
                return;
            }

            _previewOffsetY = value;
            OnPropertyChanged();
            ApplyAllCalibration();
            RefreshPreview();
            PersistCalibrationLive();
        }
    }

    public double PreviewButtonScale
    {
        get => _previewButtonScale;
        set
        {
            if (Math.Abs(_previewButtonScale - value) < double.Epsilon)
            {
                return;
            }

            _previewButtonScale = value;
            OnPropertyChanged();
            ApplyAllCalibration();
            RefreshPreview();
            PersistCalibrationLive();
        }
    }

    public bool DebugCalibration
    {
        get => _debugCalibration;
        set
        {
            if (_debugCalibration == value)
            {
                return;
            }

            _debugCalibration = value;
            KeyboardLayoutPanel.DebugKeyboardCalibration = value;
            OnPropertyChanged();
            RefreshPreview();
            PersistCalibrationLive();
        }
    }

    private void BuildKeyboard()
    {
        KeyboardKeys.Clear();
        foreach (var key in _keyboardLayoutService.CreateKeyboard(KeyboardLayoutMode.English))
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

        SelectedKeyItem = _keyItems.FirstOrDefault();
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
            _previewGap = settings.Gap;
            _previewOffsetX = settings.OffsetX;
            _previewOffsetY = settings.OffsetY;
            _previewButtonScale = settings.ButtonScale;
            _debugCalibration = settings.DebugCalibration;

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
                SetKeyOverrideValue(entry.Key, entry.Value);
            }
        }
        finally
        {
            _suppressUpdates = false;
        }

        OnPropertyChanged(nameof(PreviewKeyUnit));
        OnPropertyChanged(nameof(PreviewGap));
        OnPropertyChanged(nameof(PreviewOffsetX));
        OnPropertyChanged(nameof(PreviewOffsetY));
        OnPropertyChanged(nameof(PreviewButtonScale));
        OnPropertyChanged(nameof(DebugCalibration));
        RefreshPerKeyOverridesJsonFromItems();
    }

    private void KeyboardCalibrationWindow_Closed(object? sender, EventArgs e)
    {
        SaveCalibration();
    }

    private void SaveCalibration(bool notifyMainViewModel = true)
    {
        var calibration = _config.Settings.KeyboardCalibration ?? new KeyboardCalibrationSettings();

        calibration.KeyUnit = PreviewKeyUnit;
        calibration.Gap = PreviewGap;
        calibration.OffsetX = PreviewOffsetX;
        calibration.OffsetY = PreviewOffsetY;
        calibration.ButtonScale = PreviewButtonScale;
        calibration.DebugCalibration = DebugCalibration;

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
                    HeightAdjustment = item.HeightAdjustment
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

    private void ApplySimplePreset(double keyUnit, double gap, double offsetX, double offsetY, double buttonScale, bool resetDetails)
    {
        _suppressUpdates = true;
        try
        {
            _previewKeyUnit = keyUnit;
            _previewGap = gap;
            _previewOffsetX = offsetX;
            _previewOffsetY = offsetY;
            _previewButtonScale = buttonScale;

            if (resetDetails)
            {
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
        }
        finally
        {
            _suppressUpdates = false;
        }

        OnPropertyChanged(nameof(PreviewKeyUnit));
        OnPropertyChanged(nameof(PreviewGap));
        OnPropertyChanged(nameof(PreviewOffsetX));
        OnPropertyChanged(nameof(PreviewOffsetY));
        OnPropertyChanged(nameof(PreviewButtonScale));

        ApplyAllCalibration();
        RefreshPreview();
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
        KeyboardLayoutPanel.SetLayoutCalibration(PreviewKeyUnit, PreviewGap, PreviewOffsetX, PreviewOffsetY);
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

        foreach (var item in _keyItems.Where(item => !item.IsZero()))
        {
            KeyboardLayoutPanel.SetPerKeyOverride(item.KeyId, item.OffsetX, item.OffsetY, item.WidthAdjustment, item.HeightAdjustment);
        }
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

    private void SetKeyOverrideValue(string keyId, KeyCalibrationOverrideSettings value)
    {
        var item = _keyItems.FirstOrDefault(entry => string.Equals(entry.KeyId, keyId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        item.OffsetX = value.OffsetX;
        item.OffsetY = value.OffsetY;
        item.WidthAdjustment = value.WidthAdjustment;
        item.HeightAdjustment = value.HeightAdjustment;
    }

    private void RefreshPreview()
    {
        PreviewKeyboard.InvalidateMeasure();
        PreviewKeyboard.InvalidateArrange();
        PreviewKeyboard.UpdateLayout();
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        _suppressUpdates = true;
        try
        {
            _previewKeyUnit = 43;
            _previewGap = 3;
            _previewOffsetX = 65;
            _previewOffsetY = 72;
            _previewButtonScale = 1.0;

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
        OnPropertyChanged(nameof(PreviewGap));
        OnPropertyChanged(nameof(PreviewOffsetX));
        OnPropertyChanged(nameof(PreviewOffsetY));
        OnPropertyChanged(nameof(PreviewButtonScale));

        ApplyAllCalibration();
        RefreshPreview();
        PersistCalibrationLive();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void PresetFactory_Click(object sender, RoutedEventArgs e)
    {
        ApplySimplePreset(43, 3, 65, 72, 1.0, resetDetails: true);
    }

    private void PresetCentered_Click(object sender, RoutedEventArgs e)
    {
        ApplySimplePreset(43, 3, 0, 0, 1.0, resetDetails: true);
    }

    private void PresetTight_Click(object sender, RoutedEventArgs e)
    {
        ApplySimplePreset(40, 1.5, 65, 72, 1.0, resetDetails: true);
    }

    private void SavePermanently_Click(object sender, RoutedEventArgs e)
    {
        SaveCalibration(notifyMainViewModel: true);
        MessageBox.Show(this, "Calibration saved permanently.", "Keyboard Calibration", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadFromJsonFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Calibration JSON",
            Filter = "JSON Files|*.json|All Files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var settings = JsonSerializer.Deserialize<KeyboardCalibrationSettings>(json);

            if (settings is null)
            {
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                settings = config?.Settings?.KeyboardCalibration;
            }

            if (settings is null)
            {
                MessageBox.Show(this, "Could not find keyboard calibration settings in the selected file.", "Keyboard Calibration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadFromCalibration(settings);
            ApplyAllCalibration();
            RefreshPreview();
            SaveCalibration(notifyMainViewModel: true);

            MessageBox.Show(this, "Calibration loaded from JSON and applied.", "Keyboard Calibration", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load calibration JSON:\n{ex.Message}", "Keyboard Calibration", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetSelectedKey_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedKeyItem is null)
        {
            return;
        }

        SelectedKeyItem.Reset();
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
                SetKeyOverrideValue(entry.Key, entry.Value);
            }
        }
        finally
        {
            _suppressJsonSync = false;
            _suppressUpdates = false;
        }

        ApplyPerKeyOverrides();
        RefreshPreview();
        RefreshPerKeyOverridesJsonFromItems();
        JsonEditorStatus = "Applied";
    }

    private void RevertPerKeyJson_Click(object sender, RoutedEventArgs e)
    {
        RefreshPerKeyOverridesJsonFromItems();
        JsonEditorStatus = "Reverted to current values";
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
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

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
                    HeightAdjustment = item.HeightAdjustment
                },
                StringComparer.OrdinalIgnoreCase);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        PerKeyOverridesJson = JsonSerializer.Serialize(map, options);
    }

    private void NudgeValue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var parts = tag.Split(':', 2);
        if (parts.Length != 2)
        {
            return;
        }

        var propertyName = parts[0].Trim();
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var delta))
        {
            return;
        }

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

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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

        public double OffsetX
        {
            get => _offsetX;
            set
            {
                if (Math.Abs(_offsetX - value) < double.Epsilon)
                {
                    return;
                }

                _offsetX = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public double OffsetY
        {
            get => _offsetY;
            set
            {
                if (Math.Abs(_offsetY - value) < double.Epsilon)
                {
                    return;
                }

                _offsetY = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public void Reset()
        {
            _offsetX = 0;
            _offsetY = 0;
            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
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

        public double WidthAdjustment
        {
            get => _widthAdjustment;
            set
            {
                if (Math.Abs(_widthAdjustment - value) < double.Epsilon)
                {
                    return;
                }

                _widthAdjustment = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public void Reset()
        {
            _widthAdjustment = 0;
            OnPropertyChanged(nameof(WidthAdjustment));
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

        public KeyCalibrationItem(string keyId, string name)
        {
            KeyId = keyId;
            Name = name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? Changed;

        public string KeyId { get; }
        public string Name { get; }

        public double OffsetX
        {
            get => _offsetX;
            set
            {
                if (Math.Abs(_offsetX - value) < double.Epsilon)
                {
                    return;
                }

                _offsetX = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public double OffsetY
        {
            get => _offsetY;
            set
            {
                if (Math.Abs(_offsetY - value) < double.Epsilon)
                {
                    return;
                }

                _offsetY = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public double WidthAdjustment
        {
            get => _widthAdjustment;
            set
            {
                if (Math.Abs(_widthAdjustment - value) < double.Epsilon)
                {
                    return;
                }

                _widthAdjustment = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public double HeightAdjustment
        {
            get => _heightAdjustment;
            set
            {
                if (Math.Abs(_heightAdjustment - value) < double.Epsilon)
                {
                    return;
                }

                _heightAdjustment = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public void Reset()
        {
            _offsetX = 0;
            _offsetY = 0;
            _widthAdjustment = 0;
            _heightAdjustment = 0;
            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            OnPropertyChanged(nameof(WidthAdjustment));
            OnPropertyChanged(nameof(HeightAdjustment));
            Changed?.Invoke();
        }

        public bool IsZero()
            => Math.Abs(OffsetX) < double.Epsilon
                && Math.Abs(OffsetY) < double.Epsilon
                && Math.Abs(WidthAdjustment) < double.Epsilon
                && Math.Abs(HeightAdjustment) < double.Epsilon;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
