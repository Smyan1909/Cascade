namespace Cascade.Vision.Analysis;

public interface IElementAnalyzer
{
    Task<IReadOnlyList<VisualElement>> DetectElementsAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisualElement>> DetectElementsAsync(Capture.CaptureResult capture, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisualElement>> DetectButtonsAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisualElement>> DetectTextFieldsAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisualElement>> DetectIconsAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<LayoutAnalysis> AnalyzeLayoutAsync(byte[] imageData, CancellationToken cancellationToken = default);
}


