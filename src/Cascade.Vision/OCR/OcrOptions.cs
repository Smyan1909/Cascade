namespace Cascade.Vision.OCR;

public class OcrOptions
{
    public string Language { get; set; } = "en-US";
    public IReadOnlyList<string> AdditionalLanguages { get; set; } = Array.Empty<string>();
    public OcrEnginePreference EnginePreference { get; set; } = OcrEnginePreference.WindowsFirst;
    public bool EnablePreprocessing { get; set; } = true;
    public double MinConfidence { get; set; } = 0.5;
    public PageSegmentationMode PageSegMode { get; set; } = PageSegmentationMode.Auto;
    public bool ConvertToGrayscale { get; set; } = true;
    public bool ApplyDeskew { get; set; } = true;
    public bool EnhanceContrast { get; set; } = true;
    public int ScaleFactor { get; set; } = 2;
}

public enum OcrEnginePreference
{
    WindowsFirst,
    TesseractFirst,
    WindowsOnly,
    TesseractOnly,
    PaddleOcrOnly,
    BestConfidence,
    SmartFallback
}

public enum PageSegmentationMode
{
    Auto,
    SingleBlock,
    SingleColumn,
    SingleLine,
    SingleWord,
    CircleWord,
    SingleChar,
    SparseText,
    SparseTextOsd
}


