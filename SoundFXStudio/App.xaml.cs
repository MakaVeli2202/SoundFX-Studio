using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Views.Dialogs;
using System.Windows;

namespace SoundFXStudio;

public partial class App : Application
{
    private readonly ConfigService _configService = new();

    public App()
    {
        Exit += App_Exit;
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        var config = _configService.Load();

        // Seed default sounds if not done yet (handles first run, reinstalls, upgrades)
        if (!config.Settings.DefaultSoundsSeeded || config.Sounds.Count == 0)
        {
            SeedDefaultSounds(config);
            config.Settings.DefaultSoundsSeeded = true;
            _configService.Save(config);
        }

        // Skip setup wizard for testing
        config.Settings.SetupCompleted = true;
        _configService.Save(config);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        try
        {
            var config = _configService.Load();
            
            // Clean up virtual cable routing on app exit
            // Reset VirtualCableDeviceId so the cable is no longer in use
            if (!string.IsNullOrEmpty(config.Settings.VirtualCableDeviceId))
            {
                config.Settings.VirtualCableDeviceId = string.Empty;
                _configService.Save(config);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private void SeedDefaultSounds(AppConfig config)
    {
        // Installer drops bundled sounds into {exeDir}\DefaultSounds\
        var exeDir = System.IO.Path.GetDirectoryName(
            System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName)!;
        var defaultsDir = System.IO.Path.Combine(exeDir, "DefaultSounds");

        // Don't mark as seeded if the folder isn't there yet — leave it to retry
        // on the next launch, once a proper install actually provides it.
        if (!System.IO.Directory.Exists(defaultsDir)) return;

        var soundsDir = _configService.GetSoundsFolder();
        var extensions = new[] { ".mp3", ".wav", ".ogg", ".flac", ".m4a" };
        var files = System.IO.Directory.GetFiles(defaultsDir)
            .Where(f => extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToList();

        // Friendly names keyed by original filename stem
        var friendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1-staubsauger"] = "Vacuum Cleaner",
            ["anime-wow-sound-effect"] = "Anime Wow",
            ["dinosaur-rawr"] = "Dinosaur Rawr",
            ["drdisrespect_neverplaythisshitgameagain_by_taihplays_on_twitch"] = "Never Play This Game Again",
            ["du-bist-gut-genug"] = "Du Bist Gut Genug",
            ["fahhhhhhhhhhhhhh"] = "Fahhhhh",
            ["final_5eb1061d0b29920013151234_148071-online-audio-converter"] = "Final",
            ["halts-maul_J1aU4XQ"] = "Halts Maul",
            ["movie_1"] = "Movie",
            ["perfect-fart"] = "Perfect Fart",
            ["sven_OpcGpxO"] = "Sven",
            ["thats-what-she-said"] = "That's What She Said",
            ["y2mate_d59ywFq"] = "Y2 Mate",
            ["yaaa"] = "Yaaa",
        };

        var hotkeysList = new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" };
        int hotkeyIdx = 0;

        foreach (var file in files)
        {
            var stem = System.IO.Path.GetFileNameWithoutExtension(file);
            var dest = System.IO.Path.Combine(soundsDir, System.IO.Path.GetFileName(file));
            if (!System.IO.File.Exists(dest))
                System.IO.File.Copy(file, dest);

            // Skip if already in config (upgrade-safe)
            if (config.Sounds.Any(s => s.FilePath == dest)) continue;

            if (!friendlyNames.TryGetValue(stem, out string? name))
            {
                name = System.Text.RegularExpressions.Regex.Replace(stem, @"[_\-]", " ");
            }

            string hotkey = hotkeyIdx < hotkeysList.Length ? hotkeysList[hotkeyIdx++] : string.Empty;

            config.Sounds.Add(new SoundEntry
            {
                Id = Guid.NewGuid().ToString(),
                Name = name ?? stem,
                FilePath = dest,
                Volume = 1.0f,
            });
        }
    }
}
