namespace Cascade.Vision.OCR;

public sealed class OcrResult
{
    public string FullText { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public IReadOnlyList<OcrLine> Lines { get; init; } = Array.Empty<OcrLine>();
    public IReadOnlyList<OcrWord> Words { get; init; } = Array.Empty<OcrWord>();
    public TimeSpan ProcessingTime { get; init; }
    public string EngineUsed { get; init; } = string.Empty;

    public IReadOnlyList<OcrWord> FindWords(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        => Words.Where(word => string.Equals(word.Text, text, comparison)).ToList();

    public OcrWord? FindFirstWord(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        => Words.FirstOrDefault(word => string.Equals(word.Text, text, comparison));

    public Rectangle? GetTextBounds(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var word = FindFirstWord(text, comparison);
        return word?.BoundingBox;
    }
}

public sealed class OcrLine
{
    public string Text { get; init; } = string.Empty;
    public Rectangle BoundingBox { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<OcrWord> Words { get; init; } = Array.Empty<OcrWord>();
}

public sealed class OcrWord
{
    public string Text { get; init; } = string.Empty;
    public Rectangle BoundingBox { get; init; }
    public double Confidence { get; init; }
    public Point Center => new(BoundingBox.X + BoundingBox.Width / 2, BoundingBox.Y + BoundingBox.Height / 2);
}


