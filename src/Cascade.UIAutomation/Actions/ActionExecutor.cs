using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using Cascade.UIAutomation.Exceptions;
using Cascade.UIAutomation.Interop;
using Cascade.UIAutomation.Patterns;

namespace Cascade.UIAutomation.Actions;

/// <summary>
/// Implementation of IActionExecutor for executing UI actions.
/// </summary>
public class ActionExecutor : IActionExecutor
{
    private readonly int _defaultClickDelay;
    private readonly int _defaultTypeDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionExecutor"/> class.
    /// </summary>
    /// <param name="defaultClickDelay">Default delay after click operations in milliseconds.</param>
    /// <param name="defaultTypeDelay">Default delay between typed characters in milliseconds.</param>
    public ActionExecutor(int defaultClickDelay = 50, int defaultTypeDelay = 20)
    {
        _defaultClickDelay = defaultClickDelay;
        _defaultTypeDelay = defaultTypeDelay;
    }

    /// <inheritdoc />
    public async Task ClickAsync(IUIElement element, ClickType clickType = ClickType.Left)
    {
        EnsureEnabled(element);

        var point = element.ClickablePoint;

        await Task.Run(() =>
        {
            switch (clickType)
            {
                case ClickType.Left:
                    InputSimulator.LeftClick(point.X, point.Y);
                    break;
                case ClickType.Right:
                    InputSimulator.RightClick(point.X, point.Y);
                    break;
                case ClickType.Middle:
                    InputSimulator.MiddleClick(point.X, point.Y);
                    break;
                case ClickType.Double:
                    InputSimulator.DoubleClick(point.X, point.Y);
                    break;
            }
        });

        await Task.Delay(_defaultClickDelay);
    }

    /// <inheritdoc />
    public Task DoubleClickAsync(IUIElement element)
    {
        return ClickAsync(element, ClickType.Double);
    }

    /// <inheritdoc />
    public Task RightClickAsync(IUIElement element)
    {
        return ClickAsync(element, ClickType.Right);
    }

    /// <inheritdoc />
    public async Task TypeTextAsync(IUIElement element, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        EnsureEnabled(element);

        await SetFocusAsync(element);
        await Task.Delay(50); // Brief delay after focus

        await Task.Run(() => InputSimulator.TypeText(text, _defaultTypeDelay));
    }

    /// <inheritdoc />
    public async Task SetValueAsync(IUIElement element, string value)
    {
        EnsureEnabled(element);

        if (element.TryGetPattern<IValuePattern>(out var valuePattern) && valuePattern != null)
        {
            if (valuePattern.IsReadOnly)
                throw UIAutomationException.InvalidOperation("Element value is read-only");

            await valuePattern.SetValueAsync(value);
        }
        else
        {
            throw UIAutomationException.PatternNotSupported(nameof(IValuePattern), element.RuntimeId);
        }
    }

    /// <inheritdoc />
    public async Task ScrollAsync(IUIElement element, ScrollAmount horizontal, ScrollAmount vertical)
    {
        if (element.TryGetPattern<IScrollPattern>(out var scrollPattern) && scrollPattern != null)
        {
            await scrollPattern.ScrollAsync(horizontal, vertical);
        }
        else
        {
            throw UIAutomationException.PatternNotSupported(nameof(IScrollPattern), element.RuntimeId);
        }
    }

    /// <inheritdoc />
    public async Task DragDropAsync(IUIElement source, IUIElement target)
    {
        EnsureEnabled(source);

        var sourcePoint = source.ClickablePoint;
        var targetPoint = target.ClickablePoint;

        await Task.Run(() =>
        {
            InputSimulator.MoveTo(sourcePoint.X, sourcePoint.Y);
            Thread.Sleep(50);
            InputSimulator.LeftDown();
            Thread.Sleep(50);

            // Smooth movement to target
            var steps = 10;
            var deltaX = (targetPoint.X - sourcePoint.X) / (double)steps;
            var deltaY = (targetPoint.Y - sourcePoint.Y) / (double)steps;

            for (int i = 1; i <= steps; i++)
            {
                InputSimulator.MoveTo(
                    (int)(sourcePoint.X + deltaX * i),
                    (int)(sourcePoint.Y + deltaY * i));
                Thread.Sleep(20);
            }

            Thread.Sleep(50);
            InputSimulator.LeftUp();
        });
    }

    /// <inheritdoc />
    public async Task DragDropAsync(IUIElement source, int targetX, int targetY)
    {
        EnsureEnabled(source);

        var sourcePoint = source.ClickablePoint;

        await Task.Run(() =>
        {
            InputSimulator.MoveTo(sourcePoint.X, sourcePoint.Y);
            Thread.Sleep(50);
            InputSimulator.LeftDown();
            Thread.Sleep(50);

            // Smooth movement to target
            var steps = 10;
            var deltaX = (targetX - sourcePoint.X) / (double)steps;
            var deltaY = (targetY - sourcePoint.Y) / (double)steps;

            for (int i = 1; i <= steps; i++)
            {
                InputSimulator.MoveTo(
                    (int)(sourcePoint.X + deltaX * i),
                    (int)(sourcePoint.Y + deltaY * i));
                Thread.Sleep(20);
            }

            Thread.Sleep(50);
            InputSimulator.LeftUp();
        });
    }

    /// <inheritdoc />
    public Task SetFocusAsync(IUIElement element)
    {
        return element.SetFocusAsync();
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IUIElement element)
    {
        EnsureEnabled(element);

        if (element.TryGetPattern<IInvokePattern>(out var invokePattern) && invokePattern != null)
        {
            await invokePattern.InvokeAsync();
        }
        else
        {
            // Fall back to click
            await ClickAsync(element);
        }
    }

    /// <inheritdoc />
    public async Task ToggleAsync(IUIElement element)
    {
        EnsureEnabled(element);

        if (element.TryGetPattern<ITogglePattern>(out var togglePattern) && togglePattern != null)
        {
            await togglePattern.ToggleAsync();
        }
        else
        {
            throw UIAutomationException.PatternNotSupported(nameof(ITogglePattern), element.RuntimeId);
        }
    }

    /// <inheritdoc />
    public async Task ExpandAsync(IUIElement element)
    {
        if (element.TryGetPattern<IExpandCollapsePattern>(out var pattern) && pattern != null)
        {
            await pattern.ExpandAsync();
        }
        else
        {
            throw UIAutomationException.PatternNotSupported(nameof(IExpandCollapsePattern), element.RuntimeId);
        }
    }

    /// <inheritdoc />
    public async Task CollapseAsync(IUIElement element)
    {
        if (element.TryGetPattern<IExpandCollapsePattern>(out var pattern) && pattern != null)
        {
            await pattern.CollapseAsync();
        }
        else
        {
            throw UIAutomationException.PatternNotSupported(nameof(IExpandCollapsePattern), element.RuntimeId);
        }
    }

    /// <inheritdoc />
    public async Task SelectAsync(IUIElement element)
    {
        if (element.TryGetPattern<ISelectionItemPattern>(out var pattern) && pattern != null)
        {
            await pattern.SelectAsync();
        }
        else
        {
            throw UIAutomationException.PatternNotSupported(nameof(ISelectionItemPattern), element.RuntimeId);
        }
    }

    private static void EnsureEnabled(IUIElement element)
    {
        if (!element.IsEnabled)
            throw UIAutomationException.ElementNotEnabled(element.RuntimeId);
    }
}

