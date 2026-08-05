using System.Windows.Input;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using Xunit;

namespace SoundFXStudio.Tests;

public class HotkeyServiceTests
{
    [Theory]
    [InlineData("BACKSPACE", Key.Back)]
    [InlineData("BACK", Key.Back)]
    [InlineData("ESC", Key.Escape)]
    [InlineData("ENTER", Key.Return)]
    [InlineData("CAPS LOCK", Key.CapsLock)]
    [InlineData("PRINT SCREEN", Key.PrintScreen)]
    [InlineData("SCROLL LOCK", Key.Scroll)]
    [InlineData("NUM LOCK", Key.NumLock)]
    [InlineData("PAGE UP", Key.PageUp)]
    [InlineData("PAGE DOWN", Key.PageDown)]
    [InlineData("MENU", Key.Apps)]
    [InlineData("PAUSE", Key.Pause)]
    [InlineData("SPACE", Key.Space)]
    [InlineData("TAB", Key.Tab)]
    [InlineData("OEM102", Key.Oem102)]
    [InlineData("NUMPAD0", Key.NumPad0)]
    [InlineData("NUMPAD9", Key.NumPad9)]
    [InlineData("F13", Key.F13)]
    [InlineData("A", Key.A)]
    [InlineData("0", Key.D0)]
    [InlineData("9", Key.D9)]
    [InlineData("`", Key.OemTilde)]
    [InlineData("-", Key.OemMinus)]
    [InlineData("=", Key.OemPlus)]
    [InlineData("[", Key.OemOpenBrackets)]
    [InlineData("]", Key.Oem6)]
    [InlineData("\\", Key.Oem5)]
    [InlineData(";", Key.Oem1)]
    [InlineData("'", Key.Oem7)]
    [InlineData(",", Key.OemComma)]
    [InlineData(".", Key.OemPeriod)]
    [InlineData("/", Key.OemQuestion)]
    [InlineData("+", Key.Add)]
    [InlineData("*", Key.Multiply)]
    [InlineData("LEFT", Key.Left)]
    [InlineData("UP", Key.Up)]
    public void TryParseKey_FriendlyNames_ReturnsExpectedKey(string input, Key expected)
    {
        var parsed = HotkeyService.TryParseKey(input, out var key);
        Assert.True(parsed);
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("CTRL+SHIFT+F13")]
    [InlineData("BACKSPACE+F13")]
    [InlineData("!")]
    public void TryParseKey_InvalidInput_ReturnsFalse(string input)
    {
        var parsed = HotkeyService.TryParseKey(input, out _);
        Assert.False(parsed);
    }

    [Theory]
    [InlineData("CTRL+V", Key.V, ModifierKeys.Control)]
    [InlineData("Ctrl + V", Key.V, ModifierKeys.Control)]
    [InlineData("INSERT", Key.Insert, ModifierKeys.None)]
    [InlineData("ALT+SHIFT+F13", Key.F13, ModifierKeys.Alt | ModifierKeys.Shift)]
    [InlineData("WIN+SHIFT+T", Key.T, ModifierKeys.Windows | ModifierKeys.Shift)]
    [InlineData("F9", Key.F9, ModifierKeys.None)]
    public void TryParseHotkey_ReturnsKeyAndModifiers(string input, Key expectedKey, ModifierKeys expectedModifiers)
    {
        var parsed = HotkeyService.TryParseHotkey(input, out var key, out var modifiers);
        Assert.True(parsed);
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedModifiers, modifiers);
    }

    [Theory]
    [InlineData("C+V", Key.C, Key.V)]
    [InlineData("c + v", Key.C, Key.V)]
    [InlineData("A+B", Key.A, Key.B)]
    [InlineData("F1+F2", Key.F1, Key.F2)]
    [InlineData("NUMPAD0+NUMPAD9", Key.NumPad0, Key.NumPad9)]
    public void TryParseChord_TwoBareKeys_ReturnsKeys(string input, Key expectedFirst, Key expectedSecond)
    {
        var parsed = HotkeyService.TryParseChord(input, out var first, out var second);
        Assert.True(parsed);
        Assert.Equal(expectedFirst, first);
        Assert.Equal(expectedSecond, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("V")]
    [InlineData("CTRL+V")]
    [InlineData("C+V+Q")]
    [InlineData("CTRL+SHIFT+F13")]
    public void TryParseChord_InvalidInput_ReturnsFalse(string input)
    {
        var parsed = HotkeyService.TryParseChord(input, out _, out _);
        Assert.False(parsed);
    }

    [Fact]
    public void VoiceChangerToggleKey_Default_ParsesToChord()
    {
        var parsed = HotkeyService.TryParseChord(new Models.AppSettings().VoiceChangerToggleKey, out var first, out var second);
        Assert.True(parsed);
        Assert.Equal(Key.C, first);
        Assert.Equal(Key.V, second);
    }
}
