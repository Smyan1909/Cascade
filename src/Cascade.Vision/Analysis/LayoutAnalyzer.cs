namespace Cascade.Vision.Analysis;

public sealed class LayoutAnalyzer
{
    private readonly IElementAnalyzer _elementAnalyzer;

    public LayoutAnalyzer(IElementAnalyzer elementAnalyzer)
    {
        _elementAnalyzer = elementAnalyzer;
    }

    public Task<LayoutAnalysis> AnalyzeAsync(byte[] imageData, CancellationToken cancellationToken = default)
        => _elementAnalyzer.AnalyzeLayoutAsync(imageData, cancellationToken);
}


