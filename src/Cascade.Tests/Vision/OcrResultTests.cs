using System.Drawing;
using Cascade.Vision.OCR;
using Xunit;

namespace Cascade.Tests.Vision;

/// <summary>
/// Tests for OcrResult and related classes.
/// </summary>
public class OcrResultTests
{
    private static OcrResult CreateSampleResult()
    {
        return new OcrResult
        {
            FullText = "Hello World\nThis is a test",
            Confidence = 0.95,
            EngineUsed = "Test",
            ProcessingTime = TimeSpan.FromMilliseconds(100),
            Lines = new List<OcrLine>
            {
                new OcrLine
                {
                    Text = "Hello World",
                    BoundingBox = new Rectangle(10, 10, 100, 20),
                    Confidence = 0.95,
                    Words = new List<OcrWord>
                    {
                        new OcrWord { Text = "Hello", BoundingBox = new Rectangle(10, 10, 40, 20), Confidence = 0.96 },
                        new OcrWord { Text = "World", BoundingBox = new Rectangle(55, 10, 45, 20), Confidence = 0.94 }
                    }
                },
                new OcrLine
                {
                    Text = "This is a test",
                    BoundingBox = new Rectangle(10, 35, 120, 20),
                    Confidence = 0.93,
                    Words = new List<OcrWord>
                    {
                        new OcrWord { Text = "This", BoundingBox = new Rectangle(10, 35, 30, 20), Confidence = 0.95 },
                        new OcrWord { Text = "is", BoundingBox = new Rectangle(45, 35, 15, 20), Confidence = 0.92 },
                        new OcrWord { Text = "a", BoundingBox = new Rectangle(65, 35, 10, 20), Confidence = 0.90 },
                        new OcrWord { Text = "test", BoundingBox = new Rectangle(80, 35, 35, 20), Confidence = 0.94 }
                    }
                }
            }
        };
    }

    [Fact]
    public void HasText_WithText_ReturnsTrue()
    {
        // Arrange
        var result = CreateSampleResult();

        // Assert
        Assert.True(result.HasText);
    }

    [Fact]
    public void HasText_EmptyText_ReturnsFalse()
    {
        // Arrange
        var result = OcrResult.Empty("Test");

        // Assert
        Assert.False(result.HasText);
    }

    [Fact]
    public void Words_ReturnsAllWordsFromAllLines()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act
        var words = result.Words;

        // Assert
        Assert.Equal(6, words.Count);
    }

    [Fact]
    public void FindFirstWord_ExactMatch_ReturnsWord()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act
        var word = result.FindFirstWord("Hello");

        // Assert
        Assert.NotNull(word);
        Assert.Equal("Hello", word.Text);
    }

    [Fact]
    public void FindFirstWord_CaseInsensitive_ReturnsWord()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act
        var word = result.FindFirstWord("hello");

        // Assert
        Assert.NotNull(word);
        Assert.Equal("Hello", word.Text);
    }

    [Fact]
    public void FindFirstWord_NotFound_ReturnsNull()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act
        var word = result.FindFirstWord("NotPresent");

        // Assert
        Assert.Null(word);
    }

    [Fact]
    public void FindWords_MultipleMatches_ReturnsAll()
    {
        // Arrange
        var result = new OcrResult
        {
            FullText = "test test test",
            Confidence = 0.9,
            EngineUsed = "Test",
            Lines = new List<OcrLine>
            {
                new OcrLine
                {
                    Text = "test test test",
                    BoundingBox = new Rectangle(0, 0, 100, 20),
                    Confidence = 0.9,
                    Words = new List<OcrWord>
                    {
                        new OcrWord { Text = "test", BoundingBox = new Rectangle(0, 0, 30, 20) },
                        new OcrWord { Text = "test", BoundingBox = new Rectangle(35, 0, 30, 20) },
                        new OcrWord { Text = "test", BoundingBox = new Rectangle(70, 0, 30, 20) }
                    }
                }
            }
        };

        // Act
        var words = result.FindWords("test");

        // Assert
        Assert.Equal(3, words.Count);
    }

    [Fact]
    public void FindWordsContaining_PartialMatch_ReturnsWords()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act
        var words = result.FindWordsContaining("ell");

        // Assert
        Assert.Single(words);
        Assert.Equal("Hello", words[0].Text);
    }

    [Fact]
    public void ContainsText_PresentText_ReturnsTrue()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act & Assert
        Assert.True(result.ContainsText("Hello"));
        Assert.True(result.ContainsText("test"));
        Assert.True(result.ContainsText("World"));
    }

    [Fact]
    public void ContainsText_MissingText_ReturnsFalse()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act & Assert
        Assert.False(result.ContainsText("NotPresent"));
    }

    [Fact]
    public void GetTextBounds_ExactWord_ReturnsBoundingBox()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act
        var bounds = result.GetTextBounds("Hello");

        // Assert
        Assert.NotNull(bounds);
        Assert.Equal(10, bounds.Value.X);
        Assert.Equal(10, bounds.Value.Y);
    }

    [Fact]
    public void IsAcceptable_HighConfidence_ReturnsTrue()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act & Assert
        Assert.True(result.IsAcceptable(0.9));
    }

    [Fact]
    public void IsAcceptable_LowConfidence_ReturnsFalse()
    {
        // Arrange
        var result = CreateSampleResult();

        // Act & Assert
        Assert.False(result.IsAcceptable(0.99));
    }

    [Fact]
    public void OcrWord_Center_CalculatesCorrectly()
    {
        // Arrange
        var word = new OcrWord
        {
            Text = "Test",
            BoundingBox = new Rectangle(100, 200, 50, 20)
        };

        // Act
        var center = word.Center;

        // Assert
        Assert.Equal(125, center.X);
        Assert.Equal(210, center.Y);
    }

    [Fact]
    public void Empty_CreatesEmptyResult()
    {
        // Arrange & Act
        var result = OcrResult.Empty("TestEngine");

        // Assert
        Assert.Equal(string.Empty, result.FullText);
        Assert.Equal(0, result.Confidence);
        Assert.Equal("TestEngine", result.EngineUsed);
        Assert.Empty(result.Lines);
        Assert.False(result.HasText);
    }
}

