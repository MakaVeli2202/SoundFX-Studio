using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public interface IActionHandler
{
    Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken);
}