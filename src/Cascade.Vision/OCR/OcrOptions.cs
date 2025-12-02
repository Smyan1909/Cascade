namespace Cascade.Vision.OCR;

/// <summary>
/// Configuration options for OCR operations.
/// </summary>
public class OcrOptions
{
    /// <summary>
    /// Gets or sets the primary language for recognition (e.g., "en-US", "en", "de-DE").
    /// </summary>
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets additional languages to consider during recognition.
    /// </summary>
    public IReadOnlyList<string> AdditionalLanguages { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the engine preference for OCR operations.
    /// </summary>
    public OcrEnginePreference EnginePreference { get; set; } = OcrEnginePreference.WindowsFirst;

    /// <summary>
    /// Gets or sets whether to apply image preprocessing before OCR.
    /// </summary>
    public bool EnablePreprocessing { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum confidence threshold (0.0 to 1.0).
    /// Results below this threshold may trigger fallback to other engines.
    /// </summary>
    public double MinConfidence { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the page segmentation mode.
    /// </summary>
    public PageSegmentationMode PageSegMode { get; set; } = PageSegmentationMode.Auto;

    #region Preprocessing Options

    /// <summary>
    /// Gets or sets whether to convert the image to grayscale before OCR.
    /// </summary>
    public bool ConvertToGrayscale { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to apply automatic deskew correction.
    /// </summary>
    public bool ApplyDeskew { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enhance contrast before OCR.
    /// </summary>
    public bool EnhanceContrast { get; set; } = true;

    /// <summary>
    /// Gets or sets the scale factor for upscaling images before OCR.
    /// Higher values can improve accuracy but increase processing time.
    /// </summary>
    public int ScaleFactor { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether to apply denoising.
    /// </summary>
    public bool ApplyDenoise { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to apply binarization (thresholding).
    /// </summary>
    public bool ApplyBinarization { get; set; } = false;

    /// <summary>
    /// Gets or sets the binarization threshold (0-255).
    /// </summary>
    public int BinarizationThreshold { get; set; } = 128;

    #endregion

    /// <summary>
    /// Creates a default OcrOptions instance.
    /// </summary>
    public static OcrOptions Default => new();

    /// <summary>
    /// Creates options optimized for screen text (UI elements, dialogs).
    /// </summary>
    public static OcrOptions ForScreenText => new()
    {
        EnablePreprocessing = true,
        ConvertToGrayscale = true,
        ScaleFactor = 2,
        PageSegMode = PageSegmentationMode.Auto,
        EnginePreference = OcrEnginePreference.WindowsFirst
    };

    /// <summary>
    /// Creates options optimized for document text (scanned documents).
    /// </summary>
    public static OcrOptions ForDocument => new()
    {
        EnablePreprocessing = true,
        ConvertToGrayscale = true,
        ApplyDeskew = true,
        ScaleFactor = 2,
        PageSegMode = PageSegmentationMode.SingleBlock,
        EnginePreference = OcrEnginePreference.TesseractFirst
    };

    /// <summary>
    /// Creates options for maximum accuracy, using PaddleOCR.
    /// </summary>
    public static OcrOptions MaxAccuracy => new()
    {
        EnablePreprocessing = true,
        ScaleFactor = 2,
        EnginePreference = OcrEnginePreference.SmartFallback,
        MinConfidence = 0.8
    };
}

/// <summary>
/// Specifies the preferred OCR engine ordering.
/// </summary>
public enum OcrEnginePreference
{
    /// <summary>
    /// Try Windows OCR first, then Tesseract, then PaddleOCR.
    /// </summary>
    WindowsFirst,

    /// <summary>
    /// Try Tesseract first, then Windows OCR, then PaddleOCR.
    /// </summary>
    TesseractFirst,

    /// <summary>
    /// Use only Windows OCR.
    /// </summary>
    WindowsOnly,

    /// <summary>
    /// Use only Tesseract OCR.
    /// </summary>
    TesseractOnly,

    /// <summary>
    /// Use only PaddleOCR via gRPC (slowest but most powerful).
    /// </summary>
    PaddleOcrOnly,

    /// <summary>
    /// Run all available engines and use the result with highest confidence.
    /// </summary>
    BestConfidence,

    /// <summary>
    /// Try fast engines first, use PaddleOCR only when results are unsatisfactory.
    /// </summary>
    SmartFallback
}

/// <summary>
/// Page segmentation modes for OCR.
/// </summary>
public enum PageSegmentationMode
{
    /// <summary>
    /// Automatic page segmentation with OSD.
    /// </summary>
    Auto,

    /// <summary>
    /// Assume a single uniform block of text.
    /// </summary>
    SingleBlock,

    /// <summary>
    /// Assume a single column of text of variable sizes.
    /// </summary>
    SingleColumn,

    /// <summary>
    /// Treat the image as a single text line.
    /// </summary>
    SingleLine,

    /// <summary>
    /// Treat the image as a single word.
    /// </summary>
    SingleWord,

    /// <summary>
    /// Treat the image as a single word in a circle.
    /// </summary>
    CircleWord,

    /// <summary>
    /// Treat the image as a single character.
    /// </summary>
    SingleChar,

    /// <summary>
    /// Find as much text as possible in no particular order (sparse text).
    /// </summary>
    SparseText,

    /// <summary>
    /// Sparse text with OSD.
    /// </summary>
    SparseTextOsd
}

