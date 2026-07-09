using NAudio.CoreAudioApi;
using SoundFXStudio.Infrastructure;

namespace SoundFXStudio.Models;

public class AudioDeviceInfo : ObservableObject
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _deviceType = string.Empty;
    private string _availability = string.Empty;
    private bool _isDefault;
    private bool _isDefaultCommunication;
    private bool _isInput;
    private bool _isVirtual;
    private DeviceState _state;

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

    public string DeviceType
    {
        get => _deviceType;
        set => SetProperty(ref _deviceType, value);
    }

    public string Availability
    {
        get => _availability;
        set => SetProperty(ref _availability, value);
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }

    public bool IsDefaultCommunication
    {
        get => _isDefaultCommunication;
        set => SetProperty(ref _isDefaultCommunication, value);
    }

    public bool IsInput
    {
        get => _isInput;
        set => SetProperty(ref _isInput, value);
    }

    public bool IsVirtual
    {
        get => _isVirtual;
        set => SetProperty(ref _isVirtual, value);
    }

    public DeviceState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }
}