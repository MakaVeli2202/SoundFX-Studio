using SoundFXStudio.Infrastructure;
using System.Collections.ObjectModel;

namespace SoundFXStudio.Models;

public class KeyChord : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private Guid _actionId;
    private string _name = string.Empty;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public ObservableCollection<string> Keys { get; set; } = new();

    public Guid ActionId
    {
        get => _actionId;
        set => SetProperty(ref _actionId, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}