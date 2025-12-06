using Cascade.Body.Automation;
using FluentAssertions;
using System.Drawing;
using Xunit;

namespace Cascade.Body.Tests;

public class NormalizationHelpersTests
{
    [Fact]
    public void ToNormalizedRectangle_ClampsBetweenZeroAndOne()
    {
        var rect = new RectangleF(10, 10, 100, 100);
        var normalized = NormalizationHelpers.ToNormalizedRectangle(rect);

        normalized.X.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(1);
        normalized.Y.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(1);
        normalized.Width.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(1);
        normalized.Height.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(1);
    }
}

