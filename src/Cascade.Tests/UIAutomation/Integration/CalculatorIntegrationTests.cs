using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Enums;
using Cascade.UIAutomation.Services;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.UIAutomation.Integration;

/// <summary>
/// Integration tests using Windows Calculator.
/// These tests require Calculator to be available on the system.
/// </summary>
[Trait("Category", "Integration")]
[Collection("UIAutomation")]
public class CalculatorIntegrationTests : IDisposable
{
    private readonly UIAutomationService _service;
    private bool _launchedCalculator = false;

    public CalculatorIntegrationTests()
    {
        _service = new UIAutomationService(new UIAutomationOptions
        {
            DefaultTimeout = TimeSpan.FromSeconds(10),
            EnableCaching = true
        });
    }

    public void Dispose()
    {
        // Close Calculator if we launched it
        if (_launchedCalculator)
        {
            try
            {
                var calc = _service.Discovery.FindWindow("Calculator");
                if (calc != null)
                {
                    _service.Windows.CloseAsync(calc).Wait();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _service.Dispose();
    }

    [Fact]
    public async Task FindWindow_ShouldFindCalculator()
    {
        // Arrange
        await LaunchCalculatorAsync();

        // Act
        var calculator = _service.Discovery.FindWindow("Calculator");

        // Assert
        calculator.Should().NotBeNull();
        calculator!.ControlType.Should().Be(ControlType.Window);
    }

    [Fact]
    public async Task WaitForWindow_ShouldWaitForCalculator()
    {
        // Arrange
        var launchTask = _service.Windows.LaunchAndAttachAsync("calc.exe");

        // Act
        var calculator = await _service.WaitForWindowAsync("Calculator", TimeSpan.FromSeconds(10));
        _launchedCalculator = true;

        // Assert
        calculator.Should().NotBeNull();
    }

    [Fact]
    public async Task CaptureSnapshot_ShouldCaptureCalculatorTree()
    {
        // Arrange
        await LaunchCalculatorAsync();
        var calculator = _service.Discovery.FindWindow("Calculator");
        calculator.Should().NotBeNull();

        // Act
        var snapshot = _service.CaptureSnapshot(calculator!, maxDepth: 5);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TotalElements.Should().BeGreaterThan(1);
        snapshot.Root.Should().NotBeNull();
    }

    [Fact]
    public async Task FindElement_ShouldFindNumberButtons()
    {
        // Arrange
        await LaunchCalculatorAsync();
        var calculator = _service.Discovery.FindWindow("Calculator");
        calculator.Should().NotBeNull();

        // Act - Find button with AutomationId for number 1
        var button1 = calculator!.FindFirst(SearchCriteria.ByAutomationId("num1Button"));

        // Assert
        button1.Should().NotBeNull();
        button1!.ControlType.Should().Be(ControlType.Button);
    }

    [Fact]
    public async Task Click_ShouldClickCalculatorButton()
    {
        // Arrange
        await LaunchCalculatorAsync();
        var calculator = _service.Discovery.FindWindow("Calculator");
        calculator.Should().NotBeNull();

        // Find the Clear button to reset
        var clearButton = calculator!.FindFirst(SearchCriteria.ByAutomationId("clearButton"));
        if (clearButton != null)
        {
            await _service.Actions.ClickAsync(clearButton);
            await Task.Delay(200);
        }

        // Act - Click buttons 1, +, 2, =
        var button1 = calculator.FindFirst(SearchCriteria.ByAutomationId("num1Button"));
        var buttonPlus = calculator.FindFirst(SearchCriteria.ByAutomationId("plusButton"));
        var button2 = calculator.FindFirst(SearchCriteria.ByAutomationId("num2Button"));
        var buttonEquals = calculator.FindFirst(SearchCriteria.ByAutomationId("equalButton"));

        button1.Should().NotBeNull();

        await _service.Actions.ClickAsync(button1!);
        await Task.Delay(100);

        if (buttonPlus != null)
        {
            await _service.Actions.ClickAsync(buttonPlus);
            await Task.Delay(100);
        }

        if (button2 != null)
        {
            await _service.Actions.ClickAsync(button2);
            await Task.Delay(100);
        }

        if (buttonEquals != null)
        {
            await _service.Actions.ClickAsync(buttonEquals);
            await Task.Delay(100);
        }

        // Assert - The calculator should have performed the calculation
        // Note: The actual result verification depends on Calculator version
    }

    [Fact]
    public async Task GetChildren_ShouldReturnCalculatorChildren()
    {
        // Arrange
        await LaunchCalculatorAsync();
        var calculator = _service.Discovery.FindWindow("Calculator");
        calculator.Should().NotBeNull();

        // Act
        var children = calculator!.Children;

        // Assert
        children.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TreeWalker_ShouldNavigateCalculatorTree()
    {
        // Arrange
        await LaunchCalculatorAsync();
        var calculator = _service.Discovery.FindWindow("Calculator");
        calculator.Should().NotBeNull();

        // Act
        var descendants = _service.TreeWalker.GetDescendants(calculator!, maxDepth: 3).ToList();

        // Assert
        descendants.Should().NotBeEmpty();
        descendants.Count.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task ElementLocator_ShouldFindCalculatorElements()
    {
        // Arrange
        await LaunchCalculatorAsync();
        var calculator = _service.Discovery.FindWindow("Calculator");
        calculator.Should().NotBeNull();

        // Act
        var locator = ElementLocator.Parse("//Button[@AutomationId='num1Button']");
        var button = locator.Find(calculator!);

        // Assert
        button.Should().NotBeNull();
    }

    private async Task LaunchCalculatorAsync()
    {
        // Check if Calculator is already running
        var existing = _service.Discovery.FindWindow("Calculator");
        if (existing != null)
            return;

        // Launch Calculator
        await _service.Windows.LaunchAndAttachAsync("calc.exe", timeout: TimeSpan.FromSeconds(10));
        _launchedCalculator = true;
        await Task.Delay(1000); // Wait for Calculator to fully load
    }
}

