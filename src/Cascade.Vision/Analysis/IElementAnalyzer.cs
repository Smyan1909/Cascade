using Cascade.Vision.Capture;

namespace Cascade.Vision.Analysis;

/// <summary>
/// Interface for visual element detection and analysis.
/// </summary>
public interface IElementAnalyzer
{
    #region Element Detection

    /// <summary>
    /// Detects visual elements in an image.
    /// </summary>
    /// <param name="imageData">The image data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of detected visual elements.</returns>
    Task<IReadOnlyList<VisualElement>> DetectElementsAsync(byte[] imageData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects visual elements in a capture result.
    /// </summary>
    /// <param name="capture">The capture result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of detected visual elements.</returns>
    Task<IReadOnlyList<VisualElement>> DetectElementsAsync(CaptureResult capture, CancellationToken cancellationToken = default);

    #endregion

    #region Specific Element Detection

    /// <summary>
    /// Detects button-like elements.
    /// </summary>
    Task<IReadOnlyList<VisualElement>> DetectButtonsAsync(byte[] imageData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects text input fields.
    /// </summary>
    Task<IReadOnlyList<VisualElement>> DetectTextFieldsAsync(byte[] imageData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects icons and small graphics.
    /// </summary>
    Task<IReadOnlyList<VisualElement>> DetectIconsAsync(byte[] imageData, CancellationToken cancellationToken = default);

    #endregion

    #region Layout Analysis

    /// <summary>
    /// Analyzes the overall layout of a UI.
    /// </summary>
    /// <param name="imageData">The image data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Layout analysis result.</returns>
    Task<LayoutAnalysis> AnalyzeLayoutAsync(byte[] imageData, CancellationToken cancellationToken = default);

    #endregion
}

