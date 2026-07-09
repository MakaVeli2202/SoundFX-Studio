using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

public class Category : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string _accentColor = "#00D4FF";
    private bool _isBuiltIn;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string AccentColor
    {
        get => _accentColor;
        set => SetProperty(ref _accentColor, value);
    }

    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set => SetProperty(ref _isBuiltIn, value);
    }
}
