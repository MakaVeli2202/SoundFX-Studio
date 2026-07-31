using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ProfileActionHandler : IActionHandler
{
    private readonly Func<AppConfig> _getConfig;
    private readonly ConfigService _configService;

    public ProfileActionHandler(Func<AppConfig> getConfig, ConfigService configService)
    {
        _getConfig = getConfig;
        _configService = configService;
    }

    public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Payload))
        {
            return Task.CompletedTask;
        }

        var config = _getConfig();
        var profile = config.Profiles.FirstOrDefault(item => string.Equals(item.Id, action.Payload, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return Task.CompletedTask;
        }

        config.ActiveProfileId = profile.Id;
        _configService.Save(config);
        return Task.CompletedTask;
    }
}