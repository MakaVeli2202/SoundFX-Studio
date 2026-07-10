using SoundFXStudio.Controls;
using SoundFXStudio.Infrastructure;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace SoundFXStudio.Views.Dialogs;

public partial class KeyboardCalibrationWindow : Window, INotifyPropertyChanged
{
    private readonly KeyboardLayoutService _keyboardLayoutService = new();
    private readonly RelayCommand _noopCommand;
    private readonly ObservableCollection<ClusterCalibrationItem> _clusterItems = new();
    private readonly ObservableCollection<SpecialKeyOverrideItem> _specialItems = new();
    private bool _suppressUpdates;
    private double _previewKeyUnit = 43;
    private double _previewGap = 3;
    private double _previewOffsetX = 65;
    private double _previewOffsetY = 72;
    private double _previewButtonScale = 0.8;
    private bool _debugCalibration = true;

    public KeyboardCalibrationWindow()
    {
        InitializeComponent();
        DataContext = this;

        _noopCommand = new RelayCommand(_ => { });
        KeyboardLayoutPanel.DebugKeyboardCalibration = true;

        BuildKeyboard();
        BuildClusterItems();
        BuildSpecialItems();
        ApplyAllCalibration();
        RefreshPreview();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<KeyboardKey> KeyboardKeys { get; } = new();
    public ObservableCollection<ClusterCalibrationItem> ClusterItems => _clusterItems;
    public ObservableCollection<SpecialKeyOverrideItem> SpecialItems => _specialItems;

    public ICommand KeyClickedCommand => _noopCommand;

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
        AddClusterItem("ESC", KeyboardCluster.EscCluster);
        AddClusterItem("F1-F4", KeyboardCluster.F1ToF4Cluster);
        AddClusterItem("F5-F8", KeyboardCluster.F5ToF8Cluster);
        AddClusterItem("F9-F12", KeyboardCluster.F9ToF12Cluster);
        AddClusterItem("Main Typing", KeyboardCluster.MainTypingCluster);
        AddClusterItem("Navigation", KeyboardCluster.NavigationCluster);
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
    }

    private void OnSpecialItemChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        ApplySpecialOverrides();
        RefreshPreview();
    }

    private void ApplyAllCalibration()
    {
        KeyboardLayoutPanel.SetLayoutCalibration(PreviewKeyUnit, PreviewGap, PreviewOffsetX, PreviewOffsetY);
        KeyboardLayoutPanel.ButtonScale = PreviewButtonScale;
        ApplyClusterCalibration();
        ApplySpecialOverrides();
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

    private ClusterCalibrationItem GetClusterItem(KeyboardCluster cluster)
        => _clusterItems.First(item => item.Cluster == cluster);

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
            _previewButtonScale = 0.8;

            foreach (var cluster in _clusterItems)
            {
                cluster.Reset();
            }

            foreach (var special in _specialItems)
            {
                special.Reset();
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

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

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
}
