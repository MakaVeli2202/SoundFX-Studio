using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SoundFXStudio.Services;

namespace SoundFXStudio.Views.Dialogs;

public partial class VoicemeeterPanel : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

    private readonly VoicemeeterRemote _vm = new();
    private DispatcherTimer? _stateTimer;
    private DispatcherTimer? _vuTimer;

    private sealed class StripUi
    {
        public int Index;
        public bool Virtual;
        public TextBox Name = null!;
        public ToggleButton Hear = null!;
        public ToggleButton Team = null!;
        public ToggleButton Mute = null!;
        public Slider Vol = null!;
        public Border VuTrack = null!;
        public Border VuFill = null!;
        public bool Suppress;
    }
    private readonly List<StripUi> _strips = new();

    public VoicemeeterPanel() => InitializeComponent();

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var h = new WindowInteropHelper(this).Handle;
        int p = 2; DwmSetWindowAttribute(h, 33, ref p, Marshal.SizeOf<int>());
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ShowAllCheck.IsChecked = false;

        if (!VoicemeeterRemote.IsInstalled())
        {
            ConnectionStateText.Text = "Unavailable";
            StatusText.Text = "Voicemeeter isn't installed yet.";
            OpenVmBtn.Content = "Install Voicemeeter";
            return;
        }
        if (!_vm.Login())
        {
            ConnectionStateText.Text = "Not connected";
            StatusText.Text = "Couldn't connect to Voicemeeter — click Open Voicemeeter, then reopen this.";
            return;
        }

        BuildStrips();
        RefreshFromVoicemeeter();

        _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _stateTimer.Tick += (_, _) => { if (_vm.IsDirty()) RefreshFromVoicemeeter(); RefreshBusButtons(); };
        _stateTimer.Start();

        _vuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        _vuTimer.Tick += (_, _) => UpdateMeters();
        _vuTimer.Start();

        ConnectionStateText.Text = "Connected";
        StatusText.Text = "Connected to Voicemeeter";
    }

    private void BuildStrips()
    {
        int count = _vm.StripCount();
        int firstVirtual = _vm.FirstVirtualStrip(count);
        bool showAll = ShowAllCheck.IsChecked == true;
        StripsPanel.Children.Clear();
        _strips.Clear();

        for (int i = 0; i < count; i++)
        {
            bool virt = i >= firstVirtual;
            if (!showAll && !virt && i != 0) continue;

            string label = _vm.GetString($"Strip[{i}].Label");
            string name = !string.IsNullOrWhiteSpace(label)
                ? label
                : (virt ? "Apps / Soundboard" : (i == 0 ? "Mic" : $"Input {i + 1}"));
            string hint = virt ? "Your sounds & apps come in here." : "Your microphone.";

            var ui = new StripUi { Index = i, Virtual = virt };
            StripsPanel.Children.Add(BuildCard(name, hint, ui));
            _strips.Add(ui);
        }

        if (_strips.Count == 0)
        {
            StripsPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "No strips available", Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold },
                        new TextBlock { Text = "Voicemeeter is running, but there are no available strips to display yet.", Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0xA0, 0xC0)), FontSize = 12, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap }
                    }
                }
            });
        }
    }

    private Border BuildCard(string name, string hint, StripUi ui)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        badge.Child = new TextBlock { Text = ui.Virtual ? "App / soundboard" : "Mic / input", Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0xA0, 0xC0)), FontSize = 10, FontWeight = FontWeights.SemiBold };

        ui.Name = new TextBox
        {
            Text = name, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(0), CaretBrush = Brushes.White,
            ToolTip = "Click to rename",
        };
        ui.Name.LostFocus += (_, _) => CommitRename(ui);
        ui.Name.KeyDown += (_, ev) => { if (ev.Key == Key.Return) { CommitRename(ui); Keyboard.ClearFocus(); ev.Handled = true; } };
        titleRow.Children.Add(ui.Name);
        titleRow.Children.Add(badge);
        header.Children.Add(titleRow);
        header.Children.Add(new TextBlock { Text = hint, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x92, 0xB8)), FontSize = 10.5, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        ui.VuTrack = new Border
        {
            Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 4, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ui.VuFill = new Border
        {
            Height = 6, CornerRadius = new CornerRadius(3), Width = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new LinearGradientBrush(
                Color.FromRgb(0x10, 0xB9, 0x81), Color.FromRgb(0xF4, 0x3F, 0x5E), 0),
        };
        var vuHost = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        ui.VuTrack.Margin = new Thickness(0);
        vuHost.Children.Add(ui.VuTrack);
        vuHost.Children.Add(ui.VuFill);
        Grid.SetRow(vuHost, 1);
        grid.Children.Add(vuHost);

        var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var toggles = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        ui.Hear = MakeToggle("You hear", 0x35, 0x35, 0xD0);
        ui.Hear.Checked   += (_, _) => { if (!ui.Suppress) _vm.SetFloat($"Strip[{ui.Index}].A1", 1); };
        ui.Hear.Unchecked += (_, _) => { if (!ui.Suppress) _vm.SetFloat($"Strip[{ui.Index}].A1", 0); };

        ui.Team = MakeToggle("Teammates", 0x10, 0xB9, 0x81);
        ui.Team.Checked   += (_, _) => { if (!ui.Suppress) _vm.SetFloat($"Strip[{ui.Index}].B1", 1); };
        ui.Team.Unchecked += (_, _) => { if (!ui.Suppress) _vm.SetFloat($"Strip[{ui.Index}].B1", 0); };

        ui.Mute = MakeToggle("Mute", 0xF4, 0x3F, 0x5E);
        ui.Mute.Checked   += (_, _) => { if (!ui.Suppress) _vm.SetFloat($"Strip[{ui.Index}].Mute", 1); };
        ui.Mute.Unchecked += (_, _) => { if (!ui.Suppress) _vm.SetFloat($"Strip[{ui.Index}].Mute", 0); };

        toggles.Children.Add(ui.Hear);
        toggles.Children.Add(ui.Team);
        toggles.Children.Add(ui.Mute);
        Grid.SetColumn(toggles, 0);
        row.Children.Add(toggles);

        var volWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        volWrap.Children.Add(new TextBlock { Text = "Gain", Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0xA0, 0xC0)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        ui.Vol = new Slider { Minimum = -40, Maximum = 12, Width = 90, VerticalAlignment = VerticalAlignment.Center };
        ui.Vol.ValueChanged += (_, ev) => { if (!ui.Suppress) _vm.SetFloat($"Strip[{ui.Index}].Gain", (float)ev.NewValue); };
        volWrap.Children.Add(ui.Vol);
        Grid.SetColumn(volWrap, 1);
        row.Children.Add(volWrap);

        Grid.SetRow(row, 2);
        grid.Children.Add(row);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 10),
            Child = grid,
        };
    }

    private void CommitRename(StripUi ui)
    {
        string newName = ui.Name.Text.Trim();
        if (string.IsNullOrEmpty(newName)) { ui.Name.Text = _vm.GetString($"Strip[{ui.Index}].Label"); return; }
        _vm.SetStripLabel(ui.Index, newName);
    }

    private static ToggleButton MakeToggle(string text, byte r, byte g, byte b)
    {
        var on = Color.FromRgb(r, g, b);
        var tb = new ToggleButton
        {
            Content = text, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0),
            FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0xA0, 0xC0)),
        };
        var tpl = new ControlTemplate(typeof(ToggleButton));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        bd.SetValue(Border.PaddingProperty, new Thickness(12, 7, 12, 7));
        bd.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));
        bd.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)));
        bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        bd.Name = "Bd";
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        bd.AppendChild(cp);
        tpl.VisualTree = bd;
        var trig = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        trig.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x33, r, g, b)), "Bd"));
        trig.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(on), "Bd"));
        trig.Setters.Add(new Setter(ToggleButton.ForegroundProperty, Brushes.White));
        tpl.Triggers.Add(trig);
        tb.Template = tpl;
        return tb;
    }

    private void RefreshFromVoicemeeter()
    {
        foreach (var s in _strips)
        {
            s.Suppress = true;
            s.Hear.IsChecked = _vm.GetFloat($"Strip[{s.Index}].A1") >= 0.5f;
            s.Team.IsChecked = _vm.GetFloat($"Strip[{s.Index}].B1") >= 0.5f;
            s.Mute.IsChecked = _vm.GetFloat($"Strip[{s.Index}].Mute") >= 0.5f;
            s.Vol.Value      = Math.Clamp(_vm.GetFloat($"Strip[{s.Index}].Gain"), -40, 12);
            if (!s.Name.IsKeyboardFocused)
            {
                string label = _vm.GetString($"Strip[{s.Index}].Label");
                if (!string.IsNullOrWhiteSpace(label) && s.Name.Text != label) s.Name.Text = label;
            }
            s.Suppress = false;
        }
        RefreshBusButtons();
    }

    private void UpdateMeters()
    {
        foreach (var s in _strips)
        {
            float peak = _vm.StripPeak(s.Index);
            double target = s.VuTrack.ActualWidth * peak;
            double cur = s.VuFill.Width;
            s.VuFill.Width = target > cur ? target : cur - (cur - target) * 0.35;
        }
    }

    private void RefreshBusButtons()
    {
        if (!_vm.LoggedIn) return;
        SetMutedVisual(MuteHearBtn, "Mute what I hear", "Hear: MUTED", _vm.GetBusMute(_vm.A1Bus));
        SetMutedVisual(MuteTeamBtn, "Mute what team hears", "Team: MUTED", _vm.GetBusMute(_vm.B1Bus));
        SetMutedVisual(MuteAllBtn, "Mute all", "All: MUTED", !_vm.AnyStripUnmuted());
    }

    private static void SetMutedVisual(Button btn, string offText, string onText, bool muted)
    {
        var content = (btn.Content as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault();
        if (content is not null) content.Text = muted ? onText : offText;
    }

    private void MuteHear_Click(object sender, RoutedEventArgs e) { ToggleMuteHear(); RefreshBusButtons(); }
    private void MuteTeam_Click(object sender, RoutedEventArgs e) { ToggleMuteTeam(); RefreshBusButtons(); }
    private void MuteAll_Click(object sender, RoutedEventArgs e)  { ToggleMuteAll(); RefreshFromVoicemeeter(); }

    private bool ToggleMuteHear() { bool m = !_vm.GetBusMute(_vm.A1Bus); _vm.SetBusMute(_vm.A1Bus, m); return m; }
    private bool ToggleMuteTeam() { bool m = !_vm.GetBusMute(_vm.B1Bus); _vm.SetBusMute(_vm.B1Bus, m); return m; }
    private bool ToggleMuteAll()  { bool mute = _vm.AnyStripUnmuted(); _vm.SetAllStripsMute(mute); return mute; }

    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.LoggedIn) { BuildStrips(); RefreshFromVoicemeeter(); }
    }

    private void RemoveInput1_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.LoggedIn) { StatusText.Text = "Not connected to Voicemeeter."; return; }
        _vm.SetString("Strip[0].device.mme", string.Empty);
        _vm.SetString("Strip[0].device.wdm", string.Empty);
        _vm.SetString("Strip[0].device.ks", string.Empty);
        StatusText.Text = "Hardware Input 1 device removed (unselected).";
        RefreshFromVoicemeeter();
    }

    private void RemoveHwOut_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.LoggedIn) { StatusText.Text = "Not connected to Voicemeeter."; return; }
        _vm.SetString("Bus[0].device.mme", string.Empty);
        _vm.SetString("Bus[0].device.wdm", string.Empty);
        _vm.SetString("Bus[0].device.ks", string.Empty);
        _vm.SetString("Bus[0].device.asio", string.Empty);
        StatusText.Text = "Hardware Output A1 device removed (unselected).";
        RefreshFromVoicemeeter();
    }

    private void ResetBoth_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.LoggedIn) { StatusText.Text = "Not connected to Voicemeeter."; return; }
        RemoveInput1_Click(sender, e);
        RemoveHwOut_Click(sender, e);
        StatusText.Text = "Both devices unselected — nothing selected.";
        RefreshFromVoicemeeter();
    }

    private void OpenVm_Click(object sender, RoutedEventArgs e)
    {
        if (!VoicemeeterRemote.IsInstalled())
        {
            StatusText.Text = "Install Voicemeeter, then reopen.";
            return;
        }
        StatusText.Text = VoicemeeterRemote.LaunchHidden()
            ? "Voicemeeter opened (hidden)."
            : "Could not find Voicemeeter.";
    }

    private void TestHear_Click(object sender, RoutedEventArgs e)
    {
        var monitor = new TeamMonitorWindow { Owner = this };
        monitor.ShowDialog();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closed(object sender, EventArgs e)
    {
        _stateTimer?.Stop();
        _vuTimer?.Stop();
        _vm.Dispose();
    }
}
