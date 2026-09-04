using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Esquillax.AudioSwitcher.Models;

namespace Esquillax.AudioSwitcher.Views;

public partial class OsdWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly DispatcherTimer _hideTimer;
    private Storyboard? _showAnimation;
    private Storyboard? _hideAnimation;

    public OsdWindow()
    {
        InitializeComponent();

        _hideTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(1800)
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            HideAnimated();
        };

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

        _showAnimation = FindResource("ShowAnimation") as Storyboard;
        _hideAnimation = FindResource("HideAnimation") as Storyboard;

        if (_hideAnimation is not null)
        {
            _hideAnimation.Completed += (_, _) =>
            {
                Hide();
            };
        }
    }

    public void ShowDevice(AudioDeviceInfo device, int durationMs = 1800)
    {
        TxtDeviceName.Text = device.DisplayName;
        TxtIcon.Text = device.IconGlyph;

        // Position near top-center of primary screen
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        Left = (screenWidth - Width) / 2;
        Top = 40;

        Show();

        _showAnimation?.Begin(this);

        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
        _hideTimer.Start();
    }

    private void HideAnimated()
    {
        _hideAnimation?.Begin(this);
    }
}
