using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Esquillax.AudioSwitcher.Services.Hotkey
{
    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9001;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        private HwndSource? _hwndSource;
        private IntPtr _windowHandle;
        private bool _isRegistered;
        private bool _isDisposed;

        public event Action? HotkeyPressed;

        public bool IsRegistered => _isRegistered;

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(HwndHook);
        }

        public bool Register(uint modifiers, uint virtualKey)
        {
            if (_windowHandle == IntPtr.Zero) return false;

            if (_isRegistered)
            {
                Unregister();
            }

            // Add MOD_NOREPEAT to prevent spamming when key is held down
            uint modsWithNoRepeat = modifiers | MOD_NOREPEAT;
            _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, modsWithNoRepeat, virtualKey);

            if (!_isRegistered)
            {
                // Try without MOD_NOREPEAT if older OS
                _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, modifiers, virtualKey);
            }

            return _isRegistered;
        }

        public void Unregister()
        {
            if (_isRegistered && _windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
                _isRegistered = false;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                Unregister();
                _hwndSource?.RemoveHook(HwndHook);
            }
        }
    }
}
