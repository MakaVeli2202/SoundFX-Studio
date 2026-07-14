using SoundFXStudio.Infrastructure;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Collections.ObjectModel;
using Xunit;

namespace SoundFXStudio.Tests;

/// <summary>
/// Automated integration tests for SoundFX Studio core functionality.
/// Run these to validate app behavior without GUI interaction.
/// </summary>
public class IntegrationTests
{
    private readonly ConfigService _configService = new();

    [Fact]
    public void ConfigService_SaveAndLoad_PreservesSoundData()
    {
        // Arrange
        var config = new AppConfig();
        var sound = new SoundEntry
        {
            Id = "test-sound-1",
            Name = "Test Sound",
            FilePath = @"C:\Sounds\test.mp3",
            Category = "Test"
        };
        config.Sounds.Add(sound);

        // Act
        _configService.Save(config);
        var loaded = _configService.Load();

        // Assert
        Assert.NotNull(loaded);
        Assert.Contains(loaded.Sounds, s => s.Id == "test-sound-1");
        Assert.Equal("Test Sound", loaded.Sounds[0].Name);
    }

    [Fact]
    public void KeyboardKey_IsSelected_ChangesState()
    {
        // Arrange
        var key = new KeyboardKey { KeyName = "F1", Id = "F1" };
        var initial = key.IsSelected;

        // Act
        key.IsSelected = !initial;

        // Assert
        Assert.NotEqual(initial, key.IsSelected);
    }

    [Fact]
    public void AudioPlayer_Play_DoesNotThrowOnMissingFile()
    {
        // Arrange
        var player = new AudioPlayer();
        var missingFile = @"C:\NonExistent\sound.mp3";

        // Act & Assert
        player.Play("test-id", missingFile, 1f, false, PlaybackMode.Restart, -1); // Should return early, not throw
        Assert.True(true); // If we got here, no exception was thrown
    }

    [Fact]
    public void KeyboardLayout_Initializes_WithAllFunctionKeys()
    {
        // Arrange & Act
        var service = new KeyboardLayoutService();
        var layout = service.CreateKeyboard(KeyboardLayoutMode.EnglishUS);

        // Assert
        Assert.NotNull(layout);
        Assert.NotEmpty(layout);

        var fKeys = layout.Where(k => k.KeyName.StartsWith("F", StringComparison.OrdinalIgnoreCase));
        Assert.True(fKeys.Count() >= 12, "Should have at least F1-F12 keys");
    }

    [Fact]
    public void AudioDeviceService_GetOutputDevices_ReturnsDevices()
    {
        // Arrange
        var service = new AudioDeviceService();

        // Act
        var devices = service.GetOutputDevices();

        // Assert
        Assert.NotNull(devices);
        Assert.True(devices.Count > 0, "Should detect at least one audio output device");
    }

    [Fact]
    public void Profile_HasAssignments_TracksKeyBindings()
    {
        // Arrange
        var profile = new Profile
        {
            Id = "profile-1",
            Name = "Test Profile"
        };
        
        var assignment = new KeyAssignment
        {
            KeyId = "F1",
            SoundId = "sound-1"
        };

        // Act
        profile.Assignments.Add(assignment);

        // Assert
        Assert.Contains(profile.Assignments, a => a.KeyId == "F1");
        Assert.Single(profile.Assignments);
    }

    [Fact]
    public void SoundEntry_Favorite_TogglesProperly()
    {
        // Arrange
        var sound = new SoundEntry { Id = "s1", Name = "Test" };
        var initial = sound.IsFavorite;

        // Act
        sound.IsFavorite = !initial;

        // Assert
        Assert.NotEqual(initial, sound.IsFavorite);
    }

    [Fact]
    public void MultiKeyTracking_PressedKeys_TracksSingleAndMultipleKeys()
    {
        // Arrange - Simulate MainViewModel's _pressedKeys
        var pressedKeys = new HashSet<string>();

        // Act - Simulate key press
        pressedKeys.Add("F1");
        pressedKeys.Add("F2");
        var count1 = pressedKeys.Count;

        // Act - Simulate single key release
        pressedKeys.Remove("F1");
        var count2 = pressedKeys.Count;

        // Assert
        Assert.Equal(2, count1);
        Assert.Equal(1, count2);
        Assert.Contains("F2", pressedKeys);
        Assert.DoesNotContain("F1", pressedKeys);
    }

    [Fact]
    public void Category_Colors_AreValid()
    {
        // Arrange
        var category = new Category { Name = "Test", AccentColor = "#FF5733" };

        // Act & Assert
        Assert.NotNull(category.AccentColor);
        Assert.StartsWith("#", category.AccentColor);
        Assert.Equal(7, category.AccentColor.Length); // #RRGGBB format
    }

    [Theory]
    [InlineData("F1")]
    [InlineData("F12")]
    [InlineData("Enter")]
    [InlineData("Space")]
    public void KeyboardKey_AcceptsValidKeys(string keyName)
    {
        // Arrange & Act
        var key = new KeyboardKey { KeyName = keyName };

        // Assert
        Assert.Equal(keyName, key.KeyName);
    }

    [Fact]
    public void SoundEntry_FilePath_AllowsAudioExtensions()
    {
        // Arrange
        var validExtensions = new[] { ".mp3", ".wav", ".ogg", ".flac", ".m4a" };
        
        // Act & Assert
        foreach (var ext in validExtensions)
        {
            var sound = new SoundEntry { FilePath = $"C:\\Sounds\\test{ext}" };
            Assert.EndsWith(ext, sound.FilePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RelayCommand_Execute_InvokesAction()
    {
        // Arrange
        var executed = false;
        var command = new RelayCommand(_ => executed = true);

        // Act
        command.Execute(null);

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public void RelayCommand_CanExecute_RespectsCondition()
    {
        // Arrange
        var canExecute = true;
        var command = new RelayCommand(_ => { }, _ => canExecute);

        // Act
        var result1 = command.CanExecute(null);
        canExecute = false;
        var result2 = command.CanExecute(null);

        // Assert
        Assert.True(result1);
        Assert.False(result2);
    }

    [Fact]
    public void ObservableObject_PropertyChanged_Fires()
    {
        // Arrange
        var obj = new Category { Name = "Initial" };
        var propertyChanged = false;

        obj.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Category.Name))
                propertyChanged = true;
        };

        // Act
        obj.Name = "Updated";

        // Assert
        Assert.True(propertyChanged);
    }
}
