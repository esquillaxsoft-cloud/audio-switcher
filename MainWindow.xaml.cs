using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Esquillax.AudioSwitcher.Models;
using Esquillax.AudioSwitcher.Services.Audio;
using Esquillax.AudioSwitcher.Services.Hotkey;
using Esquillax.AudioSwitcher.Services.Settings;
using Esquillax.AudioSwitcher.ViewModels;
using Esquillax.AudioSwitcher.Views;

namespace Esquillax.AudioSwitcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly HotkeyManager _hotkeyManager;
    private readonly OsdWindow _osdWindow;
    private bool _isExplicitExit;

    public MainViewModel ViewModel => _viewModel;

    public MainWindow(
        AudioDeviceService audioService,
        SettingsService settingsService,
        HotkeyManager hotkeyManager)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _hotkeyManager = hotkeyManager;
        _osdWindow = new();

        _viewModel = new(audioService, settingsService, hotkeyManager);
        _viewModel.DeviceSwitched += OnDeviceSwitched;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkeyManager.Initialize(hwnd);
        _viewModel.ReconfigureHotkey();
    }

    private void OnDeviceSwitched(AudioDeviceInfo device)
    {
        if (_settingsService.Current.ShowOsd)
        {
            Dispatcher.Invoke(() =>
            {
                _osdWindow.ShowDevice(device, _settingsService.Current.OsdDurationMs);
            });
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsService.Current.MinimizeToTray)
        {
            Hide();
        }
        else
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsService.Current.CloseToTray)
        {
            Hide();
        }
        else
        {
            ExitApplication();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit && _settingsService.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _osdWindow.Close();
        }
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;
        _osdWindow.Close();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    public void ShowAndRestore()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Focus();
    }
}