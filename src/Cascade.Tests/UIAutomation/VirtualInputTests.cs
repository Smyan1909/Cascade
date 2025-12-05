using System.Drawing;
using Cascade.Core.Session;
using Cascade.UIAutomation.Input;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class VirtualInputTests
{
    [Fact]
    public async Task VirtualKeyboard_TypeTextAsync_SendsClearAndCharacters()
    {
        var fake = new FakeNativeInput();
        var keyboard = new VirtualKeyboard(fake);

        await keyboard.TypeTextAsync("ab", new TextEntryOptions { ClearBeforeTyping = true, DelayBetweenCharactersMs = 0 });

        fake.Calls.Should().ContainInOrder(
            "key Control down",
            "key A down",
            "key A up",
            "key Control up",
            "key Backspace down",
            "key Backspace up",
            "unicode a",
            "unicode b");
    }

    [Fact]
    public async Task VirtualKeyboard_SendVirtualKeyAsync_HonorsOptions()
    {
        var fake = new FakeNativeInput();
        var keyboard = new VirtualKeyboard(fake);
        var options = new KeySendOptions { KeyDown = true, KeyUp = false };

        await keyboard.SendVirtualKeyAsync(VirtualKey.Enter, options);

        fake.Calls.Should().ContainSingle().Which.Should().Be("key Enter down");
    }

    [Fact]
    public async Task VirtualMouse_DelegatesToNativeInput()
    {
        var fake = new FakeNativeInput();
        var keyboard = new VirtualKeyboard(fake);
        var mouse = new VirtualMouse(new SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() }, keyboard, fake);

        await mouse.MoveMouseAsync(new Point(10, 20));
        await mouse.ClickAsync(MouseButton.Left, new ClickOptions { ClickCount = 2, DelayAfterMs = 0, DelayBeforeMs = 0 });
        await mouse.ScrollAsync(1, new ScrollOptions { Horizontal = true, StepSize = 2 });

        fake.Calls.Should().ContainInOrder(
            "move 10,20",
            "click Left x2",
            "scroll 1 horizontal step2");
    }

    private sealed class FakeNativeInput : INativeInput
    {
        public List<string> Calls { get; } = new();

        public Task ClickAsync(MouseButton button, ClickOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add($"click {button} x{(options?.ClickCount ?? 1)}");
            return Task.CompletedTask;
        }

        public Task MoveMouseAsync(Point screenPoint, CancellationToken cancellationToken = default)
        {
            Calls.Add($"move {screenPoint.X},{screenPoint.Y}");
            return Task.CompletedTask;
        }

        public Task ScrollAsync(int delta, ScrollOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add($"scroll {delta} {(options?.Horizontal ?? false ? "horizontal" : "vertical")} step{options?.StepSize ?? 1}");
            return Task.CompletedTask;
        }

        public Task SendUnicodeCharAsync(char character, CancellationToken cancellationToken = default)
        {
            Calls.Add($"unicode {character}");
            return Task.CompletedTask;
        }

        public Task SendVirtualKeyAsync(VirtualKey key, KeySendOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new KeySendOptions();
            if (options.KeyDown)
            {
                Calls.Add($"key {key} down");
            }
            if (options.KeyUp)
            {
                Calls.Add($"key {key} up");
            }
            return Task.CompletedTask;
        }
    }
}


