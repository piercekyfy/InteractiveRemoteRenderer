using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public static class WindowsAPI
    {
        public static void MoveMouse(int x, int y, int width, int height)
        {
            int normX = (x * 65535) / width;
            int normY = (y * 65535) / height;

            var input = new WindowsMouseInputWrapper()
            {
                type = 0,
                mi = new WindowsMouseInput()
                {
                    dx = normX,
                    dy = normY,
                    dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK
                }
            };

            uint result = SendInput(1, new[] { input }, Marshal.SizeOf<WindowsMouseInputWrapper>());
        }

        public static void UpdateMouseLeft(bool pressed)
        {
            var input = new WindowsMouseInputWrapper()
            {
                type = 0,
                mi = new WindowsMouseInput()
                {
                    dwFlags = pressed ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<WindowsMouseInputWrapper>());
        }

        public static void UpdateMouseMiddle(bool pressed)
        {
            var input = new WindowsMouseInputWrapper()
            {
                type = 0,
                mi = new WindowsMouseInput()
                {
                    dwFlags = pressed ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<WindowsMouseInputWrapper>());
        }

        public static void UpdateMouseRight(bool pressed)
        {
            var input = new WindowsMouseInputWrapper()
            {
                type = 0,
                mi = new WindowsMouseInput()
                {
                    dwFlags = pressed ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<WindowsMouseInputWrapper>());
        }

        public static void UpdateKey(ushort key, bool pressed)
        {
            var input = new WindowsKeyboardInputWrapper()
            {
                type = 1,
                ki = new WindowsKeyboardInput()
                {
                    wVk = key,
                    dwFlags = pressed ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<WindowsKeyboardInputWrapper>());
        }

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, WindowsMouseInputWrapper[] pInputs, int cbSize);
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, WindowsKeyboardInputWrapper[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsMouseInputWrapper
        {
            public uint type; // 0: mouse, 1: keyboard, 2: hardware
            public WindowsMouseInput mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsKeyboardInputWrapper
        {
            public uint type; // 0: mouse, 1: keyboard, 2: hardware
            public WindowsKeyboardInput ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsMouseInput
        {
            public int dx;
            public int dy;
            public uint mouseData; // scroll wheel, etc.
            public uint dwFlags; // actions
            public uint time; // timestamp, 0: automatic
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsKeyboardInput
        {
            public ushort wVk; // key-code
            public ushort wScan; // hardware location
            public uint dwFlags; // actions
            public uint time; // timestamp, 0: automatic
            public IntPtr dwExtraInfo;
        }

        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput
        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

    }
}
