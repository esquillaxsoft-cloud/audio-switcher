using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Esquillax.AudioSwitcher.Models;
using Esquillax.AudioSwitcher.Services.Audio;
using Esquillax.AudioSwitcher.Services.Hotkey;
using Esquillax.AudioSwitcher.Services.Settings;
using Forms = System.Windows.Forms;
using WpfApp = System.Windows.Application;

namespace Esquillax.AudioSwitcher;

public partial class App : WpfApp
{
    private const string AppMutexName = "Global\\Esquillax_AudioSwitcher_SingleInstance_Mutex";
    private Mutex? _mutex;
    private bool _ownsMutex;
    private Forms.NotifyIcon? _notifyIcon;
    private SettingsService? _settingsService;
    private AudioDeviceService? _audioService;
    private HotkeyManager? _hotkeyManager;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enforce Single Instance
        _mutex = new Mutex(true, AppMutexName, out bool isNewInstance);
        _ownsMutex = isNewInstance;
        if (!isNewInstance)
        {
            // Already running
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Initialize services
        _settingsService = new();
        _audioService = new(_settingsService);
        _hotkeyManager = new();

        _mainWindow = new(_audioService, _settingsService, _hotkeyManager);

        // Setup Tray Icon
        SetupTrayIcon();

        // Track active device changes to update tray tooltip and menu
        _audioService.DevicesChanged += UpdateTrayState;
        _audioService.DefaultDeviceChanged += _ => UpdateTrayState();

        UpdateTrayState();

        bool startMinimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase)
                              || _settingsService.Current.StartMinimized;

        if (!startMinimized)
        {
            _mainWindow.Show();
        }
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new()
        {
            Visible = true,
            Text = "AudioSwitcher"
        };

        // Load icon
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons", "app.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new(iconPath);
            }
            else
            {
                // Fallback to executable icon
                _notifyIcon.Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        // Left Click -> Instant Toggle!
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.ViewModel.ExecuteToggleSwitch();
                });
            }
        };

        _notifyIcon.DoubleClick += (_, _) =>
        {
            _mainWindow?.Dispatcher.Invoke(() =>
            {
                _mainWindow.ShowAndRestore();
            });
        };

        RebuildTrayContextMenu();
    }

    private void RebuildTrayContextMenu()
    {
        if (_notifyIcon is null || _audioService is null || _mainWindow is null) return;

        Forms.ContextMenuStrip menu = new();

        // Quick Toggle Item
        Forms.ToolStripMenuItem toggleItem = new("⇄ Switch to Next Device", null, (_, _) =>
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.ViewModel.ExecuteToggleSwitch());
        })
        {
            Font = new(menu.Font, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(toggleItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        // Active devices list
        var devices = _audioService.GetActiveDevices();

        foreach (var dev in devices)
        {
            Forms.ToolStripMenuItem item = new(dev.DisplayName, null, (_, _) =>
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _audioService.SetDefaultDevice(dev.Id);
                    _mainWindow.ViewModel.RefreshDevices();
                });
            })
            {
                Checked = dev.IsDefault
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Forms.ToolStripSeparator());

        // Open Window
        menu.Items.Add(new Forms.ToolStripMenuItem("Open AudioSwitcher", null, (_, _) =>
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.ShowAndRestore());
        }));

        // Settings
        menu.Items.Add(new Forms.ToolStripMenuItem("Settings", null, (_, _) =>
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.ViewModel.IsSettingsOpen = true;
                _mainWindow.ShowAndRestore();
            });
        }));

        menu.Items.Add(new Forms.ToolStripSeparator());

        // Exit
        menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (_, _) =>
        {
            _mainWindow.Dispatcher.Invoke(() => _mainWindow.ExitApplication());
        }));

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void UpdateTrayState()
    {
        Dispatcher.Invoke(() =>
        {
            if (_notifyIcon is not null && _audioService is not null)
            {
                var current = _audioService.GetDefaultDevice();
                string tip = current is not null
                    ? $"Audio: {current.DisplayName}"
                    : "AudioSwitcher";

                // Limit to 63 chars for Windows NotifyIcon text limit
                if (tip.Length >= 64)
                {
                    tip = $"{tip[..60]}...";
                }

                _notifyIcon.Text = tip;
                RebuildTrayContextMenu();
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _hotkeyManager?.Dispose();
        _audioService?.Dispose();

        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // Ignore if mutex ownership had already expired
            }
        }
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
