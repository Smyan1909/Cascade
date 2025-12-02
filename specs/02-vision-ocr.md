# Vision and OCR Module Specification

## Overview

The `Cascade.Vision` module provides visual analysis capabilities including screenshot capture, OCR (Optical Character Recognition), visual element detection, and change detection. This module complements UI Automation by handling scenarios where programmatic element access is limited.

The OCR system uses a tiered approach optimized for speed:
1. **Windows OCR** (Primary) - Fastest, built-in Windows 10+ engine
2. **Tesseract** (Secondary) - Fast local fallback with broad language support  
3. **PaddleOCR via gRPC** (Fallback) - Vision Transformer-based engine for difficult cases

## Dependencies

```xml
<PackageReference Include="Microsoft.Windows.SDK.Contracts" Version="10.0.22621.755" />
<PackageReference Include="System.Drawing.Common" Version="8.0.0" />
<PackageReference Include="Tesseract" Version="5.2.0" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.0" />
<PackageReference Include="Grpc.Net.Client" Version="2.59.0" />
<PackageReference Include="Google.Protobuf" Version="3.25.1" />
```

## Architecture

```
Cascade.Vision/
├── Capture/
│   ├── IScreenCapture.cs           # Capture interface
│   ├── ScreenCapture.cs            # Implementation
│   ├── CaptureOptions.cs           # Configuration
│   ├── CaptureResult.cs            # Result model
│   └── RegionSelector.cs           # Region selection helpers
├── OCR/
│   ├── IOcrEngine.cs               # OCR interface
│   ├── WindowsOcrEngine.cs         # Windows.Media.Ocr (primary - fastest)
│   ├── TesseractOcrEngine.cs       # Tesseract (secondary)
│   ├── PaddleOcrEngine.cs          # PaddleOCR via gRPC (fallback - ViT-based)
│   ├── CompositeOcrEngine.cs       # Smart engine orchestration
│   ├── OcrResult.cs                # OCR result model
│   └── OcrOptions.cs               # Configuration
├── Analysis/
│   ├── IElementAnalyzer.cs         # Visual element detection
│   ├── ElementAnalyzer.cs          # Implementation
│   ├── VisualElement.cs            # Detected element model
│   ├── ContrastAnalyzer.cs         # Text/background contrast
│   └── LayoutAnalyzer.cs           # UI layout detection
├── Comparison/
│   ├── IChangeDetector.cs          # Change detection interface
│   ├── ChangeDetector.cs           # Implementation
│   ├── ChangeResult.cs             # Change result model
│   └── ComparisonOptions.cs        # Configuration
├── Processing/
│   ├── ImageProcessor.cs           # Image manipulation
│   ├── PreprocessingPipeline.cs    # OCR preprocessing
│   └── ImageFilters.cs             # Various filters
└── Services/
    ├── VisionService.cs            # Main service facade
    └── VisionOptions.cs            # Configuration

python/paddle_ocr_service/
├── __init__.py
├── server.py                       # gRPC server hosting PaddleOCR
├── ocr_service.py                  # Service implementation with ViTSTR/SVTR
├── models.py                       # Model loading and management
└── requirements.txt                # PaddlePaddle, PaddleOCR, grpcio
```

## Core Interfaces

### IScreenCapture

```csharp
public interface IScreenCapture
{
    // Full screen capture
    Task<CaptureResult> CaptureScreenAsync(int screenIndex = 0);
    Task<CaptureResult> CaptureAllScreensAsync();
    
    // Window capture
    Task<CaptureResult> CaptureWindowAsync(IntPtr windowHandle);
    Task<CaptureResult> CaptureWindowAsync(string windowTitle);
    Task<CaptureResult> CaptureForegroundWindowAsync();
    
    // Region capture
    Task<CaptureResult> CaptureRegionAsync(Rectangle region);
    Task<CaptureResult> CaptureElementAsync(IUIElement element);
    
    // Interactive capture
    Task<CaptureResult> CaptureInteractiveAsync(); // User selects region
    
    // Configuration
    CaptureOptions Options { get; set; }
}
```

### CaptureResult

```csharp
public class CaptureResult
{
    public byte[] ImageData { get; set; }
    public string ImageFormat { get; set; } // "png", "jpeg", "bmp"
    public int Width { get; set; }
    public int Height { get; set; }
    public Rectangle CapturedRegion { get; set; }
    public DateTime CapturedAt { get; set; }
    public IntPtr? SourceWindowHandle { get; set; }
    
    // Convenience methods
    public Image ToImage();
    public Bitmap ToBitmap();
    public string ToBase64();
    public Task SaveAsync(string filePath);
    public Task<Stream> ToStreamAsync();
}
```

### CaptureOptions

```csharp
public class CaptureOptions
{
    public string ImageFormat { get; set; } = "png";
    public int JpegQuality { get; set; } = 90;
    public bool IncludeCursor { get; set; } = false;
    public bool CaptureShadow { get; set; } = false;
    public double Scale { get; set; } = 1.0;
    public Rectangle? CropRegion { get; set; }
    public bool RemoveTransparency { get; set; } = true;
    public Color TransparencyReplacement { get; set; } = Color.White;
}
```

## OCR Interfaces

### IOcrEngine

```csharp
public interface IOcrEngine
{
    // Single image OCR
    Task<OcrResult> RecognizeAsync(byte[] imageData);
    Task<OcrResult> RecognizeAsync(string imagePath);
    Task<OcrResult> RecognizeAsync(CaptureResult capture);
    
    // Region-specific OCR
    Task<OcrResult> RecognizeRegionAsync(byte[] imageData, Rectangle region);
    
    // Configuration
    OcrOptions Options { get; set; }
    
    // Engine info
    string EngineName { get; }
    IReadOnlyList<string> SupportedLanguages { get; }
    bool IsAvailable { get; }
}
```

### OcrResult

```csharp
public class OcrResult
{
    public string FullText { get; set; }
    public double Confidence { get; set; }
    public IReadOnlyList<OcrLine> Lines { get; set; }
    public IReadOnlyList<OcrWord> Words { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public string EngineUsed { get; set; }
    
    // Search within results
    public IReadOnlyList<OcrWord> FindWords(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase);
    public OcrWord? FindFirstWord(string text);
    public Rectangle? GetTextBounds(string text);
}

public class OcrLine
{
    public string Text { get; set; }
    public Rectangle BoundingBox { get; set; }
    public double Confidence { get; set; }
    public IReadOnlyList<OcrWord> Words { get; set; }
}

public class OcrWord
{
    public string Text { get; set; }
    public Rectangle BoundingBox { get; set; }
    public double Confidence { get; set; }
    public Point Center => new Point(
        BoundingBox.X + BoundingBox.Width / 2,
        BoundingBox.Y + BoundingBox.Height / 2);
}
```

### OcrOptions

```csharp
public class OcrOptions
{
    public string Language { get; set; } = "en-US";
    public IReadOnlyList<string> AdditionalLanguages { get; set; } = new List<string>();
    public OcrEnginePreference EnginePreference { get; set; } = OcrEnginePreference.WindowsFirst;
    public bool EnablePreprocessing { get; set; } = true;
    public double MinConfidence { get; set; } = 0.5;
    public PageSegmentationMode PageSegMode { get; set; } = PageSegmentationMode.Auto;
    
    // Preprocessing options
    public bool ConvertToGrayscale { get; set; } = true;
    public bool ApplyDeskew { get; set; } = true;
    public bool EnhanceContrast { get; set; } = true;
    public int ScaleFactor { get; set; } = 2; // Upscale for better accuracy
}

public enum OcrEnginePreference
{
    WindowsFirst,      // Try Windows OCR, fallback to Tesseract, then PaddleOCR
    TesseractFirst,    // Try Tesseract, fallback to Windows OCR, then PaddleOCR
    WindowsOnly,
    TesseractOnly,
    PaddleOcrOnly,     // Use PaddleOCR via gRPC (slowest but most powerful)
    BestConfidence,    // Run all engines, use result with highest confidence
    SmartFallback      // Try fast engines first, use PaddleOCR only when needed
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
```

## Windows OCR Engine

```csharp
public class WindowsOcrEngine : IOcrEngine
{
    // Uses Windows.Media.Ocr from UWP
    // Pros: No external dependencies, fast, good quality
    // Cons: Windows 10+ only, limited language support
    
    public string EngineName => "Windows.Media.Ocr";
    
    public IReadOnlyList<string> SupportedLanguages { get; }
    
    public bool IsAvailable { get; } // Check if Windows OCR is available
}
```

## Tesseract OCR Engine

```csharp
public class TesseractOcrEngine : IOcrEngine
{
    // Uses Tesseract via Tesseract.NET
    // Pros: Many languages, highly configurable, cross-platform
    // Cons: Requires tessdata files, can be slower
    
    public string EngineName => "Tesseract";
    
    public string TessDataPath { get; set; }
    
    // Additional Tesseract-specific options
    public string? Whitelist { get; set; }
    public string? Blacklist { get; set; }
}
```

## PaddleOCR Engine (Vision Transformer Fallback)

PaddleOCR provides state-of-the-art OCR capabilities using Vision Transformer (ViT) based models. It runs as a separate Python gRPC service for maximum accuracy when Windows OCR and Tesseract fail to recognize text.

### Architecture

```
┌─────────────────────┐         gRPC          ┌──────────────────────────┐
│  Cascade.Vision     │ ◄──────────────────► │  Python PaddleOCR Service │
│  (PaddleOcrEngine)  │                       │  (ViTSTR / SVTR models)   │
└─────────────────────┘                       └──────────────────────────┘
```

### Model Options

PaddleOCR supports multiple Vision Transformer-based recognition models:

| Model | Description | Use Case |
|-------|-------------|----------|
| **ViTSTR** | Vision Transformer for Scene Text Recognition | General text, ~80% accuracy on benchmarks |
| **SVTR** | Single Visual Model for Scene Text Recognition | Faster inference, good for real-time |
| **PP-OCRv4** | Latest PaddleOCR model with ViT backbone | Best overall accuracy |

### PaddleOcrEngine Interface

```csharp
public class PaddleOcrEngine : IOcrEngine
{
    // Uses PaddleOCR via gRPC service
    // Pros: State-of-the-art accuracy, Vision Transformer based, handles difficult text
    // Cons: Requires Python service, slower due to network call, needs GPU for best performance
    
    public string EngineName => "PaddleOCR";
    
    // gRPC connection settings
    public string ServiceEndpoint { get; set; } = "localhost:50052";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public bool EnableRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    
    // Model selection
    public PaddleOcrModel Model { get; set; } = PaddleOcrModel.PPOCRv4;
    
    // Performance options
    public bool UseGpu { get; set; } = false;
    public int GpuMemoryLimit { get; set; } = 500; // MB
    
    public bool IsAvailable { get; } // Check if gRPC service is reachable
    
    public IReadOnlyList<string> SupportedLanguages => new[] 
    { 
        "en", "ch", "japan", "korean", "french", "german", 
        "arabic", "cyrillic", "latin", "devanagari" 
    };
}

public enum PaddleOcrModel
{
    ViTSTR,      // Vision Transformer - balanced
    SVTR,        // Single Visual Model - faster
    PPOCRv4      // Latest model - best accuracy (default)
}
```

### PaddleOCR gRPC Service (Python)

The Python service hosts PaddleOCR and exposes it via gRPC:

```python
# python/paddle_ocr_service/ocr_service.py

from paddleocr import PaddleOCR
import grpc
from concurrent import futures

class PaddleOcrServicer(vision_pb2_grpc.PaddleOcrServiceServicer):
    def __init__(self, model='PP-OCRv4', use_gpu=False):
        self.ocr = PaddleOCR(
            use_angle_cls=True,
            lang='en',
            use_gpu=use_gpu,
            rec_algorithm='SVTR_LCNet',  # or 'ViTSTR'
            show_log=False
        )
    
    def Recognize(self, request, context):
        # Convert image bytes to numpy array
        image = np.frombuffer(request.image_data, dtype=np.uint8)
        image = cv2.imdecode(image, cv2.IMREAD_COLOR)
        
        # Run OCR
        result = self.ocr.ocr(image, cls=True)
        
        # Build response with bounding boxes and confidence
        return self._build_response(result)
```

### Smart Fallback Logic in CompositeOcrEngine

```csharp
public class CompositeOcrEngine : IOcrEngine
{
    private readonly WindowsOcrEngine _windowsOcr;
    private readonly TesseractOcrEngine _tesseract;
    private readonly PaddleOcrEngine _paddleOcr;
    
    public double ConfidenceThreshold { get; set; } = 0.7;
    public double MinTextLengthForFallback { get; set; } = 3;
    
    public async Task<OcrResult> RecognizeAsync(byte[] imageData)
    {
        // 1. Try Windows OCR first (fastest)
        if (_windowsOcr.IsAvailable)
        {
            var result = await _windowsOcr.RecognizeAsync(imageData);
            if (IsResultAcceptable(result))
                return result;
        }
        
        // 2. Try Tesseract (fast, local)
        var tesseractResult = await _tesseract.RecognizeAsync(imageData);
        if (IsResultAcceptable(tesseractResult))
            return tesseractResult;
        
        // 3. Fallback to PaddleOCR (slowest but most powerful)
        if (_paddleOcr.IsAvailable)
        {
            var paddleResult = await _paddleOcr.RecognizeAsync(imageData);
            return paddleResult; // Always accept PaddleOCR result as final fallback
        }
        
        // Return best result from available engines
        return tesseractResult;
    }
    
    private bool IsResultAcceptable(OcrResult result)
    {
        // Reject if confidence too low
        if (result.Confidence < ConfidenceThreshold)
            return false;
        
        // Reject if no meaningful text found
        if (string.IsNullOrWhiteSpace(result.FullText) || 
            result.FullText.Length < MinTextLengthForFallback)
            return false;
        
        return true;
    }
    
    // Targeted fallback - use when searching for specific text
    public async Task<OcrResult> RecognizeWithTargetAsync(byte[] imageData, string targetText)
    {
        // Try fast engines first
        var result = await RecognizeAsync(imageData);
        
        // If target text found, return immediately
        if (result.FindFirstWord(targetText) != null)
            return result;
        
        // Target not found - try PaddleOCR as last resort
        if (_paddleOcr.IsAvailable)
        {
            return await _paddleOcr.RecognizeAsync(imageData);
        }
        
        return result;
    }
}
```

### PaddleOCR Service Proto Definition

```protobuf
// In protos/vision.proto

service PaddleOcrService {
    // Basic OCR recognition
    rpc Recognize(PaddleOcrRequest) returns (PaddleOcrResponse);
    
    // Streaming for multiple images
    rpc RecognizeBatch(stream PaddleOcrRequest) returns (stream PaddleOcrResponse);
    
    // Health check
    rpc GetStatus(Empty) returns (PaddleOcrStatus);
}

message PaddleOcrRequest {
    bytes image_data = 1;
    string language = 2;
    PaddleOcrModel model = 3;
    bool use_angle_classifier = 4;
    bool detect_only = 5;  // Only detect text regions, don't recognize
}

message PaddleOcrResponse {
    bool success = 1;
    string error_message = 2;
    string full_text = 3;
    double confidence = 4;
    repeated PaddleOcrLine lines = 5;
    int32 processing_time_ms = 6;
    string model_used = 7;
}

message PaddleOcrLine {
    string text = 1;
    repeated PaddleOcrPoint polygon = 2;  // 4-point polygon for rotated text
    double confidence = 3;
    repeated PaddleOcrWord words = 4;
}

message PaddleOcrPoint {
    int32 x = 1;
    int32 y = 2;
}

message PaddleOcrWord {
    string text = 1;
    repeated PaddleOcrPoint polygon = 2;
    double confidence = 3;
}

message PaddleOcrStatus {
    bool is_ready = 1;
    string model_loaded = 2;
    bool gpu_available = 3;
    int32 gpu_memory_used_mb = 4;
}

enum PaddleOcrModel {
    PADDLE_OCR_MODEL_UNSPECIFIED = 0;
    PADDLE_OCR_MODEL_VITSTR = 1;
    PADDLE_OCR_MODEL_SVTR = 2;
    PADDLE_OCR_MODEL_PP_OCRV4 = 3;
}
```

### Python Service Requirements

```
# python/paddle_ocr_service/requirements.txt
paddlepaddle>=2.5.0
paddleocr>=2.7.0
grpcio>=1.59.0
grpcio-tools>=1.59.0
protobuf>=4.24.0
numpy>=1.24.0
opencv-python>=4.8.0
Pillow>=10.0.0
```

### When to Use PaddleOCR Fallback

PaddleOCR is invoked automatically when:
- Windows OCR and Tesseract both return confidence < 70%
- No text is detected by primary engines
- User explicitly requests PaddleOCR via `OcrEnginePreference.PaddleOcrOnly`
- Searching for specific text that isn't found by faster engines

Benefits of Vision Transformer models:
- Better handling of rotated/skewed text
- Superior scene text recognition (text in photos)
- Multi-language support with single model
- Handles low-contrast and degraded images better

## Visual Element Analysis

### IElementAnalyzer

```csharp
public interface IElementAnalyzer
{
    // Detect visual elements in image
    Task<IReadOnlyList<VisualElement>> DetectElementsAsync(byte[] imageData);
    Task<IReadOnlyList<VisualElement>> DetectElementsAsync(CaptureResult capture);
    
    // Detect specific element types
    Task<IReadOnlyList<VisualElement>> DetectButtonsAsync(byte[] imageData);
    Task<IReadOnlyList<VisualElement>> DetectTextFieldsAsync(byte[] imageData);
    Task<IReadOnlyList<VisualElement>> DetectIconsAsync(byte[] imageData);
    
    // Layout analysis
    Task<LayoutAnalysis> AnalyzeLayoutAsync(byte[] imageData);
}
```

### VisualElement

```csharp
public class VisualElement
{
    public VisualElementType Type { get; set; }
    public Rectangle BoundingBox { get; set; }
    public double Confidence { get; set; }
    public string? Text { get; set; }  // If text was detected
    public Color? DominantColor { get; set; }
    public Color? BackgroundColor { get; set; }
    public bool IsClickable { get; set; }
    public Dictionary<string, object> Properties { get; set; }
}

public enum VisualElementType
{
    Unknown,
    Button,
    TextBox,
    Label,
    Image,
    Icon,
    Checkbox,
    RadioButton,
    Dropdown,
    Menu,
    MenuBar,
    Toolbar,
    StatusBar,
    Tab,
    Link,
    Table,
    List,
    Tree,
    Dialog,
    Panel
}
```

### LayoutAnalysis

```csharp
public class LayoutAnalysis
{
    public IReadOnlyList<LayoutRegion> Regions { get; set; }
    public LayoutType DetectedLayout { get; set; }
    public Rectangle ContentArea { get; set; }
    public Rectangle? HeaderArea { get; set; }
    public Rectangle? FooterArea { get; set; }
    public Rectangle? SidebarArea { get; set; }
    public Rectangle? NavigationArea { get; set; }
}

public class LayoutRegion
{
    public string Name { get; set; }
    public Rectangle Bounds { get; set; }
    public LayoutRegionType Type { get; set; }
    public IReadOnlyList<VisualElement> Elements { get; set; }
}

public enum LayoutType
{
    Unknown,
    SingleColumn,
    TwoColumn,
    ThreeColumn,
    Dashboard,
    Form,
    List,
    Grid,
    Dialog,
    Wizard
}
```

## Change Detection

### IChangeDetector

```csharp
public interface IChangeDetector
{
    // Compare two images
    Task<ChangeResult> CompareAsync(byte[] baseline, byte[] current);
    Task<ChangeResult> CompareAsync(CaptureResult baseline, CaptureResult current);
    
    // Compare with baseline
    Task SetBaselineAsync(byte[] imageData);
    Task<ChangeResult> CompareWithBaselineAsync(byte[] current);
    
    // Monitor for changes
    Task<ChangeResult> WaitForChangeAsync(IScreenCapture capture, Rectangle region, TimeSpan timeout);
    Task<ChangeResult> WaitForStabilityAsync(IScreenCapture capture, Rectangle region, TimeSpan stabilityDuration);
}
```

### ChangeResult

```csharp
public class ChangeResult
{
    public bool HasChanges { get; set; }
    public double DifferencePercentage { get; set; }
    public IReadOnlyList<Rectangle> ChangedRegions { get; set; }
    public byte[]? DifferenceImage { get; set; }  // Visual diff
    public ChangeType ChangeType { get; set; }
    
    // Specific change detection
    public bool HasNewElements { get; set; }
    public bool HasRemovedElements { get; set; }
    public bool HasTextChanges { get; set; }
}

public enum ChangeType
{
    None,
    Minor,      // < 5% pixels changed
    Moderate,   // 5-20% pixels changed
    Major,      // > 20% pixels changed
    Complete    // Entirely different content
}
```

### ComparisonOptions

```csharp
public class ComparisonOptions
{
    public double ChangeThreshold { get; set; } = 0.01; // 1% change considered significant
    public bool IgnoreAntiAliasing { get; set; } = true;
    public bool IgnoreMinorColorDifferences { get; set; } = true;
    public int ColorTolerance { get; set; } = 10; // RGB difference tolerance
    public IReadOnlyList<Rectangle>? IgnoreRegions { get; set; }
    public bool GenerateDifferenceImage { get; set; } = true;
    public Color DifferenceHighlightColor { get; set; } = Color.Red;
}
```

## Image Processing

### ImageProcessor

```csharp
public class ImageProcessor
{
    // Basic operations
    public byte[] Crop(byte[] imageData, Rectangle region);
    public byte[] Resize(byte[] imageData, int width, int height, ResizeMode mode = ResizeMode.Fit);
    public byte[] Rotate(byte[] imageData, double degrees);
    public byte[] Flip(byte[] imageData, FlipMode mode);
    
    // Color operations
    public byte[] ToGrayscale(byte[] imageData);
    public byte[] AdjustBrightness(byte[] imageData, float brightness);
    public byte[] AdjustContrast(byte[] imageData, float contrast);
    public byte[] Invert(byte[] imageData);
    
    // Enhancement
    public byte[] Sharpen(byte[] imageData);
    public byte[] Denoise(byte[] imageData);
    public byte[] EnhanceForOcr(byte[] imageData);
    public byte[] Binarize(byte[] imageData, int threshold = 128);
    public byte[] AdaptiveThreshold(byte[] imageData);
    
    // Analysis
    public Color GetDominantColor(byte[] imageData);
    public Color GetAverageColor(byte[] imageData, Rectangle? region = null);
    public double GetBrightness(byte[] imageData);
    public double GetContrast(byte[] imageData);
}
```

### PreprocessingPipeline

```csharp
public class PreprocessingPipeline
{
    // Build preprocessing steps for OCR
    public PreprocessingPipeline AddGrayscale();
    public PreprocessingPipeline AddResize(int scale);
    public PreprocessingPipeline AddDeskew();
    public PreprocessingPipeline AddBinarize(int threshold);
    public PreprocessingPipeline AddDenoise();
    public PreprocessingPipeline AddSharpen();
    public PreprocessingPipeline AddCustom(Func<byte[], byte[]> processor);
    
    // Execute pipeline
    public byte[] Process(byte[] imageData);
    
    // Predefined pipelines
    public static PreprocessingPipeline ForScreenText { get; }
    public static PreprocessingPipeline ForDocument { get; }
    public static PreprocessingPipeline ForLowContrast { get; }
}
```

## Service Configuration

```csharp
public class VisionOptions
{
    // Screenshot options
    public CaptureOptions DefaultCaptureOptions { get; set; } = new();
    
    // OCR options
    public OcrOptions DefaultOcrOptions { get; set; } = new();
    public string? TesseractDataPath { get; set; }
    
    // PaddleOCR gRPC service options
    public PaddleOcrOptions PaddleOcr { get; set; } = new();
    
    // Change detection
    public ComparisonOptions DefaultComparisonOptions { get; set; } = new();
    
    // Performance
    public bool EnableCaching { get; set; } = true;
    public int MaxCachedScreenshots { get; set; } = 10;
    
    // Logging
    public bool SaveDebugImages { get; set; } = false;
    public string? DebugImagePath { get; set; }
}

public class PaddleOcrOptions
{
    // gRPC connection
    public string ServiceEndpoint { get; set; } = "localhost:50052";
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    // Retry policy
    public bool EnableRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    
    // Model configuration
    public PaddleOcrModel DefaultModel { get; set; } = PaddleOcrModel.PPOCRv4;
    public string DefaultLanguage { get; set; } = "en";
    public bool UseAngleClassifier { get; set; } = true;
    
    // Fallback behavior
    public double FallbackConfidenceThreshold { get; set; } = 0.7;
    public bool EnableAutoFallback { get; set; } = true;
}
```

## Integration with UI Automation

```csharp
public class HybridElementFinder
{
    private readonly IElementDiscovery _uiaDiscovery;
    private readonly IScreenCapture _screenCapture;
    private readonly IOcrEngine _ocrEngine;
    private readonly IElementAnalyzer _elementAnalyzer;
    
    // Find element using combined approaches
    public async Task<HybridElement?> FindElementAsync(string text)
    {
        // 1. Try UI Automation first
        var uiaElement = _uiaDiscovery.FindElement(
            SearchCriteria.ByName(text).Or(
            SearchCriteria.ByAutomationId(text)));
        
        if (uiaElement != null)
            return new HybridElement(uiaElement);
        
        // 2. Fallback to OCR
        var capture = await _screenCapture.CaptureForegroundWindowAsync();
        var ocrResult = await _ocrEngine.RecognizeAsync(capture);
        
        var word = ocrResult.FindFirstWord(text);
        if (word != null)
            return new HybridElement(word.BoundingBox, text);
        
        // 3. Try visual detection
        var elements = await _elementAnalyzer.DetectElementsAsync(capture);
        var visualElement = elements.FirstOrDefault(e => 
            e.Text?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
        
        if (visualElement != null)
            return new HybridElement(visualElement);
        
        return null;
    }
}

public class HybridElement
{
    public IUIElement? UIAElement { get; }
    public Rectangle BoundingBox { get; }
    public string? DetectedText { get; }
    public HybridElementSource Source { get; }
    
    public async Task ClickAsync()
    {
        if (UIAElement != null)
        {
            await UIAElement.ClickAsync();
        }
        else
        {
            // Click at center of bounding box
            var center = new Point(
                BoundingBox.X + BoundingBox.Width / 2,
                BoundingBox.Y + BoundingBox.Height / 2);
            await MouseOperations.ClickAtAsync(center);
        }
    }
}

public enum HybridElementSource
{
    UIAutomation,
    OCR,
    VisualDetection
}
```

## Usage Examples

### Screenshot and OCR
```csharp
var capture = await screenCapture.CaptureForegroundWindowAsync();
var ocrResult = await ocrEngine.RecognizeAsync(capture);

Console.WriteLine($"Recognized text: {ocrResult.FullText}");
Console.WriteLine($"Confidence: {ocrResult.Confidence:P}");

// Find specific text and click it
var submitButton = ocrResult.FindFirstWord("Submit");
if (submitButton != null)
{
    await MouseOperations.ClickAtAsync(submitButton.Center);
}
```

### Change Detection
```csharp
var detector = new ChangeDetector();
await detector.SetBaselineAsync(initialScreenshot);

// Perform action
await button.ClickAsync();

// Wait for UI to change
var result = await detector.WaitForChangeAsync(
    screenCapture, 
    expectedChangeRegion,
    TimeSpan.FromSeconds(5));

if (result.HasChanges)
{
    Console.WriteLine($"UI changed: {result.DifferencePercentage:P}");
}
```

### Layout Analysis
```csharp
var capture = await screenCapture.CaptureWindowAsync("Notepad");
var layout = await elementAnalyzer.AnalyzeLayoutAsync(capture.ImageData);

Console.WriteLine($"Layout type: {layout.DetectedLayout}");
foreach (var region in layout.Regions)
{
    Console.WriteLine($"  {region.Type}: {region.Bounds}");
}
```


