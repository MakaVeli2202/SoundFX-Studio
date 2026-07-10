using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ProfileActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;

    public ProfileActionHandler(AppConfig config, ConfigService configService)
    {
        _config = config;
        _configService = configService;
    }

    public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Payload))
        {
            return Task.CompletedTask;
        }

        var profile = _config.Profiles.FirstOrDefault(item => string.Equals(item.Id, action.Payload, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return Task.CompletedTask;
        }

        _config.ActiveProfileId = profile.Id;
        _configService.Save(_config);
        return Task.CompletedTask;
    }
}