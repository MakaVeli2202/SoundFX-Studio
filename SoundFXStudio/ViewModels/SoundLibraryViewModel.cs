using Microsoft.Win32;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Views.Dialogs;
using NAudio.Wave;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Data;

namespace SoundFXStudio.ViewModels;

public sealed class SoundLibraryViewModel
{
    private readonly ConfigService _configService;
    private readonly AudioPlayer _audioPlayer;
    private readonly HttpClient _httpClient;
    private readonly ObservableCollection<SoundEntry> _sounds;
    private readonly ObservableCollection<Profile> _profiles;
    private readonly ObservableCollection<KeyboardKey> _keyboardKeys;
    private readonly ObservableCollection<AudioDeviceInfo> _outputDevices;
    private readonly ICollectionView _soundsView;
    private readonly Func<AppConfig> _getConfig;
    private readonly Func<AppSettings> _getSettings;
    private readonly Func<Window?> _getWindow;
    private readonly Func<SoundEntry?> _getSelectedSound;
    private readonly Action<SoundEntry?> _setSelectedSound;
    private readonly Func<KeyboardKey?> _getSelectedKey;
    private readonly Action<KeyboardKey?> _setSelectedKey;
    private readonly Action<string> _setStatusText;
    private readonly Action _save;
    private readonly Action _updateStatus;
    private readonly Action _updateTitle;
    private readonly Action _raiseSoundCollectionStats;
    private readonly Action _refreshAssignments;
    private readonly Action<Action> _runOnUiThread;
    private readonly Func<string, string> _importImage;

    public SoundLibraryViewModel(
        ConfigService configService,
        AudioPlayer audioPlayer,
        HttpClient httpClient,
        ObservableCollection<SoundEntry> sounds,
        ObservableCollection<Profile> profiles,
        ObservableCollection<KeyboardKey> keyboardKeys,
        ObservableCollection<AudioDeviceInfo> outputDevices,
        ICollectionView soundsView,
        Func<AppConfig> getConfig,
        Func<AppSettings> getSettings,
        Func<Window?> getWindow,
        Func<SoundEntry?> getSelectedSound,
        Action<SoundEntry?> setSelectedSound,
        Func<KeyboardKey?> getSelectedKey,
        Action<KeyboardKey?> setSelectedKey,
        Action<string> setStatusText,
        Action save,
        Action updateStatus,
        Action updateTitle,
        Action raiseSoundCollectionStats,
        Action refreshAssignments,
        Action<Action> runOnUiThread,
        Func<string, string> importImage)
    {
        _configService = configService;
        _audioPlayer = audioPlayer;
        _httpClient = httpClient;
        _sounds = sounds;
        _profiles = profiles;
        _keyboardKeys = keyboardKeys;
        _outputDevices = outputDevices;
        _soundsView = soundsView;
        _getConfig = getConfig;
        _getSettings = getSettings;
        _getWindow = getWindow;
        _getSelectedSound = getSelectedSound;
        _setSelectedSound = setSelectedSound;
        _getSelectedKey = getSelectedKey;
        _setSelectedKey = setSelectedKey;
        _setStatusText = setStatusText;
        _save = save;
        _updateStatus = updateStatus;
        _updateTitle = updateTitle;
        _raiseSoundCollectionStats = raiseSoundCollectionStats;
        _refreshAssignments = refreshAssignments;
        _runOnUiThread = runOnUiThread;
        _importImage = importImage;
    }

    public void AddSound()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add Sound",
            Filter = "Audio Files|*.mp3;*.wav;*.flac;*.ogg;*.m4a|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var fileName in dialog.FileNames)
        {
            if (!TryGetSoundDetails(fileName, null, out var details))
            {
                continue;
            }

            AddSoundFromDetails(details);
        }

        _save();
        _updateStatus();
        _raiseSoundCollectionStats();
    }

    public void AddMultipleSounds() => AddSound();

    public Task AddSoundFromUrlAsync()
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox("Paste an audio URL", "Add Sound From URL", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.CompletedTask;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || (uri.Scheme is not "http" and not "https"))
        {
            _setStatusText("Enter a valid http or https audio URL");
            return Task.CompletedTask;
        }

        return ImportSoundFromUrlAsync(uri);
    }

    public void DeleteMarkedSounds()
    {
        var marked = _sounds.Where(item => item.IsMarkedForDelete).ToList();
        if (marked.Count == 0)
        {
            return;
        }

        foreach (var sound in marked)
        {
            _sounds.Remove(sound);
            _getConfig().Sounds.Remove(sound);

            foreach (var profile in _profiles)
            {
                var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase));
                if (assignment is not null)
                {
                    profile.Assignments.Remove(assignment);
                }
            }
        }

        _refreshAssignments();
        _save();
        _updateStatus();
        _soundsView.Refresh();
        _raiseSoundCollectionStats();
    }

    public void DuplicateSelectedSound()
    {
        var selectedSound = _getSelectedSound();
        if (selectedSound is null)
        {
            return;
        }

        var duplicate = new SoundEntry
        {
            Name = $"{selectedSound.Name} Copy",
            FilePath = selectedSound.FilePath,
            Volume = selectedSound.Volume,
            ImagePath = selectedSound.ImagePath,
            Category = selectedSound.Category,
            IsFavorite = selectedSound.IsFavorite,
            Hotkey = string.Empty
        };

        _sounds.Add(duplicate);
        _getConfig().Sounds.Add(duplicate);
        _setSelectedSound(duplicate);
        _save();
    }

    public void DeleteSelectedSound()
    {
        var selectedSound = _getSelectedSound();
        if (selectedSound is null)
        {
            return;
        }

        if (MessageBox.Show($"Delete '{selectedSound.Name}'?", "Delete Sound", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _sounds.Remove(selectedSound);
        _getConfig().Sounds.Remove(selectedSound);

        foreach (var profile in _profiles)
        {
            var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, selectedSound.Id, StringComparison.OrdinalIgnoreCase));
            if (assignment is not null)
            {
                profile.Assignments.Remove(assignment);
            }
        }

        _setSelectedSound(null);
        _refreshAssignments();
        _save();
        _updateStatus();
        _raiseSoundCollectionStats();
    }

    public void RenameSelectedSound(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        var input = Microsoft.VisualBasic.Interaction.InputBox("Sound name", "Rename Sound", sound.Name);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        sound.Name = input.Trim();
        _save();
        _setSelectedSound(sound);
        _setStatusText($"Renamed sound to {sound.Name}");
    }

    public void ChooseSelectedSoundImage(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Choose Sound Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        sound.ImagePath = _importImage(dialog.FileName);
        _save();
        _refreshAssignments();
        _setSelectedSound(sound);
        _setStatusText($"Updated image for {sound.Name}");
    }

    public void PlaySelectedSound()
    {
        var selectedSound = _getSelectedSound();
        if (selectedSound is not null)
        {
            PlaySound(selectedSound);
        }
    }

    public void PlaySoundFromLibrary(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        PlaySound(sound);
    }

    public void ImportSound(string fileName)
    {
        var destinationFolder = _configService.GetSoundsFolder();
        var destinationFile = Path.Combine(destinationFolder, MainViewModel.GetUniqueFileName(destinationFolder, Path.GetFileName(fileName)));
        if (!string.Equals(Path.GetFullPath(fileName), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(fileName, destinationFile, true);
        }

        var sound = new SoundEntry
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            FilePath = destinationFile,
            Category = "Custom"
        };

        _sounds.Add(sound);
        _getConfig().Sounds.Add(sound);
        _soundsView.Refresh();
        _setStatusText($"Imported {sound.Name}");
        _raiseSoundCollectionStats();
    }

    public void AddSoundFromDetails(SoundAssignmentViewModel details)
    {
        if (string.IsNullOrWhiteSpace(details.FilePath) || !File.Exists(details.FilePath))
        {
            return;
        }

        var sourceFile = details.FilePath;
        var destinationFolder = _configService.GetSoundsFolder();
        var destinationFile = Path.Combine(destinationFolder, MainViewModel.GetUniqueFileName(destinationFolder, Path.GetFileName(sourceFile)));
        if (!string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFile, destinationFile, true);
        }

        var sound = new SoundEntry
        {
            Name = string.IsNullOrWhiteSpace(details.Name) ? Path.GetFileNameWithoutExtension(destinationFile) : details.Name.Trim(),
            FilePath = destinationFile,
            Category = "Custom",
            Volume = (float)Math.Clamp(details.VolumePercent / 100.0, 0.0, 1.0),
            IsFavorite = details.IsFavorite
        };

        if (!string.IsNullOrWhiteSpace(details.ImagePath) && File.Exists(details.ImagePath))
        {
            sound.ImagePath = _importImage(details.ImagePath);
        }

        _sounds.Add(sound);
        _getConfig().Sounds.Add(sound);
        UpdateSoundKeyAssignment(sound, details.SelectedKey);
        UpdateSoundChordAssignment(sound, details.ChordKeys);
        _refreshAssignments();
        _soundsView.Refresh();
        _setSelectedSound(sound);
        _save();
        _setStatusText($"Added {sound.Name}");
        _raiseSoundCollectionStats();
    }

    public void UpdateSoundFromDetails(SoundEntry sound, SoundAssignmentViewModel details)
    {
        if (string.IsNullOrWhiteSpace(details.FilePath) || !File.Exists(details.FilePath))
        {
            return;
        }

        if (File.Exists(details.FilePath) && !string.Equals(Path.GetFullPath(details.FilePath), Path.GetFullPath(sound.FilePath), StringComparison.OrdinalIgnoreCase))
        {
            var destinationFolder = _configService.GetSoundsFolder();
            var destinationFile = Path.Combine(destinationFolder, MainViewModel.GetUniqueFileName(destinationFolder, Path.GetFileName(details.FilePath)));
            if (!string.Equals(Path.GetFullPath(details.FilePath), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(details.FilePath, destinationFile, true);
            }

            sound.FilePath = destinationFile;
        }

        sound.Name = string.IsNullOrWhiteSpace(details.Name) ? sound.Name : details.Name.Trim();
        sound.Volume = (float)Math.Clamp(details.VolumePercent / 100.0, 0.0, 1.0);
        sound.IsFavorite = details.IsFavorite;

        if (!string.IsNullOrWhiteSpace(details.ImagePath) && File.Exists(details.ImagePath))
        {
            sound.ImagePath = _importImage(details.ImagePath);
        }
        else if (string.IsNullOrWhiteSpace(details.ImagePath))
        {
            sound.ImagePath = null;
        }

        UpdateSoundKeyAssignment(sound, details.SelectedKey);
        UpdateSoundChordAssignment(sound, details.ChordKeys);
        _refreshAssignments();
        _save();
        _setSelectedSound(sound);
        _setStatusText($"Updated {sound.Name}");
        _raiseSoundCollectionStats();
    }

    public bool TryGetSoundDetails(string? initialFilePath, SoundEntry? existingSound, out SoundAssignmentViewModel details)
    {
        details = new SoundAssignmentViewModel();

        if (existingSound is not null)
        {
            details.FilePath = existingSound.FilePath;
            details.ImagePath = existingSound.ImagePath ?? string.Empty;
            details.Name = existingSound.Name;
            details.VolumePercent = existingSound.Volume * 100;
            details.IsFavorite = existingSound.IsFavorite;
            details.SelectedKey = GetAssignedKeyIdForSound(existingSound) ?? string.Empty;

            details.ChordKeys = GetChordKeysForSound(existingSound);
        }

        if (!string.IsNullOrWhiteSpace(initialFilePath) && string.IsNullOrWhiteSpace(details.FilePath))
        {
            details.FilePath = initialFilePath;
            details.Name = Path.GetFileNameWithoutExtension(initialFilePath);
            details.VolumePercent = 100;
        }

        var editor = new SoundAssignmentWindow
        {
            DataContext = details
        };

        var owner = _getWindow();
        if (owner is not null)
        {
            editor.Owner = owner;
        }

        return editor.ShowDialog() == true;
    }

    internal void UpdateSoundKeyAssignment(SoundEntry sound, string? keyId)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var existingAssignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(keyId))
        {
            if (existingAssignment is not null)
            {
                profile.Assignments.Remove(existingAssignment);
            }

            sound.AssignedKeyId = null;
            return;
        }

        var key = _keyboardKeys.FirstOrDefault(item => string.Equals(item.Id, keyId, StringComparison.OrdinalIgnoreCase));
        if (key is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = sound.Id, ActionId = EnsureSoundAction(sound).Id, HotkeyText = key.KeyName };
            profile.Assignments.Add(assignment);
        }
        else
        {
            assignment.SoundId = sound.Id;
            assignment.ActionId = EnsureSoundAction(sound).Id;
            assignment.HotkeyText = key.KeyName;
        }

        if (existingAssignment is not null && !string.Equals(existingAssignment.KeyId, key.Id, StringComparison.OrdinalIgnoreCase))
        {
            profile.Assignments.Remove(existingAssignment);
        }

        sound.AssignedKeyId = key.Id;
        sound.AssignedKeyLabel = key.DisplayLabel;
    }

    internal void UpdateSoundChordAssignment(SoundEntry sound, IReadOnlyList<string> chordKeys)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var tokens = (chordKeys ?? Array.Empty<string>())
            .Select(NormalizeChordToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var soundAction = EnsureSoundAction(sound);

        var existingChord = profile.Assignments.FirstOrDefault(item =>
            string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.ChordKey));

        if (tokens.Count < 2)
        {
            if (existingChord is not null)
            {
                profile.Assignments.Remove(existingChord);
            }

            RemoveSoundChord(profile, soundAction.Id);
            return;
        }

        var firstToken = tokens[0];
        var secondToken = tokens[1];
        var firstKey = _keyboardKeys.FirstOrDefault(item => string.Equals(item.KeyName, firstToken, StringComparison.OrdinalIgnoreCase));
        if (existingChord is not null)
        {
            existingChord.HotkeyText = firstToken;
            existingChord.ChordKey = secondToken;
            existingChord.KeyId = firstKey?.Id ?? existingChord.KeyId;
            existingChord.ActionId = soundAction.Id;
        }
        else
        {
            var assignment = new KeyAssignment
            {
                SoundId = sound.Id,
                ActionId = soundAction.Id,
                HotkeyText = firstToken,
                ChordKey = secondToken,
                KeyId = firstKey?.Id ?? string.Empty
            };
            profile.Assignments.Add(assignment);
        }

        SyncSoundChord(profile, sound, soundAction, tokens);
    }

    private static void SyncSoundChord(Profile profile, SoundEntry sound, ActionDefinition soundAction, IReadOnlyList<string> keys)
    {
        var chord = profile.KeyChords.FirstOrDefault(item => item.ActionId == soundAction.Id);
        if (chord is null)
        {
            chord = new KeyChord { Name = sound.Name, ActionId = soundAction.Id };
            profile.KeyChords.Add(chord);
        }

        chord.Name = sound.Name;
        chord.Keys.Clear();
        foreach (var key in keys)
        {
            chord.Keys.Add(key);
        }
    }

    private static void RemoveSoundChord(Profile profile, Guid soundActionId)
    {
        var chord = profile.KeyChords.FirstOrDefault(item => item.ActionId == soundActionId);
        if (chord is not null)
        {
            profile.KeyChords.Remove(chord);
        }
    }

    internal KeyAssignment? GetChordForSound(SoundEntry sound)
        => ActiveProfile?.Assignments.FirstOrDefault(item =>
            string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.ChordKey));

    internal IReadOnlyList<string> GetChordKeysForSound(SoundEntry sound)
    {
        var action = _getConfig().Actions.FirstOrDefault(item =>
            item.Type == ActionType.Sound && string.Equals(item.Payload, sound.Id, StringComparison.OrdinalIgnoreCase));
        if (action is not null)
        {
            var chord = ActiveProfile?.KeyChords.FirstOrDefault(item => item.ActionId == action.Id);
            if (chord is not null && chord.Keys.Count > 0)
            {
                return chord.Keys.ToList();
            }
        }

        var legacy = GetChordForSound(sound);
        if (legacy is null)
        {
            return Array.Empty<string>();
        }

        var keys = new List<string>();
        if (!string.IsNullOrWhiteSpace(legacy.HotkeyText)) keys.Add(NormalizeChordToken(legacy.HotkeyText));
        if (!string.IsNullOrWhiteSpace(legacy.ChordKey)) keys.Add(NormalizeChordToken(legacy.ChordKey));
        return keys;
    }

    private static string NormalizeChordToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var parts = token.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? string.Empty : parts[^1].Trim().ToUpperInvariant();
    }

    internal string? GetAssignedKeyIdForSound(SoundEntry sound)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return null;
        }

        return profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase))?.KeyId;
    }

    private void PlaySound(SoundEntry sound, KeyAssignment? assignment = null)
    {
        if (!File.Exists(sound.FilePath))
        {
            _setStatusText($"Missing file: {sound.Name}");
            return;
        }

        var deviceId = _getSettings().OutputDeviceId;
        var deviceIndex = ResolveOutputDeviceIndex(deviceId);

        _audioPlayer.Play(
            sound.Id,
            sound.FilePath,
            assignment?.VolumeOverride ?? sound.Volume,
            PlaybackMode.Restart,
            deviceIndex);

        _runOnUiThread(() =>
        {
            sound.PlayCount++;
            sound.LastPlayedUtc = DateTime.UtcNow;
            _raiseSoundCollectionStats();
            _setStatusText($"Playing {sound.Name}");
            _updateTitle();
        });

        _ = TrackPlaybackAsync(sound.Id, assignment?.KeyId);
    }

    private int ResolveOutputDeviceIndex(string deviceId)
    {
        var device = _outputDevices.FirstOrDefault(item => string.Equals(item.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (device is null)
        {
            return -1;
        }

        if (string.IsNullOrWhiteSpace(device.Name))
        {
            return -1;
        }

        try
        {
            var name = device.Name.Trim();
            for (var i = 0; i < WaveOut.DeviceCount; i++)
            {
                var productName = WaveOut.GetCapabilities(i).ProductName;
                var truncated = name.Length > 31 ? name[..31] : name;
                if (productName.StartsWith(truncated, StringComparison.OrdinalIgnoreCase) ||
                    truncated.StartsWith(productName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        catch
        {
            // fall back to the default device
        }

        return -1;
    }

    private async Task ImportSoundFromUrlAsync(Uri uri)
    {
        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var destinationFolder = _configService.GetSoundsFolder();
            var fileName = ResolveFileNameFromUrl(uri, response);
            var destinationFile = Path.Combine(destinationFolder, MainViewModel.GetUniqueFileName(destinationFolder, fileName));

            await using (var sourceStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var destinationStream = File.Create(destinationFile))
            {
                await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);
            }

            var sound = new SoundEntry
            {
                Name = Path.GetFileNameWithoutExtension(destinationFile),
                FilePath = destinationFile,
                Category = "Custom"
            };

            _runOnUiThread(() =>
            {
                _sounds.Add(sound);
                _getConfig().Sounds.Add(sound);
                _soundsView.Refresh();
                _setStatusText($"Imported {sound.Name} from URL");
                _raiseSoundCollectionStats();
                _save();
            });
        }
        catch (Exception ex)
        {
            _runOnUiThread(() => _setStatusText($"URL import failed: {ex.Message}"));
        }
    }

    private SoundEntry? ResolveSound(object? parameter) => parameter as SoundEntry ?? _getSelectedSound();

    private Profile? ActiveProfile => _profiles.FirstOrDefault(item => string.Equals(item.Id, _getConfig().ActiveProfileId, StringComparison.OrdinalIgnoreCase))
        ?? _profiles.FirstOrDefault(item => item.IsDefault)
        ?? _profiles.FirstOrDefault();

    private ActionDefinition EnsureSoundAction(SoundEntry sound)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            throw new InvalidOperationException("No profile available.");
        }

        var action = profile.Actions.FirstOrDefault(item => item.Type == ActionType.Sound && string.Equals(item.Payload, sound.Id, StringComparison.OrdinalIgnoreCase));
        if (action is not null)
        {
            return action;
        }

        action = new ActionDefinition
        {
            Type = ActionType.Sound,
            Name = sound.Name,
            Description = $"Play {sound.Name}",
            Payload = sound.Id,
            Category = sound.Category,
            IconPath = sound.ImagePath ?? string.Empty,
            IsFavorite = sound.IsFavorite,
            PlaybackMode = PlaybackMode.Restart
        };

        profile.Actions.Add(action);
        return action;
    }

    private async Task TrackPlaybackAsync(string soundId, string? keyId)
    {
        while (_audioPlayer.IsPlaying(soundId))
        {
            await Task.Delay(80).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(keyId))
        {
            return;
        }

        _runOnUiThread(() =>
        {
            var key = _keyboardKeys.FirstOrDefault(item => string.Equals(item.Id, keyId, StringComparison.OrdinalIgnoreCase));
            if (key is not null)
            {
                key.State = key.HasAssignment ? KeyState.Assigned : KeyState.Empty;
            }
        });
    }

    internal static string ResolveFileNameFromUrl(Uri uri, HttpResponseMessage response)
    {
        var contentDisposition = response.Content.Headers.ContentDisposition;
        var fromHeader = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        var fileName = string.IsNullOrWhiteSpace(fromHeader)
            ? Path.GetFileName(uri.LocalPath)
            : fromHeader.Trim('"');

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "AudioFile.mp3";
        }

        fileName = string.Concat(fileName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            fileName += ".mp3";
        }

        return fileName;
    }
}
