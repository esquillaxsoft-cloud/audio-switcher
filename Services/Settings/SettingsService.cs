using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace AudioSwitcher.Services.Settings
{
    public class SettingsService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AudioSwitcher"
        );
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");
        private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "AudioSwitcher";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public AppSettings Current { get; private set; }

        public event Action<AppSettings>? SettingsChanged;

        public SettingsService()
        {
            Current = Load();
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to load settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                string json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);

                UpdateStartupRegistry(Current.StartWithWindows);
                SettingsChanged?.Invoke(Current);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to save settings: {ex.Message}");
            }
        }

        public void UpdateStartupRegistry(bool enableStartup)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
                if (key != null)
                {
                    if (enableStartup)
                    {
                        string? exePath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue(StartupValueName, $"\"{exePath}\" --minimized");
                        }
                    }
                    else
                    {
                        key.DeleteValue(StartupValueName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to update startup registry: {ex.Message}");
            }
        }
    }
}
