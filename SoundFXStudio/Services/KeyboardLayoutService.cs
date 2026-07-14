using SoundFXStudio.Models;
using System.Globalization;

namespace SoundFXStudio.Services;

public sealed class KeyboardLayoutService
{
    public IReadOnlyList<KeyboardKey> CreateKeyboard(KeyboardLayoutMode layoutMode)
    {
        if (layoutMode == KeyboardLayoutMode.Automatic)
        {
            layoutMode = KeyboardLayoutMode.EnglishUS;
        }

        var isGerman = layoutMode == KeyboardLayoutMode.German;
        var isEnglishUk = layoutMode == KeyboardLayoutMode.EnglishUK;
        var isIso = isGerman || isEnglishUk;

        var keys = new List<KeyboardKey>();

        AddRow(keys, 0, new[]
        {
            Key("ESC", widthUnits: 1.25), Key("F1"), Key("F2"), Key("F3"), Key("F4"), Key("F5"), Key("F6"),
            Key("F7"), Key("F8"), Key("F9"), Key("F10"), Key("F11"), Key("F12")
        });

        AddRow(keys, 1, new[]
        {
            Key("`", displayLabel: isGerman ? "^" : isEnglishUk ? "¬" : "`"),
            Key("1", displayLabel: "1"), Key("2", displayLabel: "2"), Key("3", displayLabel: "3"), Key("4", displayLabel: "4"), Key("5", displayLabel: "5"), Key("6", displayLabel: "6"), Key("7", displayLabel: "7"), Key("8", displayLabel: "8"), Key("9", displayLabel: "9"), Key("0", displayLabel: "0"),
            Key("-", displayLabel: isGerman ? "ß" : "-"),
            Key("=", displayLabel: isGerman ? "´" : "="),
            Key("BACKSPACE", displayLabel: isGerman ? "Backspace" : "Backspace", widthUnits: 2.0)
        });

        AddRow(keys, 2, new[]
        {
            Key("TAB", displayLabel: "Tab", widthUnits: 1.5),
            Key("Q", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Q" : "Q"),
            Key("W", displayLabel: "W"),
            Key("E", displayLabel: "E"),
            Key("R", displayLabel: "R"),
            Key("T", displayLabel: "T"),
            isGerman ? Key("Y", displayLabel: "Z") : Key("Y", displayLabel: "Y"),
            Key("U", displayLabel: "U"), Key("I", displayLabel: "I"), Key("O", displayLabel: "O"), Key("P", displayLabel: "P"),
            Key("[", displayLabel: isGerman ? "Ü" : "{"),
            Key("]", displayLabel: isGerman ? "+" : "}"),
            Key("\\", displayLabel: isGerman ? "#" : isEnglishUk ? "#" : "\\", widthUnits: isIso ? 1.25 : 1.5)
        });

        AddRow(keys, 3, new[]
        {
            Key("CAPS LOCK", displayLabel: isGerman ? "Feststell" : "Caps", widthUnits: 1.75), Key("A"), Key("S"), Key("D"), Key("F"), Key("G"), Key("H"), Key("J"), Key("K"), Key("L"),
            Key(";", displayLabel: isGerman ? "Ö" : ";"),
            Key("'", displayLabel: isGerman ? "Ä" : "'"),
            Key("ENTER", displayLabel: "↵", widthUnits: 2.25)
        });

        var bottomRow = new List<KeyboardKey>
        {
            Key("SHIFT", displayLabel: "Shift", widthUnits: 2.25)
        };

        if (isIso)
        {
            bottomRow.Add(Key("OEM102", displayLabel: isGerman ? "<" : "\\", widthUnits: 1.25));
            bottomRow.Add(Key("Z", displayLabel: isGerman ? "Y" : "Z"));
        }
        else
        {
            bottomRow.Add(Key("Z", displayLabel: "Z"));
        }

        bottomRow.AddRange(new[]
        {
            Key("X"), Key("C"), Key("V"), Key("B"), Key("N"), Key("M"),
            Key(",", displayLabel: ","), Key(".", displayLabel: "."), Key("/", displayLabel: isGerman ? "-" : "/"),
            Key("SHIFT", displayLabel: "Shift", widthUnits: 2.75)
        });

        AddRow(keys, 4, bottomRow);

        AddRow(keys, 5, new[]
        {
            Key("CTRL", displayLabel: isGerman ? "Strg" : "Ctrl", widthUnits: 1.25),
            Key("WIN", displayLabel: "⊞", widthUnits: 1.25),
            Key("ALT", displayLabel: "Alt", widthUnits: 1.25),
            Key("SPACE", displayLabel: isGerman ? "Leertaste" : "Space", widthUnits: 6.25),
            Key("ALT", displayLabel: isGerman ? "AltGr" : "Alt", widthUnits: 1.25),
            Key("WIN", displayLabel: "⊞", widthUnits: 1.25),
            Key("MENU", displayLabel: isGerman ? "☰" : "☰", widthUnits: 1.25),
            Key("CTRL", displayLabel: isGerman ? "Strg" : "Ctrl", widthUnits: 1.25)
        });

        AddKey(keys, "INSERT", 1, 16.25, displayLabel: isGerman ? "Einfg" : "Insert");
        AddKey(keys, "HOME", 1, 17.25, displayLabel: isGerman ? "Pos1" : "Home");
        AddKey(keys, "PAGE UP", 1, 18.25, displayLabel: isGerman ? "Bild ↑" : "Page ↑");
        AddKey(keys, "PRINT SCREEN", 0, 16.25, displayLabel: isGerman ? "Druck" : "Print");
        AddKey(keys, "SCROLL LOCK", 0, 17.25, displayLabel: isGerman ? "Rollen" : "Scroll");
        AddKey(keys, "PAUSE", 0, 18.25, displayLabel: "Pause");
        AddKey(keys, "DELETE", 2, 16.25, displayLabel: isGerman ? "Entf" : "Delete");
        AddKey(keys, "END", 2, 17.25, displayLabel: isGerman ? "Ende" : "End");
        AddKey(keys, "PAGE DOWN", 2, 18.25, displayLabel: isGerman ? "Bild ↓" : "Page ↓");

        AddKey(keys, "NUM LOCK", 1, 20.25, displayLabel: "Num");
        AddKey(keys, "/", 1, 21.25, displayLabel: "/");
        AddKey(keys, "*", 1, 22.25, displayLabel: "*");
        AddKey(keys, "-", 1, 23.25, displayLabel: "-");
        AddKey(keys, "7", 2, 20.25, displayLabel: "7");
        AddKey(keys, "8", 2, 21.25, displayLabel: "8");
        AddKey(keys, "9", 2, 22.25, displayLabel: "9");
        AddKey(keys, "4", 3, 20.25, displayLabel: "4");
        AddKey(keys, "5", 3, 21.25, displayLabel: "5");
        AddKey(keys, "6", 3, 22.25, displayLabel: "6");
        AddKey(keys, "1", 4, 20.25, displayLabel: "1");
        AddKey(keys, "2", 4, 21.25, displayLabel: "2");
        AddKey(keys, "3", 4, 22.25, displayLabel: "3");
        AddKey(keys, "0", 5, 20.25, displayLabel: "0", widthUnits: 2.0);
        AddKey(keys, ".", 5, 22.25, displayLabel: isGerman ? "," : ".");
        AddKey(keys, "ENTER", 4, 23.25, displayLabel: "↵", widthUnits: 1.0, heightUnits: 2.0);
        AddKey(keys, "+", 2, 23.25, displayLabel: "+", widthUnits: 1.0, heightUnits: 2.0);

        AddKey(keys, "LEFT", 5, 16.75, displayLabel: "←");
        AddKey(keys, "DOWN", 5, 17.75, displayLabel: "↓");
        AddKey(keys, "RIGHT", 5, 18.75, displayLabel: "→");
        AddKey(keys, "UP", 4, 17.75, displayLabel: "↑");

        return keys;
    }

    private static void AddRow(ICollection<KeyboardKey> keys, int rowIndex, IEnumerable<KeyboardKey> rowKeys)
    {
        double column = 0;
        foreach (var key in rowKeys)
        {
            key.RowIndex = rowIndex;
            key.ColumnIndex = column;
            key.Id = NormalizeId(key.KeyName, rowIndex, column);
            keys.Add(key);
            column += Math.Max(1d, key.WidthUnits);
        }
    }

    private static void AddKey(ICollection<KeyboardKey> keys, string name, int rowIndex, double columnIndex, string? displayLabel = null, double widthUnits = 1, double heightUnits = 1)
    {
        var key = Key(name, displayLabel, widthUnits, heightUnits);
        key.DisplayLabel = displayLabel ?? name;
        key.RowIndex = rowIndex;
        key.ColumnIndex = columnIndex;
        key.Id = NormalizeId(key.KeyName, rowIndex, columnIndex);
        keys.Add(key);
    }

    private static KeyboardKey Key(string name, string? displayLabel = null, double widthUnits = 1, double heightUnits = 1)
    {
        return new KeyboardKey
        {
            KeyName = name,
            DisplayLabel = displayLabel ?? name,
            WidthUnits = Math.Max(1d, widthUnits),
            HeightUnits = Math.Max(1d, heightUnits)
        };
    }

    private static string NormalizeId(string name, int rowIndex, double columnIndex)
    {
        var columnToken = columnIndex.ToString("0.##", CultureInfo.InvariantCulture).Replace(".", "_");
        return $"{name}-{rowIndex}-{columnToken}".Replace(" ", string.Empty).Replace("/", "SLASH").Replace("\\", "BACKSLASH");
    }
}