using System.Drawing;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.Vision.Analysis;
using Cascade.Vision.Capture;
using Cascade.Vision.OCR;

namespace Cascade.Vision.Services;

/// <summary>
/// Finds UI elements using a combination of UI Automation and OCR.
/// </summary>
public class HybridElementFinder
{
    private readonly IElementDiscovery _uiaDiscovery;
    private readonly IScreenCapture _screenCapture;
    private readonly IOcrEngine _ocrEngine;
    private readonly IElementAnalyzer _elementAnalyzer;

    /// <summary>
    /// Creates a new HybridElementFinder.
    /// </summary>
    /// <param name="uiaDiscovery">UI Automation element discovery.</param>
    /// <param name="screenCapture">Screen capture service.</param>
    /// <param name="ocrEngine">OCR engine.</param>
    /// <param name="elementAnalyzer">Visual element analyzer.</param>
    public HybridElementFinder(
        IElementDiscovery uiaDiscovery,
        IScreenCapture screenCapture,
        IOcrEngine ocrEngine,
        IElementAnalyzer elementAnalyzer)
    {
        _uiaDiscovery = uiaDiscovery;
        _screenCapture = screenCapture;
        _ocrEngine = ocrEngine;
        _elementAnalyzer = elementAnalyzer;
    }

    /// <summary>
    /// Finds an element using combined approaches.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The found element, or null if not found.</returns>
    public async Task<HybridElement?> FindElementAsync(
        string text, 
        CancellationToken cancellationToken = default)
    {
        // 1. Try UI Automation first (fastest, most reliable)
        try
        {
            var uiaElement = _uiaDiscovery.FindElement(
                SearchCriteria.ByName(text).Or(
                SearchCriteria.ByAutomationId(text)));

            if (uiaElement != null)
            {
                return new HybridElement(uiaElement);
            }
        }
        catch
        {
            // Continue to OCR fallback
        }

        // 2. Fallback to OCR
        try
        {
            var capture = await _screenCapture.CaptureForegroundWindowAsync(cancellationToken);
            var ocrResult = await _ocrEngine.RecognizeAsync(capture, cancellationToken);

            var word = ocrResult.FindFirstWord(text);
            if (word != null)
            {
                return new HybridElement(word.BoundingBox, text, HybridElementSource.OCR);
            }

            // Try partial match
            var containingWords = ocrResult.FindWordsContaining(text);
            if (containingWords.Count > 0)
            {
                return new HybridElement(containingWords[0].BoundingBox, containingWords[0].Text, HybridElementSource.OCR);
            }
        }
        catch
        {
            // Continue to visual detection
        }

        // 3. Try visual element detection
        try
        {
            var capture = await _screenCapture.CaptureForegroundWindowAsync(cancellationToken);
            var elements = await _elementAnalyzer.DetectElementsAsync(capture, cancellationToken);

            var visualElement = elements.FirstOrDefault(e =>
                e.Text?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);

            if (visualElement != null)
            {
                return new HybridElement(visualElement);
            }
        }
        catch
        {
            // Return null if all methods fail
        }

        return null;
    }

    /// <summary>
    /// Finds all elements matching the text.
    /// </summary>
    public async Task<IReadOnlyList<HybridElement>> FindAllElementsAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var results = new List<HybridElement>();

        // Try UI Automation
        try
        {
            var criteria = SearchCriteria.ByName(text).Or(SearchCriteria.ByAutomationId(text));
            var uiaElements = _uiaDiscovery.FindAllElements(criteria);

            foreach (var element in uiaElements)
            {
                results.Add(new HybridElement(element));
            }
        }
        catch
        {
            // Continue to other methods
        }

        // If no UIA elements found, try OCR
        if (results.Count == 0)
        {
            try
            {
                var capture = await _screenCapture.CaptureForegroundWindowAsync(cancellationToken);
                var ocrResult = await _ocrEngine.RecognizeAsync(capture, cancellationToken);

                var words = ocrResult.FindWords(text);
                foreach (var word in words)
                {
                    results.Add(new HybridElement(word.BoundingBox, word.Text, HybridElementSource.OCR));
                }
            }
            catch
            {
                // Return what we have
            }
        }

        return results;
    }

    /// <summary>
    /// Finds a clickable element (button, link, etc.) by text.
    /// </summary>
    public async Task<HybridElement?> FindClickableElementAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Try UI Automation for buttons, links, etc.
        try
        {
            // Try to find by name with button or hyperlink control type
            var buttonCriteria = SearchCriteria.ByName(text)
                .And(SearchCriteria.ByControlType(UIAutomation.Enums.ControlType.Button));
            var uiaElement = _uiaDiscovery.FindElement(buttonCriteria);
            
            if (uiaElement == null)
            {
                var linkCriteria = SearchCriteria.ByName(text)
                    .And(SearchCriteria.ByControlType(UIAutomation.Enums.ControlType.Hyperlink));
                uiaElement = _uiaDiscovery.FindElement(linkCriteria);
            }
            
            if (uiaElement != null)
            {
                return new HybridElement(uiaElement);
            }
        }
        catch
        {
            // Continue to visual detection
        }

        // Fall back to visual detection for clickable elements
        try
        {
            var capture = await _screenCapture.CaptureForegroundWindowAsync(cancellationToken);
            var elements = await _elementAnalyzer.DetectElementsAsync(capture, cancellationToken);

            var clickable = elements
                .Where(e => e.IsClickable && 
                           (e.Text?.Contains(text, StringComparison.OrdinalIgnoreCase) == true ||
                            e.Type == VisualElementType.Button))
                .FirstOrDefault();

            if (clickable != null)
            {
                return new HybridElement(clickable);
            }
        }
        catch
        {
            // Return null
        }

        // Last resort: find any element with the text
        return await FindElementAsync(text, cancellationToken);
    }
}

/// <summary>
/// Represents an element found through hybrid detection.
/// </summary>
public class HybridElement
{
    /// <summary>
    /// Gets the UI Automation element if available.
    /// </summary>
    public IUIElement? UIAElement { get; }

    /// <summary>
    /// Gets the bounding rectangle of the element.
    /// </summary>
    public Rectangle BoundingBox { get; }

    /// <summary>
    /// Gets the detected text of the element.
    /// </summary>
    public string? DetectedText { get; }

    /// <summary>
    /// Gets the source that found this element.
    /// </summary>
    public HybridElementSource Source { get; }

    /// <summary>
    /// Gets the center point of the element.
    /// </summary>
    public Point Center => new(
        BoundingBox.X + BoundingBox.Width / 2,
        BoundingBox.Y + BoundingBox.Height / 2);

    /// <summary>
    /// Gets whether this element can be clicked using UI Automation.
    /// </summary>
    public bool CanUseUIAutomation => UIAElement != null;

    /// <summary>
    /// Creates a HybridElement from a UI Automation element.
    /// </summary>
    public HybridElement(IUIElement uiaElement)
    {
        UIAElement = uiaElement;
        BoundingBox = uiaElement.BoundingRectangle;
        DetectedText = uiaElement.Name;
        Source = HybridElementSource.UIAutomation;
    }

    /// <summary>
    /// Creates a HybridElement from a bounding box (OCR result).
    /// </summary>
    public HybridElement(Rectangle boundingBox, string? text, HybridElementSource source)
    {
        BoundingBox = boundingBox;
        DetectedText = text;
        Source = source;
    }

    /// <summary>
    /// Creates a HybridElement from a visual element.
    /// </summary>
    public HybridElement(VisualElement visualElement)
    {
        BoundingBox = visualElement.BoundingBox;
        DetectedText = visualElement.Text;
        Source = HybridElementSource.VisualDetection;
    }

    /// <summary>
    /// Clicks the element.
    /// </summary>
    public async Task ClickAsync()
    {
        if (UIAElement != null)
        {
            await UIAElement.ClickAsync();
        }
        else
        {
            // Click at center using input simulation
            UIAutomation.Interop.InputSimulator.LeftClick(Center.X, Center.Y);
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Double-clicks the element.
    /// </summary>
    public async Task DoubleClickAsync()
    {
        if (UIAElement != null)
        {
            await UIAElement.DoubleClickAsync();
        }
        else
        {
            UIAutomation.Interop.InputSimulator.DoubleClick(Center.X, Center.Y);
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Right-clicks the element.
    /// </summary>
    public async Task RightClickAsync()
    {
        if (UIAElement != null)
        {
            await UIAElement.RightClickAsync();
        }
        else
        {
            UIAutomation.Interop.InputSimulator.RightClick(Center.X, Center.Y);
            await Task.CompletedTask;
        }
    }
}

/// <summary>
/// Indicates the source that found a hybrid element.
/// </summary>
public enum HybridElementSource
{
    /// <summary>Found via UI Automation.</summary>
    UIAutomation,

    /// <summary>Found via OCR text recognition.</summary>
    OCR,

    /// <summary>Found via visual element detection.</summary>
    VisualDetection
}

