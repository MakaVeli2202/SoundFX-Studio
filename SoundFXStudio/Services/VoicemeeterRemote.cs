// TODO(caveman): 2026-08-01 voice setup done. Mic->Strip[0] WDM, Speakers->Bus[0] A1 MME.
//   - Fixed GetString [Out] marshaling; added Input/Output device enumeration for exact-name match.
//   - ApplyRouting verifies writes (rc==0); no device.type param exists in API (was causing false failure).
//   - MainViewModel AutoSetup routes Windows defaults to VoiceMeeter Input/Output + app I/O.
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SoundFXStudio.Services;

/// <summary>
/// Thin wrapper around the Voicemeeter native API used by the audio routing and mixer UI.
/// </summary>
public sealed class VoicemeeterRemote : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DLogin();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DLogout();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DRun(int type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetF([MarshalAs(UnmanagedType.LPStr)] string p, ref float v);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DSetF([MarshalAs(UnmanagedType.LPStr)] string p, float v);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetS([MarshalAs(UnmanagedType.LPStr)] string p, [Out] byte[] s);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DDirty();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetType(ref int t);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DSetS([MarshalAs(UnmanagedType.LPStr)] string p, [MarshalAs(UnmanagedType.LPStr)] string s);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetLevel(int type, int channel, ref float v);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate long DGetDeviceNumber();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetDeviceDesc(long zindex, ref long nType, [Out] byte[] szName, [Out] byte[] szHwId);

    private IntPtr _lib;
    private DLogin? _login; private DLogout? _logout; private DRun? _run;
    private DGetF? _getF; private DSetF? _setF; private DGetS? _getS; private DDirty? _dirty;
    private DGetType? _getType; private DSetS? _setS; private DGetLevel? _getLevel;
    private DGetDeviceNumber? _inputDevNum; private DGetDeviceNumber? _outputDevNum;
    private DGetDeviceDesc? _inputDevDesc; private DGetDeviceDesc? _outputDevDesc;

    public bool Available { get; private set; }
    public bool LoggedIn  { get; private set; }
    public string LastDiagnostics { get; private set; } = "";

    public bool Load()
    {
        if (Available) return true;
        var dll = FindDll();
        if (dll is null) return false;
        try
        {
            _lib = NativeLibrary.Load(dll);
            _login  = Get<DLogin>("VBVMR_Login");
            _logout = Get<DLogout>("VBVMR_Logout");
            _run    = Get<DRun>("VBVMR_RunVoicemeeter");
            _getF   = Get<DGetF>("VBVMR_GetParameterFloat");
            _setF   = Get<DSetF>("VBVMR_SetParameterFloat");
            _getS   = Get<DGetS>("VBVMR_GetParameterStringA");
            _dirty  = Get<DDirty>("VBVMR_IsParametersDirty");
            _getType = Get<DGetType>("VBVMR_GetVoicemeeterType");
            _setS    = Get<DSetS>("VBVMR_SetParameterStringA");
            _getLevel = Get<DGetLevel>("VBVMR_GetLevel");
            _inputDevNum = Get<DGetDeviceNumber>("VBVMR_Input_GetDeviceNumber");
            _outputDevNum = Get<DGetDeviceNumber>("VBVMR_Output_GetDeviceNumber");
            _inputDevDesc = Get<DGetDeviceDesc>("VBVMR_Input_GetDeviceDescA");
            _outputDevDesc = Get<DGetDeviceDesc>("VBVMR_Output_GetDeviceDescA");
            Available = true;
            return true;
        }
        catch { return false; }
    }

    private T Get<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_lib, name));

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    private const int SW_HIDE = 0;

    /// <summary>
    /// Connects to the local Voicemeeter instance and prepares the session for parameter access.
    /// </summary>
    public bool Login()
    {
        if (!Available && !Load()) return false;
        int r = _login!();
        if (r == 1)
        {
            _run!(1);
            System.Threading.Thread.Sleep(2000);
            HideVmWindow();
            System.Threading.Thread.Sleep(1000);
            HideVmWindow();
        }
        LoggedIn = r >= 0;
        return LoggedIn;
    }

    /// <summary>
    /// Attempts a lightweight connection without forcing the Voicemeeter UI to be shown.
    /// </summary>
    public bool TryConnect()
    {
        if (!Available && !Load()) return false;
        int r = _login!();
        LoggedIn = r >= 0;
        return LoggedIn;
    }

    public bool IsDirty() => Available && _dirty!() > 0;

    public int Edition()
    {
        int t = 0;
        if (Available) _getType!(ref t);
        return t;
    }

    public int StripCount() => Edition() switch { 3 => 8, 2 => 5, 1 => 3, _ => 0 };
    public int FirstVirtualStrip(int stripCount) => stripCount switch
    {
        8 => 5, 5 => 3, _ => stripCount - 1
    };

    public float GetFloat(string param)
    {
        float v = 0f;
        if (Available) _getF!(param, ref v);
        return v;
    }

    public int SetFloat(string param, float value)
    {
        if (!Available) return -2;
        return _setF!(param, value);
    }

    public int SetString(string param, string value)
    {
        if (!Available) return -2;
        return _setS!(param, value);
    }

    /// <summary>
    /// Applies the selected microphone and output routing to the Voicemeeter strips and buses.
    /// </summary>
    public bool ApplyRouting(string hearDevice, string talkDevice)
    {
        LastDiagnostics = "";
        if (!Available && !Load()) return false;
        if (!LoggedIn && !Login()) return false;
        if (!WaitUntilReady()) return false;

        int count = StripCount();
        if (count == 0) return false;
        int firstVirtual = FirstVirtualStrip(count);

        var outputs = WaitForOutputDevices();
        var inputs = GetInputDevices();

        bool ok = true;
        if (!string.IsNullOrEmpty(talkDevice))
        {
            var talkMatch = MatchVmDevice(inputs, talkDevice);
            ok &= TrySetDevice("Strip[0].device.name", new[]
            {
                ("Strip[0].device.wdm", talkMatch?.Name ?? talkDevice)
            });
            SetFloat("Strip[0].A1", 0);
            SetFloat("Strip[0].B1", 1);
        }
        if (!string.IsNullOrEmpty(hearDevice))
        {
            ok &= TrySetBusOutputDevice(outputs, hearDevice);
            if (ok)
                SetString("Bus[0].label", "SoundFX");
        }

        for (int i = firstVirtual; i < count; i++)
        {
            SetFloat($"Strip[{i}].A1", 1);
            SetFloat($"Strip[{i}].B1", 1);
        }
        return ok;
    }

    /// <summary>
    /// Clears the selected devices from Voicemeeter and reports any read-back issues.
    /// </summary>
    public string ResetRouting(IReadOnlyCollection<string>? currentInputNames = null,
                               IReadOnlyCollection<string>? currentOutputNames = null)
    {
        LastDiagnostics = "";
        if (!Available && !Load()) return "Voicemeeter DLL not found.";
        if (!LoggedIn && !Login()) return "Could not log in to Voicemeeter.";
        if (!WaitUntilReady()) return "Voicemeeter not ready.";

        int count = StripCount();
        int firstVirtual = FirstVirtualStrip(count);
        var failures = new StringBuilder();

        var deviceParams = new[]
        {
            "Strip[0].device.mme",
            "Strip[0].device.wdm",
            "Strip[0].device.ks",
            "Bus[0].device.mme",
            "Bus[0].device.wdm",
            "Bus[0].device.ks",
            "Bus[0].device.asio"
        };

        foreach (var param in deviceParams)
        {
            int rc = SetString(param, string.Empty);
            if (rc != 0)
                failures.AppendLine($"{param} rc={rc}");
        }

        SetFloat("Strip[0].A1", 0);
        SetFloat("Strip[0].B1", 0);
        SetFloat("Strip[0].gain", 0);
        SetFloat("Bus[0].gain", 0);
        SetFloat("Bus[0].Mute", 0);
        SetString("Bus[0].label", string.Empty);

        for (int i = firstVirtual; i < count; i++)
        {
            SetFloat($"Strip[{i}].A1", 1);
            SetFloat($"Strip[{i}].B1", 0);
            SetFloat($"Strip[{i}].gain", 0);
        }

        string stripDevice = "";
        string busDevice = "";
        var readBackOk = false;
        for (int i = 0; i < 10; i++)
        {
            System.Threading.Thread.Sleep(200);
            stripDevice = GetString("Strip[0].device.name");
            busDevice = GetString("Bus[0].device.name");
            if (string.IsNullOrWhiteSpace(stripDevice) && string.IsNullOrWhiteSpace(busDevice))
            {
                readBackOk = true;
                break;
            }
        }
        if (!readBackOk)
        {
            bool stripGone = currentInputNames is not null &&
                             !string.IsNullOrWhiteSpace(stripDevice) &&
                             !currentInputNames.Contains(stripDevice, StringComparer.OrdinalIgnoreCase);
            bool busGone = currentOutputNames is not null &&
                           !string.IsNullOrWhiteSpace(busDevice) &&
                           !currentOutputNames.Contains(busDevice, StringComparer.OrdinalIgnoreCase);
            if (stripGone || busGone)
            {
                var gone = new List<string>();
                if (stripGone) gone.Add($"Strip[0] '{stripDevice}'");
                if (busGone) gone.Add($"Bus[0] '{busDevice}'");
                LastDiagnostics = string.Join(", ", gone) +
                    " no longer exist in Windows — cleared from SoundFX Studio; Voicemeeter will drop them after restart.";
                return "✓ Voicemeeter reset — removed devices no longer in Windows.";
            }
            failures.AppendLine($"read-back Strip[0]='{stripDevice}' Bus[0]='{busDevice}' (still selected)");
        }

        LastDiagnostics = failures.ToString();
        return failures.Length == 0
            ? "✓ Voicemeeter reset to factory (devices unselected)."
            : "⚠ Some Voicemeeter writes failed:\n" + failures;
    }

    private List<VmDevice> WaitForOutputDevices(int timeoutMs = 10000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var devices = GetOutputDevices();
            if (devices.Count > 0)
                return devices;
            System.Threading.Thread.Sleep(200);
        }
        return GetOutputDevices();
    }

    private bool WaitUntilReady(int timeoutMs = 15000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (Edition() != 0)
                return true;
            System.Threading.Thread.Sleep(250);
        }
        return Edition() != 0;
    }

    private bool TrySetBusOutputDevice(List<VmDevice> outputs, string userDevice)
    {
        var diag = new StringBuilder();
        var match = MatchVmDevice(outputs, userDevice);
        var mmeMatch = MatchVmDevice(outputs.Where(d => d.Types.Contains(1L)), userDevice);

        var wdm = match is not null && match.Types.Contains(3L) ? match.Name : null;
        var mmeName = mmeMatch?.Name;

        bool ok;
        if (!string.IsNullOrEmpty(mmeName))
        {
            int r1 = SetString("Bus[0].device.mme", mmeName);
            diag.AppendLine($"Bus[0].device.mme='{mmeName}' rc={r1}");
            ok = r1 == 0;
            diag.AppendLine(ok ? "MME selected (applies async)" : "MME write rejected");
        }
        else if (!string.IsNullOrEmpty(wdm))
        {
            int r1 = SetString("Bus[0].device.wdm", wdm);
            diag.AppendLine($"Bus[0].device.wdm='{wdm}' rc={r1}");
            ok = r1 == 0;
            diag.AppendLine(ok ? "WDM selected (applies async)" : "WDM write rejected");
        }
        else
        {
            diag.AppendLine("No matching device entry in Voicemeeter enumeration");
            ok = false;
        }

        System.Threading.Thread.Sleep(1200);
        var read = GetString("Bus[0].device.name");
        diag.AppendLine($"read-back '{read}'");
        LastDiagnostics = diag.ToString();
        return ok;
    }

    private bool TrySetDevice(string readParam, IEnumerable<(string Param, string Name)> attempts)
    {
        var diag = new StringBuilder();
        bool ok = false;

        foreach (var (param, name) in attempts)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            int rc = SetString(param, name);
            diag.AppendLine($"{param}='{name}' rc={rc}");
            if (rc == 0)
            {
                ok = true;
                break;
            }
        }

        for (int i = 0; i < 20 && ok; i++)
        {
            System.Threading.Thread.Sleep(100);
            var current = GetString(readParam);
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            diag.AppendLine($"read-back '{current}'");
            break;
        }

        LastDiagnostics = diag.ToString();
        return ok;
    }

    private static VmDevice? MatchVmDevice(IEnumerable<VmDevice> devices, string? userDevice)
    {
        if (string.IsNullOrEmpty(userDevice)) return null;
        foreach (var device in devices)
            if (string.Equals(device.Name, userDevice, StringComparison.OrdinalIgnoreCase))
                return device;
        foreach (var device in devices)
            if (!string.IsNullOrEmpty(device.Name) && userDevice.StartsWith(device.Name, StringComparison.OrdinalIgnoreCase))
                return device;
        foreach (var device in devices)
            if (!string.IsNullOrEmpty(device.Name) && device.Name.StartsWith(userDevice, StringComparison.OrdinalIgnoreCase))
                return device;
        return null;
    }

    public sealed class VmDevice
    {
        public string Name { get; set; } = string.Empty;
        public List<long> Types { get; } = new();
    }

    public List<VmDevice> GetInputDevices()
    {
        return EnumerateDevices(useOutput: false);
    }

    public List<VmDevice> GetOutputDevices()
    {
        return EnumerateDevices(useOutput: true);
    }

    private List<VmDevice> EnumerateDevices(bool useOutput)
    {
        var result = new List<VmDevice>();
        if (!Available) return result;

        var numFn = useOutput ? _outputDevNum : _inputDevNum;
        var descFn = useOutput ? _outputDevDesc : _inputDevDesc;
        if (numFn is null || descFn is null) return result;

        long count = numFn();
        for (long i = 0; i < count; i++)
        {
            long type = 0;
            var name = new byte[512];
            var hw = new byte[512];
            if (descFn(i, ref type, name, hw) == 0)
            {
                string s = ReadCString(name);
                if (s.Length == 0) continue;
                var existing = result.FirstOrDefault(d => string.Equals(d.Name, s, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    existing = new VmDevice { Name = s };
                    result.Add(existing);
                }
                if (!existing.Types.Contains(type))
                    existing.Types.Add(type);
            }
        }
        return result;
    }

    private static string ReadCString(byte[] buffer)
    {
        int n = Array.IndexOf(buffer, (byte)0);
        return Encoding.ASCII.GetString(buffer, 0, n < 0 ? buffer.Length : n).Trim();
    }

    public int BusCount() => Edition() switch { 3 => 8, 2 => 5, 1 => 2, _ => 0 };
    public int A1Bus => 0;
    public int B1Bus => Edition() switch { 3 => 5, 2 => 3, 1 => 1, _ => 1 };

    public bool GetBusMute(int bus) => GetFloat($"Bus[{bus}].Mute") >= 0.5f;
    public void SetBusMute(int bus, bool mute) => SetFloat($"Bus[{bus}].Mute", mute ? 1 : 0);

    public bool GetStripB1(int strip) => GetFloat($"Strip[{strip}].B1") >= 0.5f;
    public void SetStripB1(int strip, bool on) => SetFloat($"Strip[{strip}].B1", on ? 1 : 0);

    public void SetStripLabel(int strip, string label) => SetString($"Strip[{strip}].Label", label ?? "");

    public bool AnyStripUnmuted()
    {
        int n = StripCount();
        for (int i = 0; i < n; i++)
            if (GetFloat($"Strip[{i}].Mute") < 0.5f) return true;
        return false;
    }

    public void SetAllStripsMute(bool mute)
    {
        int n = StripCount();
        for (int i = 0; i < n; i++) SetFloat($"Strip[{i}].Mute", mute ? 1 : 0);
    }

    public float GetLevel(int type, int channel)
    {
        float v = 0f;
        if (Available) _getLevel!(type, channel, ref v);
        return v;
    }

    private int StripChannelBase(int strip)
    {
        int firstVirtual = FirstVirtualStrip(StripCount());
        int b = 0;
        for (int j = 0; j < strip; j++) b += j >= firstVirtual ? 8 : 2;
        return b;
    }

    public float StripPeak(int strip)
    {
        int b = StripChannelBase(strip);
        float amp = Math.Max(GetLevel(1, b), GetLevel(1, b + 1));
        if (amp <= 0.0000001f) return 0f;
        float db = 20f * (float)Math.Log10(amp);
        return Math.Clamp((db + 60f) / 60f, 0f, 1f);
    }

    public string GetString(string param)
    {
        if (!Available) return "";
        var buf = new byte[512];
        if (_getS!(param, buf) != 0) return "";
        return ReadCString(buf);
    }

    public static bool IsInstalled() => FindDll() is not null;

    private static string? FindDll()
    {
        string? dir = null;
        using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
        using (var k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}"))
            if (k?.GetValue("UninstallString") is string u)
                dir = Path.GetDirectoryName(u.Trim('"'));

        foreach (var d in new[] { dir, @"C:\Program Files (x86)\VB\Voicemeeter", @"C:\Program Files\VB\Voicemeeter" })
        {
            if (string.IsNullOrEmpty(d)) continue;
            var dll = Path.Combine(d, Environment.Is64BitProcess ? "VoicemeeterRemote64.dll" : "VoicemeeterRemote.dll");
            if (File.Exists(dll)) return dll;
        }
        return null;
    }

    private static void HideVmWindow()
    {
        foreach (var name in new[] { "voicemeeter8x64", "voicemeeter8", "voicemeeterpro", "voicemeeter" })
            foreach (var p in Process.GetProcessesByName(name))
                if (p.MainWindowHandle != IntPtr.Zero)
                    ShowWindow(p.MainWindowHandle, SW_HIDE);
    }

    public static bool LaunchHidden()
    {
        var exe = FindVmExe();
        if (exe is null) return false;
        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                HideVmWindow();
                await Task.Delay(1500);
                HideVmWindow();
            });
            return true;
        }
        catch { return false; }
    }

    public static string? FindVmExe()
    {
        string? dir = null;
        var uninstallKeys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter Banana {E431F2C7-D220-4C7B-A36B-FBAA507F6AA1}",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter Potato {BDA1746F-3D44-4E5D-BC69-EC0421615921}"
        };

        using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        foreach (var uninstallKey in uninstallKeys)
        {
            using var k = b.OpenSubKey(uninstallKey);
            if (k?.GetValue("UninstallString") is string u)
            {
                dir = Path.GetDirectoryName(u.Trim('"'));
                if (dir != null) break;
            }
        }

        foreach (var d in new[] { dir, @"C:\Program Files (x86)\VB\Voicemeeter", @"C:\Program Files\VB\Voicemeeter", @"C:\Program Files (x86)\VB-Audio\Voicemeeter", @"C:\Program Files\VB-Audio\Voicemeeter" })
        {
            if (string.IsNullOrEmpty(d)) continue;
            foreach (var exe in new[] { "voicemeeter8x64.exe", "voicemeeter8.exe", "voicemeeterpro.exe", "voicemeeter.exe" })
            {
                var p = Path.Combine(d, exe);
                if (File.Exists(p)) return p;
            }
        }
        return null;
    }

    public void Dispose()
    {
        try { if (LoggedIn) _logout?.Invoke(); } catch { }
        if (_lib != IntPtr.Zero) { try { NativeLibrary.Free(_lib); } catch { } _lib = IntPtr.Zero; }
        Available = LoggedIn = false;
    }
}
