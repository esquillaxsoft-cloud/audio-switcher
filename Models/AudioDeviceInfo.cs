using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Esquillax.AudioSwitcher.Models
{
    public enum AudioDeviceType
    {
        Speakers,
        Headphones,
        Television,
        DigitalOutput,
        UsbAudio,
        Generic
    }

    public class AudioDeviceInfo : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string? _alias;
        private bool _isDefault;
        private bool _isCommunicationsDefault;
        private bool _isSelectedForToggle;
        private int _toggleOrder;
        private int _volumePercent;
        private bool _isMuted;
        private AudioDeviceType _deviceType;

        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                    UpdateDeviceType();
                }
            }
        }

        public string? Alias
        {
            get => _alias;
            set
            {
                if (_alias != value)
                {
                    _alias = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(HasAlias));
                }
            }
        }

        public string DisplayName => !string.IsNullOrWhiteSpace(_alias) ? _alias : _name;

        public bool HasAlias => !string.IsNullOrWhiteSpace(_alias);

        public bool IsDefault
        {
            get => _isDefault;
            set { if (_isDefault != value) { _isDefault = value; OnPropertyChanged(); } }
        }

        public bool IsCommunicationsDefault
        {
            get => _isCommunicationsDefault;
            set { if (_isCommunicationsDefault != value) { _isCommunicationsDefault = value; OnPropertyChanged(); } }
        }

        public bool IsSelectedForToggle
        {
            get => _isSelectedForToggle;
            set { if (_isSelectedForToggle != value) { _isSelectedForToggle = value; OnPropertyChanged(); } }
        }

        public int ToggleOrder
        {
            get => _toggleOrder;
            set { if (_toggleOrder != value) { _toggleOrder = value; OnPropertyChanged(); } }
        }

        public int VolumePercent
        {
            get => _volumePercent;
            set { if (_volumePercent != value) { _volumePercent = value; OnPropertyChanged(); } }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set { if (_isMuted != value) { _isMuted = value; OnPropertyChanged(); } }
        }

        public AudioDeviceType DeviceType
        {
            get => _deviceType;
            set { if (_deviceType != value) { _deviceType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IconGlyph)); } }
        }

        public string IconGlyph => _deviceType switch
        {
            AudioDeviceType.Headphones => "🎧",
            AudioDeviceType.Television => "📺",
            AudioDeviceType.DigitalOutput => "⚡",
            AudioDeviceType.UsbAudio => "🔌",
            AudioDeviceType.Speakers => "🔊",
            _ => "🔈"
        };

        // todo see if something better exists
        private void UpdateDeviceType()
        {
            string lower = _name.ToLowerInvariant();
            if (lower.Contains("headphone") || lower.Contains("headset") || lower.Contains("earphone") || lower.Contains("fiio") || lower.Contains("airpods"))
            {
                DeviceType = AudioDeviceType.Headphones;
            }
            else if (lower.Contains("tv") || lower.Contains("television") || lower.Contains("qcq") || lower.Contains("samsung") || lower.Contains("lg") || lower.Contains("sony") || lower.Contains("oled") || lower.Contains("bravia"))
            {
                DeviceType = AudioDeviceType.Television;
            }
            else if (lower.Contains("digital") || lower.Contains("spdif") || lower.Contains("optical"))
            {
                DeviceType = AudioDeviceType.DigitalOutput;
            }
            else if (lower.Contains("usb"))
            {
                DeviceType = AudioDeviceType.UsbAudio;
            }
            else
            {
                DeviceType = AudioDeviceType.Speakers;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
