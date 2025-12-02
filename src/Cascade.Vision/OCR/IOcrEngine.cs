using System.Drawing;
using Cascade.Vision.Capture;

namespace Cascade.Vision.OCR;

/// <summary>
/// Interface for OCR engines.
/// </summary>
public interface IOcrEngine
{
    #region Recognition

    /// <summary>
    /// Recognizes text in an image from raw byte data.
    /// </summary>
    /// <param name="imageData">The image data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCR result.</returns>
    Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes text in an image from a file path.
    /// </summary>
    /// <param name="imagePath">The path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCR result.</returns>
    Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes text in a capture result.
    /// </summary>
    /// <param name="capture">The capture result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCR result.</returns>
    Task<OcrResult> RecognizeAsync(CaptureResult capture, CancellationToken cancellationToken = default);

    #endregion

    #region Region Recognition

    /// <summary>
    /// Recognizes text in a specific region of an image.
    /// </summary>
    /// <param name="imageData">The image data.</param>
    /// <param name="region">The region to recognize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCR result.</returns>
    Task<OcrResult> RecognizeRegionAsync(byte[] imageData, Rectangle region, CancellationToken cancellationToken = default);

    #endregion

    #region Configuration

    /// <summary>
    /// Gets or sets the OCR options.
    /// </summary>
    OcrOptions Options { get; set; }

    #endregion

    #region Engine Information

    /// <summary>
    /// Gets the name of this OCR engine.
    /// </summary>
    string EngineName { get; }

    /// <summary>
    /// Gets the list of supported languages.
    /// </summary>
    IReadOnlyList<string> SupportedLanguages { get; }

    /// <summary>
    /// Gets whether this engine is available and ready to use.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the priority of this engine (lower = higher priority).
    /// Used by CompositeOcrEngine for ordering.
    /// </summary>
    int Priority { get; }

    #endregion
}

