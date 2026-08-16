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
using System.Windows.Media;
using System.Windows.Threading;

namespace SoundFXStudio.Views.Dialogs;

public partial class KeyboardCalibrationWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _configService = new();
    private readonly AppConfig _config;
    private readonly KeyboardLayoutService _keyboardLayoutService = new();
    private readonly RelayCommand _noopCommand;
    private readonly ObservableCollection<ClusterCalibrationItem> _clusterItems = new();
    private readonly ObservableCollection<KeyCalibrationItem> _keyItems = new();
    private readonly ObservableCollection<RowOffsetItem> _rowItems = new();

    private bool _suppressUpdates;

    private readonly DispatcherTimer _livePreviewThrottle;

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
    private double _previewCapsLockIndicatorSize = 34;
    private double _previewNumLockIndicatorSize = 34;
    private double _previewScrollLockIndicatorSize = 34;

    private double _previewOpenKeyboardButtonX = 36;
    private double _previewOpenKeyboardButtonY = 488;
    private double _previewOpenKeyboardButtonWidth = 220;
    private double _previewOpenKeyboardButtonHeight = 48;

    private double _soundboardPanelX;
    private double _soundboardPanelY;
    private double _soundboardPanelWidth;
    private double _soundboardPanelHeight;
    private double _closePanelX;
    private double _closePanelY;
    private double _closePanelWidth;
    private double _closePanelHeight;

    private KeyCalibrationItem? _selectedKeyItem;
    private string _perKeyOverridesJson = "{}";
    private string _jsonEditorStatus = "Ready";

    private bool _keyboardDragActive;
    private bool _keyboardDragMoved;
    private Point _keyboardDragStart;
    private double _keyboardDragStartX;
    private double _keyboardDragStartY;
    private KeyCalibrationItem? _keyboardDragKeyItem;
    private string? _keyboardDragLamp;
    private FrameworkElement? _keyboardDragElement;
    private KeyboardCluster? _keyboardDragCluster;
    private int? _keyboardDragRow;
    private PreviewDragMode _dragMode = PreviewDragMode.Key;

    public enum PreviewDragMode
    {
        Key,
        Cluster,
        Row
    }

    private const double KeyboardHudBaseWidth = 1512.6;
    private const double KeyboardHudBaseHeight = 608;

    public KeyboardCalibrationWindow()
    {
        InitializeComponent();
        DataContext = this;
        _livePreviewThrottle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _livePreviewThrottle.Tick += OnLivePreviewThrottleTick;
        Loaded += (_, _) =>
        {
            UpdateOverlayPreview();
        };

        _config = _configService.Load();
        _noopCommand = new RelayCommand(SelectPreviewKey);

        BuildKeyboard();
        BuildClusterItems();
        BuildKeyItems();
        BuildRowItems();
        LoadFromSettings();
        ApplyAllCalibration();
        RefreshPreview();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CalibrationSaved;
    public event EventHandler? OpenKeyboardButtonChanged;
    public event EventHandler? LivePreviewChanged;

    private void OnLivePreviewThrottleTick(object? sender, EventArgs e)
    {
        _livePreviewThrottle.Stop();
        LivePreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseLivePreviewChanged()
    {
        _livePreviewThrottle.Stop();
        _livePreviewThrottle.Start();
    }

    public ObservableCollection<KeyboardKey> KeyboardKeys { get; } = new();
    public ObservableCollection<ClusterCalibrationItem> ClusterItems => _clusterItems;
    public ObservableCollection<KeyCalibrationItem> KeyItems => _keyItems;
    public ObservableCollection<RowOffsetItem> RowItems => _rowItems;
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

    public PreviewDragMode DragMode
    {
        get => _dragMode;
        set
        {
            if (_dragMode == value)
            {
                return;
            }

            _dragMode = value;
            OnPropertyChanged();
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

    public double CapsLockIndicatorSize
    {
        get => _previewCapsLockIndicatorSize;
        set => SetAndApply(ref _previewCapsLockIndicatorSize, Math.Clamp(value, 12, 80));
    }

    public double NumLockIndicatorSize
    {
        get => _previewNumLockIndicatorSize;
        set => SetAndApply(ref _previewNumLockIndicatorSize, Math.Clamp(value, 12, 80));
    }

    public double ScrollLockIndicatorSize
    {
        get => _previewScrollLockIndicatorSize;
        set => SetAndApply(ref _previewScrollLockIndicatorSize, Math.Clamp(value, 12, 80));
    }

    public double OpenKeyboardButtonX
    {
        get => _previewOpenKeyboardButtonX;
        set => SetAndApply(ref _previewOpenKeyboardButtonX, Math.Clamp(value, 0, 1500));
    }

    public double OpenKeyboardButtonY
    {
        get => _previewOpenKeyboardButtonY;
        set => SetAndApply(ref _previewOpenKeyboardButtonY, Math.Clamp(value, 0, 1200));
    }

    public double OpenKeyboardButtonWidth
    {
        get => _previewOpenKeyboardButtonWidth;
        set => SetAndApply(ref _previewOpenKeyboardButtonWidth, Math.Clamp(value, 100, 600));
    }

    public double OpenKeyboardButtonHeight
    {
        get => _previewOpenKeyboardButtonHeight;
        set => SetAndApply(ref _previewOpenKeyboardButtonHeight, Math.Clamp(value, 20, 200));
    }

    public double SoundboardPanelX
    {
        get => _soundboardPanelX;
        set => SetHudPanel(ref _soundboardPanelX, value);
    }

    public double SoundboardPanelY
    {
        get => _soundboardPanelY;
        set => SetHudPanel(ref _soundboardPanelY, value);
    }

    public double SoundboardPanelWidth
    {
        get => _soundboardPanelWidth;
        set => SetHudPanel(ref _soundboardPanelWidth, Math.Clamp(value, 60, 600));
    }

    public double SoundboardPanelHeight
    {
        get => _soundboardPanelHeight;
        set => SetHudPanel(ref _soundboardPanelHeight, Math.Clamp(value, 20, 120));
    }

    public double ClosePanelX
    {
        get => _closePanelX;
        set => SetHudPanel(ref _closePanelX, value);
    }

    public double ClosePanelY
    {
        get => _closePanelY;
        set => SetHudPanel(ref _closePanelY, value);
    }

    public double ClosePanelWidth
    {
        get => _closePanelWidth;
        set => SetHudPanel(ref _closePanelWidth, Math.Clamp(value, 40, 500));
    }

    public double ClosePanelHeight
    {
        get => _closePanelHeight;
        set => SetHudPanel(ref _closePanelHeight, Math.Clamp(value, 20, 120));
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
        AddClusterItem("Main Letters & Symbols", KeyboardCluster.MainLettersCluster);
        AddClusterItem("Special Keys (Tab/Caps/Shift/Ctrl/Win/Alt)", KeyboardCluster.MainTypingCluster);
        AddClusterItem("Navigation (Ins/Home/PgUp/PgDn/Del/End)", KeyboardCluster.NavigationCluster);
        AddClusterItem("Arrows", KeyboardCluster.ArrowCluster);
        AddClusterItem("Numpad", KeyboardCluster.NumpadCluster);
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
            _previewCapsLockIndicatorSize = settings.CapsLockIndicatorSize;
            _previewNumLockIndicatorSize = settings.NumLockIndicatorSize;
            _previewScrollLockIndicatorSize = settings.ScrollLockIndicatorSize;
            _previewOpenKeyboardButtonX = settings.OpenKeyboardButtonX;
            _previewOpenKeyboardButtonY = settings.OpenKeyboardButtonY;
            _previewOpenKeyboardButtonWidth = settings.OpenKeyboardButtonWidth;
            _previewOpenKeyboardButtonHeight = settings.OpenKeyboardButtonHeight;

            _soundboardPanelX = settings.SoundboardStatusPanelX;
            _soundboardPanelY = settings.SoundboardStatusPanelY;
            _soundboardPanelWidth = settings.SoundboardStatusPanelWidth;
            _soundboardPanelHeight = settings.SoundboardStatusPanelHeight;
            _closePanelX = settings.CloseButtonPanelX;
            _closePanelY = settings.CloseButtonPanelY;
            _closePanelWidth = settings.CloseButtonPanelWidth;
            _closePanelHeight = settings.CloseButtonPanelHeight;

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
            GetClusterItem(KeyboardCluster.MainLettersCluster).OffsetX = settings.MainLettersOffsetX;
            GetClusterItem(KeyboardCluster.MainLettersCluster).OffsetY = settings.MainLettersOffsetY;

            GetClusterItem(KeyboardCluster.EscCluster).WidthAdjustment = settings.EscWidthAdjustment;
            GetClusterItem(KeyboardCluster.EscCluster).HeightAdjustment = settings.EscHeightAdjustment;
            GetClusterItem(KeyboardCluster.F1ToF4Cluster).WidthAdjustment = settings.F1ToF4WidthAdjustment;
            GetClusterItem(KeyboardCluster.F1ToF4Cluster).HeightAdjustment = settings.F1ToF4HeightAdjustment;
            GetClusterItem(KeyboardCluster.F5ToF8Cluster).WidthAdjustment = settings.F5ToF8WidthAdjustment;
            GetClusterItem(KeyboardCluster.F5ToF8Cluster).HeightAdjustment = settings.F5ToF8HeightAdjustment;
            GetClusterItem(KeyboardCluster.F9ToF12Cluster).WidthAdjustment = settings.F9ToF12WidthAdjustment;
            GetClusterItem(KeyboardCluster.F9ToF12Cluster).HeightAdjustment = settings.F9ToF12HeightAdjustment;
            GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).WidthAdjustment = settings.PrintScrollPauseWidthAdjustment;
            GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).HeightAdjustment = settings.PrintScrollPauseHeightAdjustment;
            GetClusterItem(KeyboardCluster.MainTypingCluster).WidthAdjustment = settings.MainTypingWidthAdjustment;
            GetClusterItem(KeyboardCluster.MainTypingCluster).HeightAdjustment = settings.MainTypingHeightAdjustment;
            GetClusterItem(KeyboardCluster.NavigationCluster).WidthAdjustment = settings.NavigationWidthAdjustment;
            GetClusterItem(KeyboardCluster.NavigationCluster).HeightAdjustment = settings.NavigationHeightAdjustment;
            GetClusterItem(KeyboardCluster.ArrowCluster).WidthAdjustment = settings.ArrowWidthAdjustment;
            GetClusterItem(KeyboardCluster.ArrowCluster).HeightAdjustment = settings.ArrowHeightAdjustment;
            GetClusterItem(KeyboardCluster.NumpadCluster).WidthAdjustment = settings.NumpadWidthAdjustment;
            GetClusterItem(KeyboardCluster.NumpadCluster).HeightAdjustment = settings.NumpadHeightAdjustment;
            GetClusterItem(KeyboardCluster.MainLettersCluster).WidthAdjustment = settings.MainLettersWidthAdjustment;
            GetClusterItem(KeyboardCluster.MainLettersCluster).HeightAdjustment = settings.MainLettersHeightAdjustment;

            LoadClusterGaps(settings);

            SetRowItem(1, settings.MainRowOffsetX1, settings.MainRowOffsetY1, GetRowGap(settings.RowGapOverridesX, 1), GetRowGap(settings.RowGapOverridesY, 1));
            SetRowItem(2, settings.MainRowOffsetX2, settings.MainRowOffsetY2, GetRowGap(settings.RowGapOverridesX, 2), GetRowGap(settings.RowGapOverridesY, 2));
            SetRowItem(3, settings.MainRowOffsetX3, settings.MainRowOffsetY3, GetRowGap(settings.RowGapOverridesX, 3), GetRowGap(settings.RowGapOverridesY, 3));
            SetRowItem(4, settings.MainRowOffsetX4, settings.MainRowOffsetY4, GetRowGap(settings.RowGapOverridesX, 4), GetRowGap(settings.RowGapOverridesY, 4));

            foreach (var entry in settings.KeyBaselines)
            {
                KeyboardLayoutPanel.SetKeyBaseline(entry.Key, entry.Value.X, entry.Value.Y, entry.Value.Width, entry.Value.Height);
            }

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
        OnPropertyChanged(nameof(CapsLockIndicatorSize));
        OnPropertyChanged(nameof(NumLockIndicatorSize));
        OnPropertyChanged(nameof(ScrollLockIndicatorSize));
        OnPropertyChanged(nameof(OpenKeyboardButtonX));
        OnPropertyChanged(nameof(OpenKeyboardButtonY));
        OnPropertyChanged(nameof(OpenKeyboardButtonWidth));
        OnPropertyChanged(nameof(OpenKeyboardButtonHeight));
        OnPropertyChanged(nameof(SoundboardPanelX));
        OnPropertyChanged(nameof(SoundboardPanelY));
        OnPropertyChanged(nameof(SoundboardPanelWidth));
        OnPropertyChanged(nameof(SoundboardPanelHeight));
        OnPropertyChanged(nameof(ClosePanelX));
        OnPropertyChanged(nameof(ClosePanelY));
        OnPropertyChanged(nameof(ClosePanelWidth));
        OnPropertyChanged(nameof(ClosePanelHeight));

        RefreshPerKeyOverridesJsonFromItems();
    }

    private void LoadClusterGaps(KeyboardCalibrationSettings settings)
    {
        foreach (var item in _clusterItems)
        {
            if (settings.ClusterGapOverridesX.TryGetValue((int)item.Cluster, out var gapX))
            {
                item.GapX = gapX;
            }

            if (settings.ClusterGapOverridesY.TryGetValue((int)item.Cluster, out var gapY))
            {
                item.GapY = gapY;
            }
        }
    }

    private static double GetRowGap(Dictionary<int, double> overrides, int rowIndex)
        => overrides.TryGetValue(rowIndex, out var gap) ? gap : 0;

    private void AddClusterItem(string name, KeyboardCluster cluster)
    {
        var item = new ClusterCalibrationItem(name, cluster);
        item.Changed += OnClusterItemChanged;
        _clusterItems.Add(item);
    }

    private void BuildRowItems()
    {
        _rowItems.Clear();
        AddRowItem("Row 1 — Number & symbol row", 1);
        AddRowItem("Row 2 — Q W E R T Y row", 2);
        AddRowItem("Row 3 — A S D F G H row", 3);
        AddRowItem("Row 4 — Z X C V B N M row", 4);
    }

    private void AddRowItem(string name, int rowIndex)
    {
        var item = new RowOffsetItem(name, rowIndex);
        item.Changed += OnRowItemChanged;
        _rowItems.Add(item);
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
        RaiseLivePreviewChanged();
    }

    private void SetHudPanel(ref double field, double value, [CallerMemberName] string? propertyName = null)
    {
        if (Math.Abs(field - value) < double.Epsilon)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        RaiseLivePreviewChanged();
    }

    private void OnKeyItemChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        ApplyPerKeyOverrides();
        RefreshPreview();
        RefreshPerKeyOverridesJsonFromItems();
        RaiseLivePreviewChanged();
    }

    private void ApplyAllCalibration()
    {
        KeyboardLayoutPanel.SetLayoutCalibration(PreviewKeyUnit, PreviewGapX, PreviewGapY, PreviewOffsetX, PreviewOffsetY);
        KeyboardLayoutPanel.ButtonScale = PreviewButtonScale;
        ApplyClusterCalibration();
        ApplyRowCalibration();
        ApplyPerKeyOverrides();
        UpdateOverlayPreview();
        RaiseLivePreviewChanged();
    }

    private void UpdateOverlayPreview()
    {
        OpenKeyboardButtonChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetSoundboardPanel_Click(object sender, RoutedEventArgs e)
    {
        _suppressUpdates = true;
        try
        {
            _soundboardPanelX = 0;
            _soundboardPanelY = 0;
            _soundboardPanelWidth = 0;
            _soundboardPanelHeight = 0;
        }
        finally
        {
            _suppressUpdates = false;
        }

        OnPropertyChanged(nameof(SoundboardPanelX));
        OnPropertyChanged(nameof(SoundboardPanelY));
        OnPropertyChanged(nameof(SoundboardPanelWidth));
        OnPropertyChanged(nameof(SoundboardPanelHeight));
        RaiseLivePreviewChanged();
    }

    private void ResetClosePanel_Click(object sender, RoutedEventArgs e)
    {
        _suppressUpdates = true;
        try
        {
            _closePanelX = 0;
            _closePanelY = 0;
            _closePanelWidth = 0;
            _closePanelHeight = 0;
        }
        finally
        {
            _suppressUpdates = false;
        }

        OnPropertyChanged(nameof(ClosePanelX));
        OnPropertyChanged(nameof(ClosePanelY));
        OnPropertyChanged(nameof(ClosePanelWidth));
        OnPropertyChanged(nameof(ClosePanelHeight));
        RaiseLivePreviewChanged();
    }

    private void PreviewKeyboard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        _keyboardDragKeyItem = FindKeyItemAt(e.OriginalSource);
        _keyboardDragLamp = FindLampAt(e.OriginalSource);

        if (_keyboardDragKeyItem is not null)
        {
            SelectedKeyItem = _keyboardDragKeyItem;
            var cluster = FindClusterAt(e.OriginalSource);
            _keyboardDragCluster = cluster;
            _keyboardDragRow = cluster == KeyboardCluster.MainLettersCluster
                ? FindRowIndexAt(e.OriginalSource)
                : null;

            if (_dragMode == PreviewDragMode.Cluster && cluster is not null)
            {
                var clusterItem = GetClusterItem(cluster.Value);
                _keyboardDragStartX = clusterItem.OffsetX;
                _keyboardDragStartY = clusterItem.OffsetY;
                _keyboardDragElement = null;
            }
            else if (_dragMode == PreviewDragMode.Row && _keyboardDragRow is int rowIndex)
            {
                var rowItem = GetRowItem(rowIndex);
                _keyboardDragStartX = rowItem.OffsetX;
                _keyboardDragStartY = rowItem.OffsetY;
                _keyboardDragElement = null;
            }
            else
            {
                _keyboardDragStartX = _keyboardDragKeyItem.OffsetX;
                _keyboardDragStartY = _keyboardDragKeyItem.OffsetY;
                _keyboardDragElement = FindKeyButton(_keyboardDragKeyItem.KeyId);
            }
        }
        else if (_keyboardDragLamp is not null)
        {
            _keyboardDragCluster = null;
            _keyboardDragRow = null;
            _keyboardDragStartX = GetLampOffsetX(_keyboardDragLamp);
            _keyboardDragStartY = GetLampOffsetY(_keyboardDragLamp);
            _keyboardDragElement = FindLampElement(_keyboardDragLamp);
        }
        else
        {
            _keyboardDragCluster = null;
            _keyboardDragRow = null;
            _keyboardDragStartX = PreviewOffsetX;
            _keyboardDragStartY = PreviewOffsetY;
            _keyboardDragElement = FindVisualChild<KeyboardLayoutPanel>(PreviewKeyboard);
        }

        _keyboardDragActive = true;
        _keyboardDragMoved = false;
        _keyboardDragStart = e.GetPosition(PreviewKeyboard);
        PreviewKeyboard.CaptureMouse();
        e.Handled = true;
    }

    private void PreviewKeyboard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_keyboardDragActive)
        {
            return;
        }

        var position = e.GetPosition(PreviewKeyboard);
        if (!_keyboardDragMoved &&
            Math.Abs(position.X - _keyboardDragStart.X) < 3 &&
            Math.Abs(position.Y - _keyboardDragStart.Y) < 3)
        {
            return;
        }

        _keyboardDragMoved = true;
        if (_keyboardDragElement is not null)
        {
            _keyboardDragElement.RenderTransform =
                new TranslateTransform(position.X - _keyboardDragStart.X, position.Y - _keyboardDragStart.Y);
        }

        e.Handled = true;
    }

    private void PreviewKeyboard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_keyboardDragActive)
        {
            return;
        }

        var wasMoved = _keyboardDragMoved;
        var keyItem = _keyboardDragKeyItem;
        var lamp = _keyboardDragLamp;
        var cluster = _keyboardDragCluster;
        var row = _keyboardDragRow;
        _keyboardDragActive = false;
        PreviewKeyboard.ReleaseMouseCapture();

        if (wasMoved)
        {
            var position = e.GetPosition(PreviewKeyboard);
            var dx = position.X - _keyboardDragStart.X;
            var dy = position.Y - _keyboardDragStart.Y;

            if (_keyboardDragElement is not null)
            {
                _keyboardDragElement.RenderTransform = null;
            }

            if (keyItem is not null && cluster is not null && _dragMode == PreviewDragMode.Cluster)
            {
                var clusterItem = GetClusterItem(cluster.Value);
                clusterItem.OffsetX = _keyboardDragStartX + dx;
                clusterItem.OffsetY = _keyboardDragStartY + dy;
            }
            else if (keyItem is not null && row is int rowIndex && _dragMode == PreviewDragMode.Row)
            {
                var rowItem = GetRowItem(rowIndex);
                rowItem.OffsetX = _keyboardDragStartX + dx;
                rowItem.OffsetY = _keyboardDragStartY + dy;
            }
            else if (keyItem is not null)
            {
                keyItem.OffsetX = _keyboardDragStartX + dx;
                keyItem.OffsetY = _keyboardDragStartY + dy;
            }
            else if (lamp is not null)
            {
                SetLampPosition(lamp, _keyboardDragStartX + dx, _keyboardDragStartY + dy);
            }
            else
            {
                PreviewOffsetX = _keyboardDragStartX + dx;
                PreviewOffsetY = _keyboardDragStartY + dy;
            }
        }
        else if (keyItem is null)
        {
            SelectedKeyItem = null;
        }

        _keyboardDragKeyItem = null;
        _keyboardDragLamp = null;
        _keyboardDragElement = null;
        _keyboardDragCluster = null;
        _keyboardDragRow = null;
        e.Handled = true;
    }

    private KeyCalibrationItem? FindKeyItemAt(object originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is Button { DataContext: KeyboardKey key })
            {
                return _keyItems.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
            }

            current = GetVisualOrLogicalParent(current);
        }

        return null;
    }

    private KeyboardCluster? FindClusterAt(object originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is Button { DataContext: KeyboardKey key })
            {
                return KeyboardLayoutPanel.GetClusterOf(key);
            }

            current = GetVisualOrLogicalParent(current);
        }

        return null;
    }

    private static int? FindRowIndexAt(object originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is Button { DataContext: KeyboardKey key })
            {
                return key.RowIndex;
            }

            current = GetVisualOrLogicalParent(current);
        }

        return null;
    }

    private static string? FindLampAt(object originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { Name: "CapsLockIndicator" })
            {
                return "Caps";
            }

            if (current is FrameworkElement { Name: "NumLockIndicator" })
            {
                return "Num";
            }

            if (current is FrameworkElement { Name: "ScrollLockIndicator" })
            {
                return "Scroll";
            }

            current = GetVisualOrLogicalParent(current);
        }

        return null;
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject? current)
    {
        if (current is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(current);
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match && (predicate is null || predicate(match)))
            {
                return match;
            }

            if (FindVisualChild(child, predicate) is T descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private Button? FindKeyButton(string keyId)
    {
        return FindVisualChild<Button>(
            PreviewKeyboard,
            button => button.DataContext is KeyboardKey key
                      && string.Equals(key.Id, keyId, StringComparison.OrdinalIgnoreCase));
    }

    private FrameworkElement? FindLampElement(string lamp) => lamp switch
    {
        "Caps" => FindVisualChild<FrameworkElement>(PreviewKeyboard, element => element.Name == "CapsLockIndicator"),
        "Num" => FindVisualChild<FrameworkElement>(PreviewKeyboard, element => element.Name == "NumLockIndicator"),
        _ => FindVisualChild<FrameworkElement>(PreviewKeyboard, element => element.Name == "ScrollLockIndicator")
    };

    private double GetLampOffsetX(string lamp) => lamp switch
    {
        "Caps" => CapsLockIndicatorOffsetX,
        "Num" => NumLockIndicatorOffsetX,
        _ => ScrollLockIndicatorOffsetX
    };

    private double GetLampOffsetY(string lamp) => lamp switch
    {
        "Caps" => CapsLockIndicatorOffsetY,
        "Num" => NumLockIndicatorOffsetY,
        _ => ScrollLockIndicatorOffsetY
    };

    private void SetLampPosition(string lamp, double x, double y)
    {
        switch (lamp)
        {
            case "Caps":
                CapsLockIndicatorOffsetX = x;
                CapsLockIndicatorOffsetY = y;
                break;
            case "Num":
                NumLockIndicatorOffsetX = x;
                NumLockIndicatorOffsetY = y;
                break;
            default:
                ScrollLockIndicatorOffsetX = x;
                ScrollLockIndicatorOffsetY = y;
                break;
        }
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
            GetClusterItem(KeyboardCluster.NumpadCluster).OffsetY,
            GetClusterItem(KeyboardCluster.MainLettersCluster).OffsetX,
            GetClusterItem(KeyboardCluster.MainLettersCluster).OffsetY,
            GetClusterItem(KeyboardCluster.MainLettersCluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.MainLettersCluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.EscCluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.EscCluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.F1ToF4Cluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.F1ToF4Cluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.F5ToF8Cluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.F5ToF8Cluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.F9ToF12Cluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.F9ToF12Cluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.MainTypingCluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.MainTypingCluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.NavigationCluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.NavigationCluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.ArrowCluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.ArrowCluster).HeightAdjustment,
            GetClusterItem(KeyboardCluster.NumpadCluster).WidthAdjustment,
            GetClusterItem(KeyboardCluster.NumpadCluster).HeightAdjustment);

        foreach (var item in _clusterItems)
        {
            KeyboardLayoutPanel.SetClusterGapCalibration(item.Cluster, item.GapX, item.GapY);
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

    private void SetRowItem(int rowIndex, double offsetX, double offsetY, double gapX = 0, double gapY = 0)
    {
        var item = _rowItems.FirstOrDefault(entry => entry.RowIndex == rowIndex);
        if (item is null)
        {
            return;
        }

        item.OffsetX = offsetX;
        item.OffsetY = offsetY;
        item.GapX = gapX;
        item.GapY = gapY;
    }

    private void ApplyRowCalibration()
    {
        foreach (var item in _rowItems)
        {
            KeyboardLayoutPanel.SetRowCalibration(item.RowIndex, item.OffsetX, item.OffsetY);
            KeyboardLayoutPanel.SetRowGapCalibration(item.RowIndex, item.GapX, item.GapY);
        }
    }

    private void OnRowItemChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        ApplyRowCalibration();
        RefreshPreview();
        RaiseLivePreviewChanged();
    }

    private void OnClusterItemChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        ApplyClusterCalibration();
        RefreshPreview();
        RaiseLivePreviewChanged();
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

    public KeyboardCalibrationSettings BuildCalibration()
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
        calibration.CapsLockIndicatorSize = CapsLockIndicatorSize;
        calibration.NumLockIndicatorSize = NumLockIndicatorSize;
        calibration.ScrollLockIndicatorSize = ScrollLockIndicatorSize;
        calibration.OpenKeyboardButtonX = Math.Round(OpenKeyboardButtonX, 1);
        calibration.OpenKeyboardButtonY = Math.Round(OpenKeyboardButtonY, 1);
        calibration.OpenKeyboardButtonWidth = Math.Round(OpenKeyboardButtonWidth, 1);
        calibration.OpenKeyboardButtonHeight = Math.Round(OpenKeyboardButtonHeight, 1);

        calibration.SoundboardStatusPanelX = Math.Round(SoundboardPanelX, 1);
        calibration.SoundboardStatusPanelY = Math.Round(SoundboardPanelY, 1);
        calibration.SoundboardStatusPanelWidth = Math.Round(SoundboardPanelWidth, 1);
        calibration.SoundboardStatusPanelHeight = Math.Round(SoundboardPanelHeight, 1);
        calibration.CloseButtonPanelX = Math.Round(ClosePanelX, 1);
        calibration.CloseButtonPanelY = Math.Round(ClosePanelY, 1);
        calibration.CloseButtonPanelWidth = Math.Round(ClosePanelWidth, 1);
        calibration.CloseButtonPanelHeight = Math.Round(ClosePanelHeight, 1);

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
        calibration.MainLettersOffsetX = GetClusterItem(KeyboardCluster.MainLettersCluster).OffsetX;
        calibration.MainLettersOffsetY = GetClusterItem(KeyboardCluster.MainLettersCluster).OffsetY;

        calibration.EscWidthAdjustment = GetClusterItem(KeyboardCluster.EscCluster).WidthAdjustment;
        calibration.EscHeightAdjustment = GetClusterItem(KeyboardCluster.EscCluster).HeightAdjustment;
        calibration.F1ToF4WidthAdjustment = GetClusterItem(KeyboardCluster.F1ToF4Cluster).WidthAdjustment;
        calibration.F1ToF4HeightAdjustment = GetClusterItem(KeyboardCluster.F1ToF4Cluster).HeightAdjustment;
        calibration.F5ToF8WidthAdjustment = GetClusterItem(KeyboardCluster.F5ToF8Cluster).WidthAdjustment;
        calibration.F5ToF8HeightAdjustment = GetClusterItem(KeyboardCluster.F5ToF8Cluster).HeightAdjustment;
        calibration.F9ToF12WidthAdjustment = GetClusterItem(KeyboardCluster.F9ToF12Cluster).WidthAdjustment;
        calibration.F9ToF12HeightAdjustment = GetClusterItem(KeyboardCluster.F9ToF12Cluster).HeightAdjustment;
        calibration.PrintScrollPauseWidthAdjustment = GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).WidthAdjustment;
        calibration.PrintScrollPauseHeightAdjustment = GetClusterItem(KeyboardCluster.PrintScrollPauseCluster).HeightAdjustment;
        calibration.MainTypingWidthAdjustment = GetClusterItem(KeyboardCluster.MainTypingCluster).WidthAdjustment;
        calibration.MainTypingHeightAdjustment = GetClusterItem(KeyboardCluster.MainTypingCluster).HeightAdjustment;
        calibration.NavigationWidthAdjustment = GetClusterItem(KeyboardCluster.NavigationCluster).WidthAdjustment;
        calibration.NavigationHeightAdjustment = GetClusterItem(KeyboardCluster.NavigationCluster).HeightAdjustment;
        calibration.ArrowWidthAdjustment = GetClusterItem(KeyboardCluster.ArrowCluster).WidthAdjustment;
        calibration.ArrowHeightAdjustment = GetClusterItem(KeyboardCluster.ArrowCluster).HeightAdjustment;
        calibration.NumpadWidthAdjustment = GetClusterItem(KeyboardCluster.NumpadCluster).WidthAdjustment;
        calibration.NumpadHeightAdjustment = GetClusterItem(KeyboardCluster.NumpadCluster).HeightAdjustment;
        calibration.MainLettersWidthAdjustment = GetClusterItem(KeyboardCluster.MainLettersCluster).WidthAdjustment;
        calibration.MainLettersHeightAdjustment = GetClusterItem(KeyboardCluster.MainLettersCluster).HeightAdjustment;

        calibration.MainRowOffsetX1 = GetRowItem(1).OffsetX;
        calibration.MainRowOffsetY1 = GetRowItem(1).OffsetY;
        calibration.MainRowOffsetX2 = GetRowItem(2).OffsetX;
        calibration.MainRowOffsetY2 = GetRowItem(2).OffsetY;
        calibration.MainRowOffsetX3 = GetRowItem(3).OffsetX;
        calibration.MainRowOffsetY3 = GetRowItem(3).OffsetY;
        calibration.MainRowOffsetX4 = GetRowItem(4).OffsetX;
        calibration.MainRowOffsetY4 = GetRowItem(4).OffsetY;

        calibration.ClusterGapOverridesX = _clusterItems
            .Where(item => Math.Abs(item.GapX) > double.Epsilon)
            .ToDictionary(item => (int)item.Cluster, item => item.GapX);
        calibration.ClusterGapOverridesY = _clusterItems
            .Where(item => Math.Abs(item.GapY) > double.Epsilon)
            .ToDictionary(item => (int)item.Cluster, item => item.GapY);
        calibration.RowGapOverridesX = _rowItems
            .Where(item => Math.Abs(item.GapX) > double.Epsilon)
            .ToDictionary(item => item.RowIndex, item => item.GapX);
        calibration.RowGapOverridesY = _rowItems
            .Where(item => Math.Abs(item.GapY) > double.Epsilon)
            .ToDictionary(item => item.RowIndex, item => item.GapY);
        calibration.KeyBaselines = KeyboardLayoutPanel.GetKeyBaselinesSnapshot()
            .ToDictionary(
                entry => entry.Key,
                entry => new KeyBaselineSettings { X = entry.Value.X, Y = entry.Value.Y, Width = entry.Value.Width, Height = entry.Value.Height },
                StringComparer.OrdinalIgnoreCase);

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

        return calibration;
    }

    private void SaveCalibration(bool notifyMainViewModel)
    {
        var calibration = BuildCalibration();
        _config.Settings.KeyboardCalibration = calibration;
        _configService.Save(_config);

        if (notifyMainViewModel)
        {
            CalibrationSaved?.Invoke(this, EventArgs.Empty);
        }
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
            _previewCapsLockIndicatorSize = 34;
            _previewNumLockIndicatorSize = 34;
            _previewScrollLockIndicatorSize = 34;
            _previewOpenKeyboardButtonX = 36;
            _previewOpenKeyboardButtonY = 488;
            _previewOpenKeyboardButtonWidth = 220;
            _previewOpenKeyboardButtonHeight = 48;

            _soundboardPanelX = 0;
            _soundboardPanelY = 0;
            _soundboardPanelWidth = 0;
            _soundboardPanelHeight = 0;
            _closePanelX = 0;
            _closePanelY = 0;
            _closePanelWidth = 0;
            _closePanelHeight = 0;

            foreach (var cluster in _clusterItems)
            {
                cluster.Reset();
            }

            foreach (var row in _rowItems)
            {
                row.Reset();
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

        KeyboardLayoutPanel.ClearKeyBaselines();

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
        OnPropertyChanged(nameof(CapsLockIndicatorSize));
        OnPropertyChanged(nameof(NumLockIndicatorSize));
        OnPropertyChanged(nameof(ScrollLockIndicatorSize));
        OnPropertyChanged(nameof(OpenKeyboardButtonX));
        OnPropertyChanged(nameof(OpenKeyboardButtonY));
        OnPropertyChanged(nameof(OpenKeyboardButtonWidth));
        OnPropertyChanged(nameof(OpenKeyboardButtonHeight));
        OnPropertyChanged(nameof(SoundboardPanelX));
        OnPropertyChanged(nameof(SoundboardPanelY));
        OnPropertyChanged(nameof(SoundboardPanelWidth));
        OnPropertyChanged(nameof(SoundboardPanelHeight));
        OnPropertyChanged(nameof(ClosePanelX));
        OnPropertyChanged(nameof(ClosePanelY));
        OnPropertyChanged(nameof(ClosePanelWidth));
        OnPropertyChanged(nameof(ClosePanelHeight));

        ApplyAllCalibration();
        RefreshPreview();
    }

    private void SavePermanently_Click(object sender, RoutedEventArgs e)
    {
        SaveCalibration(notifyMainViewModel: true);
        JsonEditorStatus = "Saved";
    }

    private void SetAsDefault_Click(object sender, RoutedEventArgs e)
    {
        ApplyAllCalibration();
        RefreshPreview();

        KeyboardLayoutPanel.ClearKeyBaselines();
        foreach (var key in KeyboardKeys)
        {
            if (KeyboardLayoutPanel.GetKeyGeometry(key) is { } geometry)
            {
                KeyboardLayoutPanel.SetKeyBaseline(key.Id, geometry.X, geometry.Y, geometry.Width, geometry.Height);
            }
        }

        _suppressUpdates = true;
        try
        {
            foreach (var cluster in _clusterItems)
            {
                cluster.Reset();
            }

            foreach (var row in _rowItems)
            {
                row.Reset();
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

        ApplyClusterCalibration();
        ApplyRowCalibration();
        ApplyPerKeyOverrides();
        RefreshPerKeyOverridesJsonFromItems();
        RefreshPreview();
        JsonEditorStatus = "Set as Default captured";
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

    private void ResetSelectedRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not RowOffsetItem item)
        {
            return;
        }

        item.Reset();
    }

    private void NudgeRowKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || sender is not FrameworkElement element || element.DataContext is not RowOffsetItem item)
        {
            return;
        }

        var parts = tag.Split(':', 2);
        if (parts.Length != 2 || !string.Equals(parts[1].Trim(), "key", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sign = parts[1].StartsWith("-", StringComparison.Ordinal) ? -1d : 1d;
        var delta = (PreviewKeyUnit + PreviewGapX) * sign;

        if (string.Equals(parts[0], "X", StringComparison.OrdinalIgnoreCase))
        {
            item.OffsetX += delta;
        }
        else if (string.Equals(parts[0], "Y", StringComparison.OrdinalIgnoreCase))
        {
            item.OffsetY += delta;
        }
    }

    private void ResetOpenKeyboardButton_Click(object sender, RoutedEventArgs e)
    {
        _suppressUpdates = true;
        try
        {
            _previewOpenKeyboardButtonX = 36;
            _previewOpenKeyboardButtonY = 488;
            _previewOpenKeyboardButtonWidth = 220;
            _previewOpenKeyboardButtonHeight = 48;
        }
        finally
        {
            _suppressUpdates = false;
        }

        OnPropertyChanged(nameof(OpenKeyboardButtonX));
        OnPropertyChanged(nameof(OpenKeyboardButtonY));
        OnPropertyChanged(nameof(OpenKeyboardButtonWidth));
        OnPropertyChanged(nameof(OpenKeyboardButtonHeight));
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

    private RowOffsetItem GetRowItem(int rowIndex)
        => _rowItems.First(item => item.RowIndex == rowIndex);

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

        // Hold Shift for coarse movement while default clicks stay precise.
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            delta *= 5;
        }

        property.SetValue(target, current + delta);
    }

    private void NudgeAllLamps_Click(object sender, RoutedEventArgs e)
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

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            delta *= 5;
        }

        switch (parts[0].Trim())
        {
            case "X":
                CapsLockIndicatorOffsetX += delta;
                NumLockIndicatorOffsetX += delta;
                ScrollLockIndicatorOffsetX += delta;
                break;
            case "Y":
                CapsLockIndicatorOffsetY += delta;
                NumLockIndicatorOffsetY += delta;
                ScrollLockIndicatorOffsetY += delta;
                break;
            case "Size":
                CapsLockIndicatorSize += delta;
                NumLockIndicatorSize += delta;
                ScrollLockIndicatorSize += delta;
                break;
        }
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

    private void DragModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DragMode = DragModeComboBox.SelectedIndex switch
        {
            1 => PreviewDragMode.Cluster,
            2 => PreviewDragMode.Row,
            _ => PreviewDragMode.Key
        };
    }

    private void ApplyZoom()
    {
        if (PreviewViewbox is null)
            return;
        PreviewViewbox.LayoutTransform = new System.Windows.Media.ScaleTransform(ZoomLevel / 100.0, ZoomLevel / 100.0);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static double NormalizeLampY(double value)
        => value < 220 ? value + 70 : value;

    private void ResetLampSizes_Click(object sender, RoutedEventArgs e)
    {
        CapsLockIndicatorSize = 34;
        NumLockIndicatorSize = 34;
        ScrollLockIndicatorSize = 34;
    }

    public sealed class RowOffsetItem : INotifyPropertyChanged
    {
        private double _offsetX;
        private double _offsetY;
        private double _gapX;
        private double _gapY;

        public RowOffsetItem(string name, int rowIndex)
        {
            Name = name;
            RowIndex = rowIndex;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? Changed;

        public string Name { get; }
        public int RowIndex { get; }

        public double OffsetX { get => _offsetX; set => Set(ref _offsetX, value); }
        public double OffsetY { get => _offsetY; set => Set(ref _offsetY, value); }
        public double GapX { get => _gapX; set => Set(ref _gapX, value); }
        public double GapY { get => _gapY; set => Set(ref _gapY, value); }

        public void Reset()
        {
            _offsetX = 0;
            _offsetY = 0;
            _gapX = 0;
            _gapY = 0;
            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            OnPropertyChanged(nameof(GapX));
            OnPropertyChanged(nameof(GapY));
            Changed?.Invoke();
        }

        public bool IsZero()
        {
            return Math.Abs(OffsetX) < double.Epsilon
                && Math.Abs(OffsetY) < double.Epsilon
                && Math.Abs(GapX) < double.Epsilon
                && Math.Abs(GapY) < double.Epsilon;
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

    public sealed class ClusterCalibrationItem : INotifyPropertyChanged
    {
        private double _offsetX;
        private double _offsetY;
        private double _widthAdjustment;
        private double _heightAdjustment;
        private double _gapX;
        private double _gapY;

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
        public double WidthAdjustment { get => _widthAdjustment; set => Set(ref _widthAdjustment, value); }
        public double HeightAdjustment { get => _heightAdjustment; set => Set(ref _heightAdjustment, value); }
        public double GapX { get => _gapX; set => Set(ref _gapX, value); }
        public double GapY { get => _gapY; set => Set(ref _gapY, value); }

        public void Reset()
        {
            _offsetX = 0;
            _offsetY = 0;
            _widthAdjustment = 0;
            _heightAdjustment = 0;
            _gapX = 0;
            _gapY = 0;
            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            OnPropertyChanged(nameof(WidthAdjustment));
            OnPropertyChanged(nameof(HeightAdjustment));
            OnPropertyChanged(nameof(GapX));
            OnPropertyChanged(nameof(GapY));
            Changed?.Invoke();
        }

        public bool IsZero()
        {
            return Math.Abs(OffsetX) < double.Epsilon
                && Math.Abs(OffsetY) < double.Epsilon
                && Math.Abs(WidthAdjustment) < double.Epsilon
                && Math.Abs(HeightAdjustment) < double.Epsilon
                && Math.Abs(GapX) < double.Epsilon
                && Math.Abs(GapY) < double.Epsilon;
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
