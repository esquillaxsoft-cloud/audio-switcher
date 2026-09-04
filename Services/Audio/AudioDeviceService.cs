using System;
using System.Collections.Generic;
using System.Linq;
using Esquillax.AudioSwitcher.Models;
using Esquillax.AudioSwitcher.Services.Settings;
using NAudio.CoreAudioApi;

namespace Esquillax.AudioSwitcher.Services.Audio
{
    public class AudioDeviceService : IDisposable
    {
        private readonly MMDeviceEnumerator _enumerator;
        private readonly MMDeviceNotificationClient _notificationClient;
        private readonly SettingsService _settingsService;
        private IPolicyConfig? _policyConfig;
        private bool _isDisposed;

        public event Action? DevicesChanged;
        public event Action<AudioDeviceInfo>? DefaultDeviceChanged;

        public AudioDeviceService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _enumerator = new MMDeviceEnumerator();
            _notificationClient = _enumerator.CreateNotificationClient(true);

            _notificationClient.DefaultDeviceChanged += (s, e) =>
            {
                if (e.Flow == DataFlow.Render && e.Role == Role.Multimedia)
                {
                    var devices = GetActiveDevices();
                    var newDefault = devices.FirstOrDefault(d => string.Equals(d.Id, e.DeviceId, StringComparison.OrdinalIgnoreCase));
                    if (newDefault != null)
                    {
                        DefaultDeviceChanged?.Invoke(newDefault);
                    }
                    DevicesChanged?.Invoke();
                }
            };

            _notificationClient.DeviceStateChanged += (s, e) => DevicesChanged?.Invoke();
            _notificationClient.DeviceAdded += (s, e) => DevicesChanged?.Invoke();
            _notificationClient.DeviceRemoved += (s, e) => DevicesChanged?.Invoke();
            _notificationClient.PropertyValueChanged += (s, e) => DevicesChanged?.Invoke();

            InitPolicyConfig();
        }

        private void InitPolicyConfig()
        {
            try
            {
                _policyConfig = (IPolicyConfig)new _CPolicyConfigClient();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioDeviceService] Failed to init IPolicyConfig: {ex.Message}");
            }
        }

        public List<AudioDeviceInfo> GetActiveDevices()
        {
            var result = new List<AudioDeviceInfo>();

            try
            {
                string defaultMultimediaId = string.Empty;
                string defaultCommunicationsId = string.Empty;

                try
                {
                    using var defaultMultimedia = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    defaultMultimediaId = defaultMultimedia.ID;
                }
                catch { }

                try
                {
                    using var defaultComm = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
                    defaultCommunicationsId = defaultComm.ID;
                }
                catch { }

                var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                var settings = _settingsService.Current;

                foreach (var device in collection)
                {
                    try
                    {
                        string id = device.ID;
                        string name = device.FriendlyName;
                        settings.DeviceAliases.TryGetValue(id, out string? alias);

                        int volume = 0;
                        bool isMuted = false;
                        try
                        {
                            volume = (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                            isMuted = device.AudioEndpointVolume.Mute;
                        }
                        catch { }

                        bool isSelected = settings.SelectedDeviceIds.Contains(id, StringComparer.OrdinalIgnoreCase);

                        var info = new AudioDeviceInfo
                        {
                            Id = id,
                            Name = name,
                            Alias = alias,
                            IsDefault = string.Equals(id, defaultMultimediaId, StringComparison.OrdinalIgnoreCase),
                            IsCommunicationsDefault = string.Equals(id, defaultCommunicationsId, StringComparison.OrdinalIgnoreCase),
                            IsSelectedForToggle = isSelected,
                            VolumePercent = volume,
                            IsMuted = isMuted
                        };

                        result.Add(info);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AudioDeviceService] Error reading device: {ex.Message}");
                    }
                    finally
                    {
                        device.Dispose();
                    }
                }

                // If user has not selected any devices yet, auto-select current default + first other active device
                if (settings.SelectedDeviceIds.Count == 0 && result.Count > 0)
                {
                    var defaultDev = result.FirstOrDefault(d => d.IsDefault) ?? result.First();
                    defaultDev.IsSelectedForToggle = true;
                    settings.SelectedDeviceIds.Add(defaultDev.Id);

                    var secondDev = result.FirstOrDefault(d => !d.IsDefault);
                    if (secondDev != null)
                    {
                        secondDev.IsSelectedForToggle = true;
                        settings.SelectedDeviceIds.Add(secondDev.Id);
                    }

                    _settingsService.Save();
                }

                // Assign toggle order numbers
                int order = 1;
                foreach (var id in settings.SelectedDeviceIds)
                {
                    var dev = result.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (dev != null)
                    {
                        dev.ToggleOrder = order++;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioDeviceService] Error enumerating devices: {ex.Message}");
            }

            return result;
        }

        public AudioDeviceInfo? GetDefaultDevice()
        {
            var devices = GetActiveDevices();
            return devices.FirstOrDefault(d => d.IsDefault);
        }

        public AudioDeviceInfo? GetNextToggleDevice()
        {
            var devices = GetActiveDevices();
            var configured = devices.Where(d => d.IsSelectedForToggle).ToList();

            if (configured.Count == 0)
            {
                return null;
            }

            if (configured.Count == 1)
            {
                return configured[0];
            }

            // Find current default device index in configured list
            int currentIndex = configured.FindIndex(d => d.IsDefault);
            if (currentIndex < 0)
            {
                return configured[0];
            }

            int nextIndex = (currentIndex + 1) % configured.Count;
            return configured[nextIndex];
        }

        public bool SetDefaultDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;

            try
            {
                if (_policyConfig == null)
                {
                    InitPolicyConfig();
                }

                if (_policyConfig != null)
                {
                    _policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
                    _policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);

                    if (_settingsService.Current.SwitchCommunicationsDevice)
                    {
                        _policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioDeviceService] Error setting default endpoint: {ex.Message}");
            }

            return false;
        }

        public AudioDeviceInfo? ToggleNextDevice()
        {
            var nextDevice = GetNextToggleDevice();
            if (nextDevice != null)
            {
                SetDefaultDevice(nextDevice.Id);
                return nextDevice;
            }
            return null;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                try
                {
                    _notificationClient.Dispose();
                    _enumerator.Dispose();
                }
                catch { }
            }
        }
    }
}
