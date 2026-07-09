using SoundFXStudio.Models;

namespace SoundFXStudio.Models;

public static class KeyboardLayout
{
    public static List<KeyboardKey> Create()
    {
        return new List<KeyboardKey>
        {
            new() { KeyName = "ESC" },

            new() { KeyName = "F1" },
            new() { KeyName = "F2" },
            new() { KeyName = "F3" },
            new() { KeyName = "F4" },
            new() { KeyName = "F5" },
            new() { KeyName = "F6" },
            new() { KeyName = "F7" },
            new() { KeyName = "F8" },
            new() { KeyName = "F9" },
            new() { KeyName = "F10" },
            new() { KeyName = "F11" },
            new() { KeyName = "F12" },

            new() { KeyName = "INSERT" },
            new() { KeyName = "HOME" },
            new() { KeyName = "PGUP" },

            new() { KeyName = "DELETE" },
            new() { KeyName = "END" },
            new() { KeyName = "PGDN" },

            new() { KeyName = "`" },
            new() { KeyName = "1" },
            new() { KeyName = "2" },
            new() { KeyName = "3" },
            new() { KeyName = "4" },
            new() { KeyName = "5" },
            new() { KeyName = "6" },
            new() { KeyName = "7" },
            new() { KeyName = "8" },
            new() { KeyName = "9" },
            new() { KeyName = "0" },

            new() { KeyName = "TAB", WidthUnits = 2 },

            new() { KeyName = "Q" },
            new() { KeyName = "W" },
            new() { KeyName = "E" },
            new() { KeyName = "R" },
            new() { KeyName = "T" },
            new() { KeyName = "Y" },
            new() { KeyName = "U" },
            new() { KeyName = "I" },
            new() { KeyName = "O" },
            new() { KeyName = "P" },

            new() { KeyName = "CAPS", WidthUnits = 2 },

            new() { KeyName = "A" },
            new() { KeyName = "S" },
            new() { KeyName = "D" },
            new() { KeyName = "F" },
            new() { KeyName = "G" },
            new() { KeyName = "H" },
            new() { KeyName = "J" },
            new() { KeyName = "K" },
            new() { KeyName = "L" },

            new() { KeyName = "SHIFT", WidthUnits = 3 },

            new() { KeyName = "Z" },
            new() { KeyName = "X" },
            new() { KeyName = "C" },
            new() { KeyName = "V" },
            new() { KeyName = "B" },
            new() { KeyName = "N" },
            new() { KeyName = "M" },

            new() { KeyName = "CTRL", WidthUnits = 2 },
            new() { KeyName = "ALT", WidthUnits = 2 },

            new() { KeyName = "SPACE", WidthUnits = 6 },

            new() { KeyName = "ALT", WidthUnits = 2 },
            new() { KeyName = "CTRL", WidthUnits = 2 },

            new() { KeyName = "ENTER", WidthUnits = 3 }
        };
    }
}