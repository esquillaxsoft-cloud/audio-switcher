using System;
using System.Collections.Generic;

namespace AudioSwitcher.Services.Settings
{
    public class AppSettings
    {
        public List<string> SelectedDeviceIds { get; set; } = new List<string>();
        public Dictionary<string, string> DeviceAliases { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        public bool EnableHotkey { get; set; } = true;
        public uint HotkeyModifiers { get; set; } = 0x0002 | 0x0001; // MOD_CONTROL (2) | MOD_ALT (1)
        public uint HotkeyVirtualKey { get; set; } = 0x41; // 'A' key
        public string HotkeyDisplayText { get; set; } = "Ctrl + Alt + A";

        public bool ShowOsd { get; set; } = true;
        public int OsdDurationMs { get; set; } = 1800;

        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool CloseToTray { get; set; } = true;
        public bool SwitchCommunicationsDevice { get; set; } = true;
    }
}
