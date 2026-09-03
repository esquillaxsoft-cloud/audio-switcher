using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using AudioSwitcher.Models;
using AudioSwitcher.Services.Audio;
using AudioSwitcher.Services.Hotkey;
using AudioSwitcher.Services.Settings;
using Forms = System.Windows.Forms;
using WpfApp = System.Windows.Application;

namespace AudioSwitcher
{
    public partial class App : WpfApp
    {
        private const string AppMutexName = "Global\\AudioSwitcher_SingleInstance_Mutex";
        private Mutex? _mutex;
        private Forms.NotifyIcon? _notifyIcon;
        private SettingsService? _settingsService;
        private AudioDeviceService? _audioService;
        private HotkeyManager? _hotkeyManager;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Enforce Single Instance
            _mutex = new Mutex(true, AppMutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                // Already running
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // Initialize services
            _settingsService = new SettingsService();
            _audioService = new AudioDeviceService(_settingsService);
            _hotkeyManager = new HotkeyManager();

            _mainWindow = new MainWindow(_audioService, _settingsService, _hotkeyManager);

            // Setup Tray Icon
            SetupTrayIcon();

            // Track active device changes to update tray tooltip and menu
            _audioService.DevicesChanged += UpdateTrayState;
            _audioService.DefaultDeviceChanged += dev => UpdateTrayState();

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
            _notifyIcon = new Forms.NotifyIcon
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
                    _notifyIcon.Icon = new Icon(iconPath);
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
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    _mainWindow?.Dispatcher.Invoke(() =>
                    {
                        _mainWindow.ViewModel.ExecuteToggleSwitch();
                    });
                }
            };

            _notifyIcon.DoubleClick += (s, e) =>
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
            if (_notifyIcon == null || _audioService == null || _mainWindow == null) return;

            var menu = new Forms.ContextMenuStrip();

            // Quick Toggle Item
            var toggleItem = new Forms.ToolStripMenuItem("⇄ Switch to Next Device", null, (s, e) =>
            {
                _mainWindow.Dispatcher.Invoke(() => _mainWindow.ViewModel.ExecuteToggleSwitch());
            })
            {
                Font = new Font(menu.Font, System.Drawing.FontStyle.Bold)
            };
            menu.Items.Add(toggleItem);
            menu.Items.Add(new Forms.ToolStripSeparator());

            // Active devices list
            var devices = _audioService.GetActiveDevices();
            var currentDefault = devices.FirstOrDefault(d => d.IsDefault);

            foreach (var dev in devices)
            {
                var item = new Forms.ToolStripMenuItem(dev.DisplayName, null, (s, e) =>
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
            menu.Items.Add(new Forms.ToolStripMenuItem("Open AudioSwitcher", null, (s, e) =>
            {
                _mainWindow.Dispatcher.Invoke(() => _mainWindow.ShowAndRestore());
            }));

            // Settings
            menu.Items.Add(new Forms.ToolStripMenuItem("Settings", null, (s, e) =>
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.ViewModel.IsSettingsOpen = true;
                    _mainWindow.ShowAndRestore();
                });
            }));

            menu.Items.Add(new Forms.ToolStripSeparator());

            // Exit
            menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (s, e) =>
            {
                _mainWindow.Dispatcher.Invoke(() => _mainWindow.ExitApplication());
            }));

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void UpdateTrayState()
        {
            Dispatcher.Invoke(() =>
            {
                if (_notifyIcon != null && _audioService != null)
                {
                    var current = _audioService.GetDefaultDevice();
                    string tip = current != null
                        ? $"Audio: {current.DisplayName}"
                        : "AudioSwitcher";

                    // Limit to 63 chars for Windows NotifyIcon text limit
                    if (tip.Length >= 64)
                    {
                        tip = tip.Substring(0, 60) + "...";
                    }

                    _notifyIcon.Text = tip;
                    RebuildTrayContextMenu();
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            _hotkeyManager?.Dispose();
            _audioService?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();

            base.OnExit(e);
        }
    }
}
