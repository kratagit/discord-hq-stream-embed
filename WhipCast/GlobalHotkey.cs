using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WhipCast
{
    public class GlobalHotkey : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        private HashSet<Keys> _pressedKeys = new HashSet<Keys>();
        private List<Keys> _targetKeys = new List<Keys>();

        public event EventHandler HotkeyPressed;

        public GlobalHotkey()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        public void SetHotkeyString(string hotkeyStr)
        {
            _targetKeys.Clear();
            _pressedKeys.Clear();
            if (string.IsNullOrWhiteSpace(hotkeyStr)) return;

            var parts = hotkeyStr.ToLower().Split('+');
            foreach (var part in parts)
            {
                if (Enum.TryParse(part, true, out Keys k))
                {
                    _targetKeys.Add(k);
                }
                else if (part == "ctrl") _targetKeys.Add(Keys.LControlKey); // Simplification
                else if (part == "alt") _targetKeys.Add(Keys.LMenu);
                else if (part == "shift") _targetKeys.Add(Keys.LShiftKey);
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    _pressedKeys.Add(key);
                    CheckHotkey();
                }
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    _pressedKeys.Remove(key);
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void CheckHotkey()
        {
            if (_targetKeys.Count == 0) return;
            
            // Check if all target keys are pressed
            bool allPressed = _targetKeys.All(k => _pressedKeys.Contains(k) || 
                (k == Keys.LControlKey && (_pressedKeys.Contains(Keys.LControlKey) || _pressedKeys.Contains(Keys.RControlKey))) ||
                (k == Keys.LMenu && (_pressedKeys.Contains(Keys.LMenu) || _pressedKeys.Contains(Keys.RMenu))) ||
                (k == Keys.LShiftKey && (_pressedKeys.Contains(Keys.LShiftKey) || _pressedKeys.Contains(Keys.RShiftKey)))
            );

            if (allPressed)
            {
                // Clear to prevent multiple rapid triggers
                _pressedKeys.Clear();
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookID);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
