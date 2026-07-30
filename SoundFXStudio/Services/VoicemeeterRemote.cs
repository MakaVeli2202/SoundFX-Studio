using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SoundFXStudio.Services;

public sealed class VoicemeeterRemote : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DLogin();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DLogout();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DRun(int type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetF([MarshalAs(UnmanagedType.LPStr)] string p, ref float v);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DSetF([MarshalAs(UnmanagedType.LPStr)] string p, float v);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetS([MarshalAs(UnmanagedType.LPStr)] string p, byte[] s);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DDirty();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetType(ref int t);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DSetS([MarshalAs(UnmanagedType.LPStr)] string p, [MarshalAs(UnmanagedType.LPStr)] string s);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DGetLevel(int type, int channel, ref float v);

    private IntPtr _lib;
    private DLogin? _login; private DLogout? _logout; private DRun? _run;
    private DGetF? _getF; private DSetF? _setF; private DGetS? _getS; private DDirty? _dirty;
    private DGetType? _getType; private DSetS? _setS; private DGetLevel? _getLevel;

    public bool Available { get; private set; }
    public bool LoggedIn  { get; private set; }

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
            Available = true;
            return true;
        }
        catch { return false; }
    }

    private T Get<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_lib, name));

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    private const int SW_MINIMIZE = 6;

    public bool Login()
    {
        if (!Available && !Load()) return false;
        int r = _login!();
        if (r == 1)
        {
            _run!(1);
            System.Threading.Thread.Sleep(1200);
            try
            {
                var hwnd = FindVmWindow();
                if (hwnd != IntPtr.Zero) ShowWindow(hwnd, SW_MINIMIZE);
            }
            catch { }
        }
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

    public void SetFloat(string param, float value)
    {
        if (Available) _setF!(param, value);
    }

    public void SetString(string param, string value)
    {
        if (Available) _setS!(param, value);
    }

    public bool ApplyRouting(string hearDevice, string talkDevice)
    {
        if (!Available && !Load()) return false;
        if (!LoggedIn && !Login()) return false;

        int count = StripCount();
        if (count == 0) return false;
        int firstVirtual = FirstVirtualStrip(count);

        if (!string.IsNullOrEmpty(hearDevice))
            SetString("Bus[0].device.mme", hearDevice);
        if (!string.IsNullOrEmpty(talkDevice))
            SetString("Strip[0].device.wdm", talkDevice);
        SetFloat("Strip[0].A1", 0);
        SetFloat("Strip[0].B1", 1);
        for (int i = firstVirtual; i < count; i++)
        {
            SetFloat($"Strip[{i}].A1", 1);
            SetFloat($"Strip[{i}].B1", 1);
        }
        return true;
    }

    public int BusCount() => Edition() switch { 3 => 8, 2 => 5, 1 => 2, _ => 0 };
    public int A1Bus => 0;
    public int B1Bus => Edition() switch { 3 => 5, 2 => 3, 1 => 1, _ => 1 };

    public bool GetBusMute(int bus) => GetFloat($"Bus[{bus}].Mute") >= 0.5f;
    public void SetBusMute(int bus, bool mute) => SetFloat($"Bus[{bus}].Mute", mute ? 1 : 0);

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
        int n = Array.IndexOf(buf, (byte)0);
        return Encoding.ASCII.GetString(buf, 0, n < 0 ? buf.Length : n).Trim();
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

    private static IntPtr FindVmWindow()
    {
        foreach (var name in new[] { "voicemeeter8x64", "voicemeeter8", "voicemeeterpro", "voicemeeter" })
            foreach (var p in Process.GetProcessesByName(name))
                if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle;
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        try { if (LoggedIn) _logout?.Invoke(); } catch { }
        if (_lib != IntPtr.Zero) { try { NativeLibrary.Free(_lib); } catch { } _lib = IntPtr.Zero; }
        Available = LoggedIn = false;
    }
}
