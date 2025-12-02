using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Enums;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class SearchCriteriaTests
{
    [Fact]
    public void ByAutomationId_ShouldCreateCriteriaWithAutomationId()
    {
        // Arrange & Act
        var criteria = SearchCriteria.ByAutomationId("testId");

        // Assert
        criteria.AutomationId.Should().Be("testId");
        criteria.Name.Should().BeNull();
        criteria.ControlType.Should().BeNull();
    }

    [Fact]
    public void ByName_ShouldCreateCriteriaWithName()
    {
        // Arrange & Act
        var criteria = SearchCriteria.ByName("Test Window");

        // Assert
        criteria.Name.Should().Be("Test Window");
        criteria.AutomationId.Should().BeNull();
    }

    [Fact]
    public void ByNameContains_ShouldCreateCriteriaWithNameContains()
    {
        // Arrange & Act
        var criteria = SearchCriteria.ByNameContains("Test");

        // Assert
        criteria.NameContains.Should().Be("Test");
    }

    [Fact]
    public void ByClassName_ShouldCreateCriteriaWithClassName()
    {
        // Arrange & Act
        var criteria = SearchCriteria.ByClassName("Button");

        // Assert
        criteria.ClassName.Should().Be("Button");
    }

    [Fact]
    public void ByControlType_ShouldCreateCriteriaWithControlType()
    {
        // Arrange & Act
        var criteria = SearchCriteria.ByControlType(ControlType.Button);

        // Assert
        criteria.ControlType.Should().Be(ControlType.Button);
    }

    [Fact]
    public void ByProcessId_ShouldCreateCriteriaWithProcessId()
    {
        // Arrange & Act
        var criteria = SearchCriteria.ByProcessId(1234);

        // Assert
        criteria.ProcessId.Should().Be(1234);
    }

    [Fact]
    public void And_ShouldCombineCriteria()
    {
        // Arrange
        var criteria1 = SearchCriteria.ByName("Test");
        var criteria2 = SearchCriteria.ByControlType(ControlType.Button);

        // Act
        var combined = criteria1.And(criteria2);

        // Assert
        combined.Name.Should().Be("Test");
        combined.ToString().Should().Contain("AND");
    }

    [Fact]
    public void Or_ShouldCombineCriteria()
    {
        // Arrange
        var criteria1 = SearchCriteria.ByName("Test");
        var criteria2 = SearchCriteria.ByControlType(ControlType.Button);

        // Act
        var combined = criteria1.Or(criteria2);

        // Assert
        combined.Name.Should().Be("Test");
        combined.ToString().Should().Contain("OR");
    }

    [Fact]
    public void Not_ShouldNegateCriteria()
    {
        // Arrange
        var criteria = SearchCriteria.ByName("Test");

        // Act
        var negated = criteria.Not();

        // Assert
        negated.ToString().Should().Contain("NOT");
    }

    [Fact]
    public void All_ShouldCreateEmptyCriteria()
    {
        // Arrange & Act
        var criteria = SearchCriteria.All;

        // Assert
        criteria.AutomationId.Should().BeNull();
        criteria.Name.Should().BeNull();
        criteria.ControlType.Should().BeNull();
        criteria.ToString().Should().Be("All");
    }

    [Fact]
    public void ToString_ShouldFormatCriteriaCorrectly()
    {
        // Arrange
        var criteria = SearchCriteria.ByAutomationId("btn1")
            .And(SearchCriteria.ByControlType(ControlType.Button));

        // Act
        var result = criteria.ToString();

        // Assert
        result.Should().Contain("AutomationId='btn1'");
        result.Should().Contain("ControlType=Button");
    }

    [Fact]
    public void MultipleCriteria_ShouldBeCombinable()
    {
        // Arrange & Act
        var criteria = SearchCriteria.ByName("Calculator")
            .And(SearchCriteria.ByControlType(ControlType.Window))
            .And(SearchCriteria.ByProcessId(1234));

        // Assert
        criteria.Name.Should().Be("Calculator");
        criteria.ToString().Should().Contain("AND");
    }
}

