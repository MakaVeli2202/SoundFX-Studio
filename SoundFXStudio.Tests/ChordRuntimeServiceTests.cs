using SoundFXStudio.Models;
using SoundFXStudio.Services;
using Xunit;

namespace SoundFXStudio.Tests;

public class ChordRuntimeServiceTests
{
    [Fact]
    public async Task SingleKey_FiresOnRelease()
    {
        var config = BuildConfig(out var profile, out var actionA, out var _, out var _);
        profile.Assignments.Add(new KeyAssignment { KeyId = "S", ActionId = actionA.Id });

        var executions = new List<Guid>();
        var service = CreateService(config, executions);

        await service.HandleKeyDownAsync("S");
        await service.HandleKeyUpAsync("S");

        Assert.Equal(new[] { actionA.Id }, executions);
    }

    [Fact]
    public async Task MostSpecificChord_WinsOverSingleKey()
    {
        var config = BuildConfig(out var profile, out var actionA, out var actionB, out var _);
        profile.Assignments.Add(new KeyAssignment { KeyId = "S", ActionId = actionA.Id });
        profile.KeyChords.Add(new KeyChord { Name = "S + W", ActionId = actionB.Id, Keys = { "S", "W" } });

        var executions = new List<Guid>();
        var service = CreateService(config, executions);

        await service.HandleKeyDownAsync("S");
        await service.HandleKeyDownAsync("W");
        await service.HandleKeyUpAsync("W");
        await service.HandleKeyUpAsync("S");

        Assert.Equal(new[] { actionB.Id }, executions);
    }

    [Fact]
    public async Task ThreeKeyChord_WinsOverTwoKeyChord_AndSingleKey()
    {
        var config = BuildConfig(out var profile, out var actionA, out var actionB, out var actionC);
        profile.Assignments.Add(new KeyAssignment { KeyId = "Q", ActionId = actionA.Id });
        profile.KeyChords.Add(new KeyChord { Name = "Q + E", ActionId = actionB.Id, Keys = { "Q", "E" } });
        profile.KeyChords.Add(new KeyChord { Name = "Q + E + R", ActionId = actionC.Id, Keys = { "Q", "E", "R" } });

        var executions = new List<Guid>();
        var service = CreateService(config, executions);

        await service.HandleKeyDownAsync("Q");
        await service.HandleKeyDownAsync("E");
        await service.HandleKeyDownAsync("R");
        await service.HandleKeyUpAsync("R");
        await service.HandleKeyUpAsync("E");
        await service.HandleKeyUpAsync("Q");

        Assert.Equal(new[] { actionC.Id }, executions);
    }

    [Fact]
    public async Task Chord_FiresOncePerPressCycle()
    {
        var config = BuildConfig(out var profile, out var _, out var actionB, out var _);
        profile.KeyChords.Add(new KeyChord { Name = "S + W", ActionId = actionB.Id, Keys = { "S", "W" } });

        var executions = new List<Guid>();
        var service = CreateService(config, executions);

        await service.HandleKeyDownAsync("S");
        await service.HandleKeyDownAsync("W");
        await service.HandleKeyUpAsync("W");
        await service.HandleKeyUpAsync("S");
        await service.HandleKeyDownAsync("S");
        await service.HandleKeyDownAsync("W");
        await service.HandleKeyUpAsync("W");
        await service.HandleKeyUpAsync("S");

        Assert.Equal(new[] { actionB.Id, actionB.Id }, executions);
    }

    private static ChordRuntimeService CreateService(AppConfig config, List<Guid> executions)
    {
        return new ChordRuntimeService(
            config,
            token => config.Profiles.SelectMany(profile => profile.Assignments).FirstOrDefault(assignment => assignment.KeyId == token),
            assignment =>
            {
                if (assignment.ActionId is Guid actionId)
                {
                    executions.Add(actionId);
                }

                return Task.CompletedTask;
            },
            actionId =>
            {
                executions.Add(actionId);
                return Task.CompletedTask;
            });
    }

    private static AppConfig BuildConfig(out Profile profile, out ActionDefinition actionA, out ActionDefinition actionB, out ActionDefinition actionC)
    {
        var config = new AppConfig();
        profile = new Profile { Name = "Default" };
        config.Profiles.Add(profile);
        config.ActiveProfileId = profile.Id;

        actionA = new ActionDefinition { Name = "Action A", Type = ActionType.Sound };
        actionB = new ActionDefinition { Name = "Action B", Type = ActionType.Sound };
        actionC = new ActionDefinition { Name = "Action C", Type = ActionType.Sound };
        config.Actions.Add(actionA);
        config.Actions.Add(actionB);
        config.Actions.Add(actionC);

        return config;
    }
}