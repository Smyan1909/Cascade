using System.Drawing;

namespace Cascade.Vision.OCR;

/// <summary>
/// Represents the result of an OCR operation.
/// </summary>
public class OcrResult
{
    /// <summary>
    /// Gets or sets the full recognized text.
    /// </summary>
    public string FullText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the overall confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the recognized lines of text.
    /// </summary>
    public IReadOnlyList<OcrLine> Lines { get; set; } = Array.Empty<OcrLine>();

    /// <summary>
    /// Gets all words from all lines.
    /// </summary>
    public IReadOnlyList<OcrWord> Words => Lines.SelectMany(l => l.Words).ToList();

    /// <summary>
    /// Gets or sets the processing time for the OCR operation.
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets the name of the OCR engine that produced this result.
    /// </summary>
    public string EngineUsed { get; set; } = string.Empty;

    /// <summary>
    /// Gets whether any text was recognized.
    /// </summary>
    public bool HasText => !string.IsNullOrWhiteSpace(FullText);

    /// <summary>
    /// Gets whether the result meets minimum quality standards.
    /// </summary>
    /// <param name="minConfidence">The minimum confidence threshold.</param>
    /// <returns>True if the result is acceptable.</returns>
    public bool IsAcceptable(double minConfidence = 0.5)
    {
        return HasText && Confidence >= minConfidence;
    }

    /// <summary>
    /// Finds all words matching the specified text.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <param name="comparison">The string comparison mode.</param>
    /// <returns>A list of matching words.</returns>
    public IReadOnlyList<OcrWord> FindWords(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<OcrWord>();

        return Words.Where(w => w.Text.Equals(text, comparison)).ToList();
    }

    /// <summary>
    /// Finds the first word matching the specified text.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <param name="comparison">The string comparison mode.</param>
    /// <returns>The first matching word, or null if not found.</returns>
    public OcrWord? FindFirstWord(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        return Words.FirstOrDefault(w => w.Text.Equals(text, comparison));
    }

    /// <summary>
    /// Finds words containing the specified text.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <param name="comparison">The string comparison mode.</param>
    /// <returns>A list of words containing the text.</returns>
    public IReadOnlyList<OcrWord> FindWordsContaining(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<OcrWord>();

        return Words.Where(w => w.Text.Contains(text, comparison)).ToList();
    }

    /// <summary>
    /// Gets the bounding rectangle that contains the specified text.
    /// </summary>
    /// <param name="text">The text to find.</param>
    /// <param name="comparison">The string comparison mode.</param>
    /// <returns>The bounding rectangle, or null if text not found.</returns>
    public Rectangle? GetTextBounds(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        // First try exact word match
        var word = FindFirstWord(text, comparison);
        if (word != null)
            return word.BoundingBox;

        // Try to find the text spanning multiple words
        foreach (var line in Lines)
        {
            if (line.Text.Contains(text, comparison))
            {
                // Find the bounds within this line
                var startIdx = line.Text.IndexOf(text, comparison);
                if (startIdx >= 0)
                {
                    return FindTextBoundsInLine(line, text, startIdx);
                }
            }
        }

        return null;
    }

    private static Rectangle? FindTextBoundsInLine(OcrLine line, string text, int startIdx)
    {
        // Calculate approximate character positions
        var words = line.Words.ToList();
        if (words.Count == 0)
            return line.BoundingBox;

        int charCount = 0;
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        bool inRange = false;

        foreach (var word in words)
        {
            int wordStart = charCount;
            int wordEnd = charCount + word.Text.Length;

            if (wordEnd > startIdx && wordStart < startIdx + text.Length)
            {
                inRange = true;
                minX = Math.Min(minX, word.BoundingBox.Left);
                minY = Math.Min(minY, word.BoundingBox.Top);
                maxX = Math.Max(maxX, word.BoundingBox.Right);
                maxY = Math.Max(maxY, word.BoundingBox.Bottom);
            }

            charCount = wordEnd + 1; // +1 for space
        }

        if (inRange && minX < maxX && minY < maxY)
        {
            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        return line.BoundingBox;
    }

    /// <summary>
    /// Checks if the full text contains the specified text.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <param name="comparison">The string comparison mode.</param>
    /// <returns>True if the text is found.</returns>
    public bool ContainsText(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return FullText.Contains(text, comparison);
    }

    /// <summary>
    /// Creates an empty OcrResult.
    /// </summary>
    public static OcrResult Empty(string engineName) => new()
    {
        FullText = string.Empty,
        Confidence = 0,
        Lines = Array.Empty<OcrLine>(),
        EngineUsed = engineName
    };
}

/// <summary>
/// Represents a line of recognized text.
/// </summary>
public class OcrLine
{
    /// <summary>
    /// Gets or sets the recognized text for this line.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bounding box for this line.
    /// </summary>
    public Rectangle BoundingBox { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for this line (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the words in this line.
    /// </summary>
    public IReadOnlyList<OcrWord> Words { get; set; } = Array.Empty<OcrWord>();

    /// <summary>
    /// Gets the center point of this line's bounding box.
    /// </summary>
    public Point Center => new(
        BoundingBox.X + BoundingBox.Width / 2,
        BoundingBox.Y + BoundingBox.Height / 2);
}

/// <summary>
/// Represents a recognized word.
/// </summary>
public class OcrWord
{
    /// <summary>
    /// Gets or sets the recognized text for this word.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bounding box for this word.
    /// </summary>
    public Rectangle BoundingBox { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for this word (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets the center point of this word's bounding box.
    /// </summary>
    public Point Center => new(
        BoundingBox.X + BoundingBox.Width / 2,
        BoundingBox.Y + BoundingBox.Height / 2);

    /// <summary>
    /// Gets the top-left corner of the bounding box.
    /// </summary>
    public Point TopLeft => new(BoundingBox.X, BoundingBox.Y);

    /// <summary>
    /// Gets the bottom-right corner of the bounding box.
    /// </summary>
    public Point BottomRight => new(BoundingBox.Right, BoundingBox.Bottom);

    /// <summary>
    /// Gets the width of the word's bounding box.
    /// </summary>
    public int Width => BoundingBox.Width;

    /// <summary>
    /// Gets the height of the word's bounding box.
    /// </summary>
    public int Height => BoundingBox.Height;
}

