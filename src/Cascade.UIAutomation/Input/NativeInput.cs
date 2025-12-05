using System.Runtime.InteropServices;
using System.Drawing;

namespace Cascade.UIAutomation.Input;

/// <summary>
/// Default native input dispatcher that uses Win32 SendInput.
/// </summary>
internal sealed class NativeInput : INativeInput
{
    private const int InputKeyboard = 1;
    private const int InputMouse = 0;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint MouseeventfMiddledown = 0x0020;
    private const uint MouseeventfMiddleup = 0x0040;
    private const uint MouseeventfWheel = 0x0800;
    private const uint MouseeventfHwheel = 0x01000;
    private const int WheelDelta = 120;

    public Task SendUnicodeCharAsync(char character, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputs = new[]
        {
            CreateKeyboardInput(0, (ushort)character, KeyeventfUnicode),
            CreateKeyboardInput(0, (ushort)character, KeyeventfUnicode | KeyeventfKeyup)
        };

        Dispatch(inputs);
        return Task.CompletedTask;
    }

    public Task SendVirtualKeyAsync(VirtualKey key, KeySendOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new KeySendOptions();

        var inputs = new List<INPUT>();
        if (options.KeyDown)
        {
            inputs.Add(CreateKeyboardInput((ushort)key, 0, 0));
        }

        if (options.KeyUp)
        {
            inputs.Add(CreateKeyboardInput((ushort)key, 0, KeyeventfKeyup));
        }

        if (inputs.Count > 0)
        {
            Dispatch(inputs.ToArray());
        }

        return Task.CompletedTask;
    }

    public Task MoveMouseAsync(Point screenPoint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetCursorPos(screenPoint.X, screenPoint.Y);
        return Task.CompletedTask;
    }

    public async Task ClickAsync(MouseButton button, ClickOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ClickOptions();

        if (options.DelayBeforeMs > 0)
        {
            await Task.Delay(options.DelayBeforeMs, cancellationToken).ConfigureAwait(false);
        }

        for (var i = 0; i < Math.Max(1, options.ClickCount); i++)
        {
            var (down, up) = button switch
            {
                MouseButton.Left => (MouseeventfLeftdown, MouseeventfLeftup),
                MouseButton.Right => (MouseeventfRightdown, MouseeventfRightup),
                MouseButton.Middle => (MouseeventfMiddledown, MouseeventfMiddleup),
                _ => (MouseeventfLeftdown, MouseeventfLeftup)
            };

            Dispatch(new[]
            {
                CreateMouseInput(0, 0, 0, down),
                CreateMouseInput(0, 0, 0, up)
            });
        }

        if (options.DelayAfterMs > 0)
        {
            await Task.Delay(options.DelayAfterMs, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task ScrollAsync(int delta, ScrollOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ScrollOptions();

        var steps = Math.Max(1, options.StepSize);
        var wheelData = delta * steps * WheelDelta;
        var flag = options.Horizontal ? MouseeventfHwheel : MouseeventfWheel;

        Dispatch(new[]
        {
            CreateMouseInput(0, 0, wheelData, flag)
        });

        return Task.CompletedTask;
    }

    private static INPUT CreateKeyboardInput(ushort vk, ushort scan, uint flags)
    {
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    private static INPUT CreateMouseInput(int dx, int dy, int mouseData, uint flags)
    {
        return new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = (uint)mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    private static void Dispatch(INPUT[] inputs)
    {
        if (inputs.Length == 0)
        {
            return;
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            throw new InvalidOperationException($"SendInput failed with error {Marshal.GetLastWin32Error()}");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
}


