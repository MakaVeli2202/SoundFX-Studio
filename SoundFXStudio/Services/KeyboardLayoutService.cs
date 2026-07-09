using SoundFXStudio.Models;
using System.Globalization;

namespace SoundFXStudio.Services;

public sealed class KeyboardLayoutService
{
    public IReadOnlyList<KeyboardKey> CreateKeyboard(KeyboardLayoutMode layoutMode)
    {
        var keys = new List<KeyboardKey>();

        AddRow(keys, 0, new[]
        {
            Key("ESC", widthUnits: 1.25), Key("F1"), Key("F2"), Key("F3"), Key("F4"), Key("F5"), Key("F6"),
            Key("F7"), Key("F8"), Key("F9"), Key("F10"), Key("F11"), Key("F12")
        });

        AddRow(keys, 1, new[]
        {
            Key("`", displayLabel: layoutMode == KeyboardLayoutMode.German ? "^" : "`"),
            Key("1", displayLabel: "1"), Key("2", displayLabel: "2"), Key("3", displayLabel: "3"), Key("4", displayLabel: "4"), Key("5", displayLabel: "5"), Key("6", displayLabel: "6"), Key("7", displayLabel: "7"), Key("8", displayLabel: "8"), Key("9", displayLabel: "9"), Key("0", displayLabel: "0"),
            Key("-", displayLabel: layoutMode == KeyboardLayoutMode.German ? "ß" : "-"),
            Key("=", displayLabel: layoutMode == KeyboardLayoutMode.German ? "´" : "="),
            Key("BACKSPACE", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Rück" : "Backspace", widthUnits: 2.0)
        });

        AddRow(keys, 2, new[]
        {
            Key("TAB", displayLabel: "Tab", widthUnits: 1.5),
            Key("Q", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Q" : "Q"),
            Key("W", displayLabel: "W"),
            Key("E", displayLabel: "E"),
            Key("R", displayLabel: "R"),
            Key("T", displayLabel: "T"),
            layoutMode == KeyboardLayoutMode.German ? Key("Y", displayLabel: "Z") : Key("Y", displayLabel: "Y"),
            Key("U", displayLabel: "U"), Key("I", displayLabel: "I"), Key("O", displayLabel: "O"), Key("P", displayLabel: "P"),
            Key("[", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Ü" : "{"),
            Key("]", displayLabel: layoutMode == KeyboardLayoutMode.German ? "+" : "}"),
            Key("\\", displayLabel: layoutMode == KeyboardLayoutMode.German ? "#" : "|", widthUnits: 1.5)
        });

        AddRow(keys, 3, new[]
        {
            Key("CAPS LOCK", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Feststell" : "Caps Lock", widthUnits: 1.75), Key("A"), Key("S"), Key("D"), Key("F"), Key("G"), Key("H"), Key("J"), Key("K"), Key("L"),
            Key(";", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Ö" : ";"),
            Key("'", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Ä" : "'"),
            Key("ENTER", displayLabel: "Enter", widthUnits: 2.25)
        });

        var bottomRow = new List<KeyboardKey>
        {
            Key("SHIFT", displayLabel: "Shift", widthUnits: 2.25)
        };

        if (layoutMode == KeyboardLayoutMode.German)
        {
            bottomRow.Add(Key("OEM102", displayLabel: "<", widthUnits: 1.25));
            bottomRow.Add(Key("Z", displayLabel: "Y"));
        }
        else
        {
            bottomRow.Add(Key("Y", displayLabel: "Y"));
        }

        bottomRow.AddRange(new[]
        {
            Key("X"), Key("C"), Key("V"), Key("B"), Key("N"), Key("M"),
            Key(",", displayLabel: ","), Key(".", displayLabel: "."), Key("/", displayLabel: layoutMode == KeyboardLayoutMode.German ? "-" : "/"),
            Key("SHIFT", displayLabel: "Shift", widthUnits: 2.75)
        });

        AddRow(keys, 4, bottomRow);

        AddRow(keys, 5, new[]
        {
            Key("CTRL", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Strg" : "Ctrl", widthUnits: 1.25),
            Key("WIN", displayLabel: "Win", widthUnits: 1.25),
            Key("ALT", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Alt" : "Alt", widthUnits: 1.25),
            Key("SPACE", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Leertaste" : "Space", widthUnits: 6.25),
            Key("ALT", displayLabel: layoutMode == KeyboardLayoutMode.German ? "AltGr" : "Alt", widthUnits: 1.25),
            Key("WIN", displayLabel: "Win", widthUnits: 1.25),
            Key("MENU", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Menü" : "Menu", widthUnits: 1.25),
            Key("CTRL", displayLabel: layoutMode == KeyboardLayoutMode.German ? "Strg" : "Ctrl", widthUnits: 1.25)
        });

        AddKey(keys, "INSERT", 1, 16.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Einfg" : "Insert");
        AddKey(keys, "HOME", 1, 17.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Pos1" : "Home");
        AddKey(keys, "PAGE UP", 1, 18.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Bild↑" : "Page Up");
        AddKey(keys, "PRINT SCREEN", 0, 16.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Druck" : "Print Scrn");
        AddKey(keys, "SCROLL LOCK", 0, 17.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Rollen" : "Scroll Lock");
        AddKey(keys, "PAUSE", 0, 18.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Pause" : "Pause");
        AddKey(keys, "DELETE", 2, 16.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Entf" : "Delete");
        AddKey(keys, "END", 2, 17.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Ende" : "End");
        AddKey(keys, "PAGE DOWN", 2, 18.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Bild↓" : "Page Down");

        AddKey(keys, "NUM LOCK", 1, 20.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "Num" : "Num Lock");
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
        AddKey(keys, ".", 5, 22.25, displayLabel: layoutMode == KeyboardLayoutMode.German ? "," : ".");
        AddKey(keys, "ENTER", 4, 23.25, displayLabel: "Enter", widthUnits: 1.0, heightUnits: 2.0);
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