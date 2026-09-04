using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Esquillax.AudioSwitcher.Models;
using Esquillax.AudioSwitcher.Services.Audio;
using Esquillax.AudioSwitcher.Services.Hotkey;
using Esquillax.AudioSwitcher.Services.Settings;

namespace Esquillax.AudioSwitcher.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AudioDeviceService _audioService;
    private readonly SettingsService _settingsService;
    private readonly HotkeyManager _hotkeyManager;

    private AudioDeviceInfo? _currentDefaultDevice;
    private AudioDeviceInfo? _nextToggleDevice;
    private bool _isSettingsOpen;
    private AudioDeviceInfo? _editingDevice;
    private string _editAliasText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isRecordingHotkey;

    public ObservableCollection<AudioDeviceInfo> Devices { get; } = [];

    public AppSettings Settings => _settingsService.Current;

    public AudioDeviceInfo? CurrentDefaultDevice
    {
        get => _currentDefaultDevice;
        private set
        {
            if (_currentDefaultDevice != value)
            {
                _currentDefaultDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCurrentDevice));
            }
        }
    }

    public AudioDeviceInfo? NextToggleDevice
    {
        get => _nextToggleDevice;
        private set
        {
            if (_nextToggleDevice != value)
            {
                _nextToggleDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasNextDevice));
                OnPropertyChanged(nameof(SwitchButtonText));
            }
        }
    }

    public bool HasCurrentDevice => _currentDefaultDevice is not null;
    public bool HasNextDevice => _nextToggleDevice is not null;

    public string SwitchButtonText => _nextToggleDevice is not null
        ? $"Switch to: {_nextToggleDevice.DisplayName} ➔"
        : "No other device configured";

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set
        {
            if (_isSettingsOpen != value)
            {
                _isSettingsOpen = value;
                OnPropertyChanged();
            }
        }
    }

    public AudioDeviceInfo? EditingDevice
    {
        get => _editingDevice;
        set
        {
            if (_editingDevice != value)
            {
                _editingDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditingAlias));
            }
        }
    }

    public bool IsEditingAlias => _editingDevice is not null;

    public string EditAliasText
    {
        get => _editAliasText;
        set
        {
            if (_editAliasText != value)
            {
                _editAliasText = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRecordingHotkey
    {
        get => _isRecordingHotkey;
        set
        {
            if (_isRecordingHotkey != value)
            {
                _isRecordingHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    public event Action<AudioDeviceInfo>? DeviceSwitched;

    // Commands
    public ICommand ToggleSwitchCommand { get; }
    public ICommand SelectDeviceCommand { get; }
    public ICommand ToggleDeviceInRotationCommand { get; }
    public ICommand StartEditingAliasCommand { get; }
    public ICommand SaveAliasCommand { get; }
    public ICommand CancelEditingAliasCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand RefreshDevicesCommand { get; }
    public ICommand SaveSettingsCommand { get; }

    public MainViewModel(
        AudioDeviceService audioService,
        SettingsService settingsService,
        HotkeyManager hotkeyManager)
    {
        _audioService = audioService;
        _settingsService = settingsService;
        _hotkeyManager = hotkeyManager;

        ToggleSwitchCommand = new RelayCommand(ExecuteToggleSwitch);
        SelectDeviceCommand = new RelayCommand(ExecuteSelectDevice);
        ToggleDeviceInRotationCommand = new RelayCommand(ExecuteToggleDeviceInRotation);
        StartEditingAliasCommand = new RelayCommand(ExecuteStartEditingAlias);
        SaveAliasCommand = new RelayCommand(ExecuteSaveAlias);
        CancelEditingAliasCommand = new RelayCommand(ExecuteCancelEditingAlias);
        ToggleSettingsCommand = new RelayCommand(() => IsSettingsOpen = !IsSettingsOpen);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        SaveSettingsCommand = new RelayCommand(ExecuteSaveSettings);

        _audioService.DevicesChanged += () => System.Windows.Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _audioService.DefaultDeviceChanged += dev => System.Windows.Application.Current?.Dispatcher.Invoke(() => HandleDefaultDeviceChanged(dev));
        _hotkeyManager.HotkeyPressed += () => System.Windows.Application.Current?.Dispatcher.Invoke(ExecuteToggleSwitch);

        RefreshDevices();
    }

    public void RefreshDevices()
    {
        var activeDevices = _audioService.GetActiveDevices();

        Devices.Clear();
        foreach (var device in activeDevices)
        {
            Devices.Add(device);
        }

        CurrentDefaultDevice = Devices.FirstOrDefault(d => d.IsDefault);
        NextToggleDevice = _audioService.GetNextToggleDevice();

        OnPropertyChanged(nameof(SwitchButtonText));
    }

    private void HandleDefaultDeviceChanged(AudioDeviceInfo dev)
    {
        CurrentDefaultDevice = dev;
        NextToggleDevice = _audioService.GetNextToggleDevice();
        OnPropertyChanged(nameof(SwitchButtonText));
    }

    public void ExecuteToggleSwitch()
    {
        var newDefault = _audioService.ToggleNextDevice();
        if (newDefault is not null)
        {
            RefreshDevices();
            DeviceSwitched?.Invoke(newDefault);
            StatusMessage = $"Switched to {newDefault.DisplayName}";
        }
        else
        {
            StatusMessage = "Please select at least 2 devices for switching.";
        }
    }

    private void ExecuteSelectDevice(object? parameter)
    {
        if (parameter is AudioDeviceInfo device)
        {
            _audioService.SetDefaultDevice(device.Id);
            RefreshDevices();
            DeviceSwitched?.Invoke(device);
            StatusMessage = $"Switched to {device.DisplayName}";
        }
    }

    private void ExecuteToggleDeviceInRotation(object? parameter)
    {
        if (parameter is AudioDeviceInfo device)
        {
            var settings = _settingsService.Current;
            if (device.IsSelectedForToggle)
            {
                if (!settings.SelectedDeviceIds.Contains(device.Id, StringComparer.OrdinalIgnoreCase))
                {
                    settings.SelectedDeviceIds.Add(device.Id);
                }
            }
            else
            {
                settings.SelectedDeviceIds.RemoveAll(id => string.Equals(id, device.Id, StringComparison.OrdinalIgnoreCase));
            }

            _settingsService.Save();
            RefreshDevices();
        }
    }

    private void ExecuteStartEditingAlias(object? parameter)
    {
        if (parameter is AudioDeviceInfo device)
        {
            EditingDevice = device;
            EditAliasText = device.Alias ?? device.Name;
        }
    }

    private void ExecuteSaveAlias()
    {
        if (EditingDevice is not null)
        {
            var settings = _settingsService.Current;
            string newAlias = EditAliasText.Trim();

            if (string.IsNullOrWhiteSpace(newAlias) || string.Equals(newAlias, EditingDevice.Name, StringComparison.OrdinalIgnoreCase))
            {
                EditingDevice.Alias = null;
                settings.DeviceAliases.Remove(EditingDevice.Id);
            }
            else
            {
                EditingDevice.Alias = newAlias;
                settings.DeviceAliases[EditingDevice.Id] = newAlias;
            }

            _settingsService.Save();
            EditingDevice = null;
            EditAliasText = string.Empty;
            RefreshDevices();
        }
    }

    private void ExecuteCancelEditingAlias()
    {
        EditingDevice = null;
        EditAliasText = string.Empty;
    }

    private void ExecuteSaveSettings()
    {
        _settingsService.Save();
        ReconfigureHotkey();
        StatusMessage = "Settings saved";
        IsSettingsOpen = false;
    }

    public void ReconfigureHotkey()
    {
        _hotkeyManager.Unregister();
        if (Settings.EnableHotkey)
        {
            bool registered = _hotkeyManager.Register(Settings.HotkeyModifiers, Settings.HotkeyVirtualKey);
            if (!registered)
            {
                StatusMessage = $"Could not register hotkey {Settings.HotkeyDisplayText} (may be in use)";
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }
}
