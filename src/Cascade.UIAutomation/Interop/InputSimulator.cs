using System.Runtime.InteropServices;

namespace Cascade.UIAutomation.Interop;

/// <summary>
/// Provides input simulation capabilities for mouse and keyboard operations.
/// </summary>
public static class InputSimulator
{
    #region P/Invoke Declarations

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public INPUTUNION Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;
        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
        [FieldOffset(0)]
        public HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    // Input types
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    // Mouse event flags
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    // Keyboard event flags
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    #endregion

    /// <summary>
    /// Moves the mouse cursor to the specified screen coordinates.
    /// </summary>
    public static void MoveTo(int x, int y)
    {
        SetCursorPos(x, y);
    }

    /// <summary>
    /// Gets the current mouse cursor position.
    /// </summary>
    public static (int X, int Y) GetCursorPosition()
    {
        GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    /// <summary>
    /// Performs a left mouse click at the current cursor position.
    /// </summary>
    public static void LeftClick()
    {
        var inputs = new INPUT[]
        {
            CreateMouseInput(MOUSEEVENTF_LEFTDOWN),
            CreateMouseInput(MOUSEEVENTF_LEFTUP)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Performs a left mouse click at the specified screen coordinates.
    /// </summary>
    public static void LeftClick(int x, int y)
    {
        MoveTo(x, y);
        Thread.Sleep(10); // Small delay to ensure cursor position is updated
        LeftClick();
    }

    /// <summary>
    /// Performs a right mouse click at the current cursor position.
    /// </summary>
    public static void RightClick()
    {
        var inputs = new INPUT[]
        {
            CreateMouseInput(MOUSEEVENTF_RIGHTDOWN),
            CreateMouseInput(MOUSEEVENTF_RIGHTUP)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Performs a right mouse click at the specified screen coordinates.
    /// </summary>
    public static void RightClick(int x, int y)
    {
        MoveTo(x, y);
        Thread.Sleep(10);
        RightClick();
    }

    /// <summary>
    /// Performs a middle mouse click at the current cursor position.
    /// </summary>
    public static void MiddleClick()
    {
        var inputs = new INPUT[]
        {
            CreateMouseInput(MOUSEEVENTF_MIDDLEDOWN),
            CreateMouseInput(MOUSEEVENTF_MIDDLEUP)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Performs a middle mouse click at the specified screen coordinates.
    /// </summary>
    public static void MiddleClick(int x, int y)
    {
        MoveTo(x, y);
        Thread.Sleep(10);
        MiddleClick();
    }

    /// <summary>
    /// Performs a double left mouse click at the current cursor position.
    /// </summary>
    public static void DoubleClick()
    {
        LeftClick();
        Thread.Sleep(50);
        LeftClick();
    }

    /// <summary>
    /// Performs a double left mouse click at the specified screen coordinates.
    /// </summary>
    public static void DoubleClick(int x, int y)
    {
        MoveTo(x, y);
        Thread.Sleep(10);
        DoubleClick();
    }

    /// <summary>
    /// Presses and holds the left mouse button.
    /// </summary>
    public static void LeftDown()
    {
        var input = CreateMouseInput(MOUSEEVENTF_LEFTDOWN);
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Releases the left mouse button.
    /// </summary>
    public static void LeftUp()
    {
        var input = CreateMouseInput(MOUSEEVENTF_LEFTUP);
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Scrolls the mouse wheel.
    /// </summary>
    /// <param name="delta">The amount to scroll. Positive values scroll up, negative values scroll down.</param>
    public static void Scroll(int delta)
    {
        var input = new INPUT
        {
            Type = INPUT_MOUSE,
            Union = new INPUTUNION
            {
                Mouse = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_WHEEL,
                    mouseData = (uint)(delta * 120) // 120 is the standard wheel delta
                }
            }
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Types the specified text using keyboard simulation.
    /// </summary>
    /// <param name="text">The text to type.</param>
    /// <param name="delayBetweenKeys">Optional delay between key presses in milliseconds.</param>
    public static void TypeText(string text, int delayBetweenKeys = 0)
    {
        foreach (char c in text)
        {
            TypeCharacter(c);
            if (delayBetweenKeys > 0)
                Thread.Sleep(delayBetweenKeys);
        }
    }

    /// <summary>
    /// Types a single character.
    /// </summary>
    private static void TypeCharacter(char c)
    {
        var inputs = new INPUT[]
        {
            new INPUT
            {
                Type = INPUT_KEYBOARD,
                Union = new INPUTUNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE
                    }
                }
            },
            new INPUT
            {
                Type = INPUT_KEYBOARD,
                Union = new INPUTUNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                    }
                }
            }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Presses a key.
    /// </summary>
    /// <param name="virtualKeyCode">The virtual key code.</param>
    public static void KeyDown(ushort virtualKeyCode)
    {
        var input = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Union = new INPUTUNION
            {
                Keyboard = new KEYBDINPUT
                {
                    wVk = virtualKeyCode,
                    dwFlags = IsExtendedKey(virtualKeyCode) ? KEYEVENTF_EXTENDEDKEY : 0
                }
            }
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Releases a key.
    /// </summary>
    /// <param name="virtualKeyCode">The virtual key code.</param>
    public static void KeyUp(ushort virtualKeyCode)
    {
        var input = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Union = new INPUTUNION
            {
                Keyboard = new KEYBDINPUT
                {
                    wVk = virtualKeyCode,
                    dwFlags = KEYEVENTF_KEYUP | (IsExtendedKey(virtualKeyCode) ? KEYEVENTF_EXTENDEDKEY : 0)
                }
            }
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Presses and releases a key.
    /// </summary>
    /// <param name="virtualKeyCode">The virtual key code.</param>
    public static void KeyPress(ushort virtualKeyCode)
    {
        KeyDown(virtualKeyCode);
        KeyUp(virtualKeyCode);
    }

    /// <summary>
    /// Presses a key combination (e.g., Ctrl+C).
    /// </summary>
    /// <param name="modifiers">The modifier keys to hold.</param>
    /// <param name="key">The key to press.</param>
    public static void KeyCombination(ushort[] modifiers, ushort key)
    {
        // Press modifiers
        foreach (var modifier in modifiers)
            KeyDown(modifier);

        // Press and release key
        KeyPress(key);

        // Release modifiers in reverse order
        for (int i = modifiers.Length - 1; i >= 0; i--)
            KeyUp(modifiers[i]);
    }

    private static INPUT CreateMouseInput(uint flags)
    {
        return new INPUT
        {
            Type = INPUT_MOUSE,
            Union = new INPUTUNION
            {
                Mouse = new MOUSEINPUT
                {
                    dwFlags = flags
                }
            }
        };
    }

    private static bool IsExtendedKey(ushort vk)
    {
        // Extended keys include Insert, Delete, Home, End, Page Up, Page Down,
        // arrow keys, Num Lock, Break, Print Screen, Divide, Enter (numpad)
        return vk is >= 0x21 and <= 0x2E or 0x90 or 0x6F;
    }
}

/// <summary>
/// Common virtual key codes.
/// </summary>
public static class VirtualKeys
{
    public const ushort VK_BACK = 0x08;
    public const ushort VK_TAB = 0x09;
    public const ushort VK_RETURN = 0x0D;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_MENU = 0x12; // Alt
    public const ushort VK_ESCAPE = 0x1B;
    public const ushort VK_SPACE = 0x20;
    public const ushort VK_PRIOR = 0x21; // Page Up
    public const ushort VK_NEXT = 0x22; // Page Down
    public const ushort VK_END = 0x23;
    public const ushort VK_HOME = 0x24;
    public const ushort VK_LEFT = 0x25;
    public const ushort VK_UP = 0x26;
    public const ushort VK_RIGHT = 0x27;
    public const ushort VK_DOWN = 0x28;
    public const ushort VK_INSERT = 0x2D;
    public const ushort VK_DELETE = 0x2E;
    public const ushort VK_LWIN = 0x5B;
    public const ushort VK_RWIN = 0x5C;
    public const ushort VK_F1 = 0x70;
    public const ushort VK_F2 = 0x71;
    public const ushort VK_F3 = 0x72;
    public const ushort VK_F4 = 0x73;
    public const ushort VK_F5 = 0x74;
    public const ushort VK_F6 = 0x75;
    public const ushort VK_F7 = 0x76;
    public const ushort VK_F8 = 0x77;
    public const ushort VK_F9 = 0x78;
    public const ushort VK_F10 = 0x79;
    public const ushort VK_F11 = 0x7A;
    public const ushort VK_F12 = 0x7B;

    // Letters A-Z are 0x41-0x5A
    public static ushort Letter(char c) => (ushort)(char.ToUpper(c) - 'A' + 0x41);

    // Numbers 0-9 are 0x30-0x39
    public static ushort Number(char c) => (ushort)(c - '0' + 0x30);
}

