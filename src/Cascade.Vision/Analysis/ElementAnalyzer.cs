using System.Drawing;
using Cascade.Vision.Capture;
using Cascade.Vision.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SharpImage = SixLabors.ImageSharp.Image;

namespace Cascade.Vision.Analysis;

/// <summary>
/// Analyzes images to detect visual UI elements using edge detection and region analysis.
/// </summary>
public class ElementAnalyzer : IElementAnalyzer
{
    private readonly ImageProcessor _imageProcessor = new();

    /// <inheritdoc />
    public async Task<IReadOnlyList<VisualElement>> DetectElementsAsync(
        byte[] imageData, 
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => DetectElementsCore(imageData), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<VisualElement>> DetectElementsAsync(
        CaptureResult capture, 
        CancellationToken cancellationToken = default)
    {
        return DetectElementsAsync(capture.ImageData, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VisualElement>> DetectButtonsAsync(
        byte[] imageData, 
        CancellationToken cancellationToken = default)
    {
        var elements = await DetectElementsAsync(imageData, cancellationToken);
        return elements.Where(e => e.Type == VisualElementType.Button).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VisualElement>> DetectTextFieldsAsync(
        byte[] imageData, 
        CancellationToken cancellationToken = default)
    {
        var elements = await DetectElementsAsync(imageData, cancellationToken);
        return elements.Where(e => e.Type == VisualElementType.TextBox).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VisualElement>> DetectIconsAsync(
        byte[] imageData, 
        CancellationToken cancellationToken = default)
    {
        var elements = await DetectElementsAsync(imageData, cancellationToken);
        return elements.Where(e => e.Type == VisualElementType.Icon).ToList();
    }

    /// <inheritdoc />
    public async Task<LayoutAnalysis> AnalyzeLayoutAsync(
        byte[] imageData, 
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => AnalyzeLayoutCore(imageData), cancellationToken);
    }

    private List<VisualElement> DetectElementsCore(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        var elements = new List<VisualElement>();

        // Detect rectangular regions using edge detection
        var regions = DetectRectangularRegions(image);

        foreach (var region in regions)
        {
            var element = AnalyzeRegion(image, region);
            if (element != null)
            {
                elements.Add(element);
            }
        }

        // Merge overlapping elements
        elements = MergeOverlapping(elements);

        return elements;
    }

    private List<System.Drawing.Rectangle> DetectRectangularRegions(Image<Rgba32> image)
    {
        var regions = new List<System.Drawing.Rectangle>();
        int width = image.Width;
        int height = image.Height;

        // Simple edge-based region detection
        // Look for horizontal and vertical edges to find rectangular regions
        var horizontalEdges = new List<(int Y, int StartX, int EndX)>();
        var verticalEdges = new List<(int X, int StartY, int EndY)>();

        // Detect significant color changes
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 1; y < height - 1; y++)
            {
                var row = accessor.GetRowSpan(y);
                var prevRow = accessor.GetRowSpan(y - 1);

                int edgeStart = -1;
                for (int x = 0; x < width; x++)
                {
                    bool isEdge = GetColorDifference(row[x], prevRow[x]) > 30;
                    
                    if (isEdge && edgeStart == -1)
                    {
                        edgeStart = x;
                    }
                    else if (!isEdge && edgeStart != -1)
                    {
                        if (x - edgeStart > 10) // Minimum edge length
                        {
                            horizontalEdges.Add((y, edgeStart, x));
                        }
                        edgeStart = -1;
                    }
                }
            }
        });

        // Find rectangular regions from edges
        var candidates = FindRectanglesFromEdges(horizontalEdges, width, height);
        
        // Filter and validate regions
        foreach (var rect in candidates)
        {
            if (rect.Width >= 20 && rect.Height >= 15 && 
                rect.Width <= width * 0.9 && rect.Height <= height * 0.9)
            {
                regions.Add(rect);
            }
        }

        return regions;
    }

    private List<System.Drawing.Rectangle> FindRectanglesFromEdges(
        List<(int Y, int StartX, int EndX)> edges,
        int width, int height)
    {
        var rectangles = new List<System.Drawing.Rectangle>();

        // Group edges by approximate Y position
        var edgeGroups = edges
            .GroupBy(e => e.Y / 10 * 10) // Group by 10-pixel bands
            .Where(g => g.Count() > 0)
            .ToList();

        // Find pairs of edges that could form rectangles
        for (int i = 0; i < edgeGroups.Count - 1; i++)
        {
            for (int j = i + 1; j < edgeGroups.Count; j++)
            {
                var topEdges = edgeGroups[i].ToList();
                var bottomEdges = edgeGroups[j].ToList();

                foreach (var top in topEdges)
                {
                    foreach (var bottom in bottomEdges)
                    {
                        // Check if edges align horizontally
                        int overlap = Math.Min(top.EndX, bottom.EndX) - Math.Max(top.StartX, bottom.StartX);
                        if (overlap > 20)
                        {
                            int x = Math.Max(top.StartX, bottom.StartX);
                            int y = top.Y;
                            int w = Math.Min(top.EndX, bottom.EndX) - x;
                            int h = bottom.Y - top.Y;

                            if (h > 15 && h < height * 0.5)
                            {
                                rectangles.Add(new System.Drawing.Rectangle(x, y, w, h));
                            }
                        }
                    }
                }
            }
        }

        return rectangles;
    }

    private VisualElement? AnalyzeRegion(Image<Rgba32> image, System.Drawing.Rectangle region)
    {
        // Get region colors
        var (dominant, background) = GetRegionColors(image, region);
        
        // Classify element type based on characteristics
        var type = ClassifyElementType(region, dominant, background);

        return new VisualElement
        {
            Type = type,
            BoundingBox = region,
            Confidence = 0.7, // Base confidence
            DominantColor = dominant,
            BackgroundColor = background,
            IsClickable = IsLikelyClickable(type, region)
        };
    }

    private (System.Drawing.Color dominant, System.Drawing.Color background) GetRegionColors(
        Image<Rgba32> image, 
        System.Drawing.Rectangle region)
    {
        long totalR = 0, totalG = 0, totalB = 0;
        long edgeR = 0, edgeG = 0, edgeB = 0;
        int totalCount = 0;
        int edgeCount = 0;

        int safeX = Math.Max(0, region.X);
        int safeY = Math.Max(0, region.Y);
        int safeRight = Math.Min(image.Width, region.Right);
        int safeBottom = Math.Min(image.Height, region.Bottom);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = safeY; y < safeBottom; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = safeX; x < safeRight; x++)
                {
                    totalR += row[x].R;
                    totalG += row[x].G;
                    totalB += row[x].B;
                    totalCount++;

                    // Edge pixels
                    if (y == safeY || y == safeBottom - 1 || x == safeX || x == safeRight - 1)
                    {
                        edgeR += row[x].R;
                        edgeG += row[x].G;
                        edgeB += row[x].B;
                        edgeCount++;
                    }
                }
            }
        });

        var dominant = totalCount > 0
            ? System.Drawing.Color.FromArgb(
                (int)(totalR / totalCount),
                (int)(totalG / totalCount),
                (int)(totalB / totalCount))
            : System.Drawing.Color.Gray;

        var background = edgeCount > 0
            ? System.Drawing.Color.FromArgb(
                (int)(edgeR / edgeCount),
                (int)(edgeG / edgeCount),
                (int)(edgeB / edgeCount))
            : dominant;

        return (dominant, background);
    }

    private static VisualElementType ClassifyElementType(
        System.Drawing.Rectangle region,
        System.Drawing.Color dominant,
        System.Drawing.Color background)
    {
        double aspectRatio = (double)region.Width / region.Height;
        int area = region.Width * region.Height;

        // Small square elements are likely icons
        if (area < 1500 && aspectRatio > 0.7 && aspectRatio < 1.4)
            return VisualElementType.Icon;

        // Wide, short elements are likely buttons or text fields
        if (aspectRatio > 2 && region.Height < 50)
        {
            // Colored backgrounds suggest buttons
            var brightness = (dominant.R + dominant.G + dominant.B) / 3.0;
            if (brightness < 200 && brightness > 50)
                return VisualElementType.Button;
            
            return VisualElementType.TextBox;
        }

        // Tall, narrow elements might be scrollbars
        if (aspectRatio < 0.3 && region.Height > 100)
            return VisualElementType.Scrollbar;

        // Very wide elements at top might be toolbars
        if (aspectRatio > 10 && region.Y < 100)
            return VisualElementType.Toolbar;

        // Square-ish small elements might be checkboxes
        if (area < 800 && aspectRatio > 0.8 && aspectRatio < 1.2)
            return VisualElementType.Checkbox;

        // Default to panel for larger regions
        if (area > 10000)
            return VisualElementType.Panel;

        return VisualElementType.Unknown;
    }

    private static bool IsLikelyClickable(VisualElementType type, System.Drawing.Rectangle region)
    {
        return type switch
        {
            VisualElementType.Button => true,
            VisualElementType.Checkbox => true,
            VisualElementType.RadioButton => true,
            VisualElementType.Link => true,
            VisualElementType.Tab => true,
            VisualElementType.Icon => region.Width > 16 && region.Height > 16,
            VisualElementType.Dropdown => true,
            _ => false
        };
    }

    private static int GetColorDifference(Rgba32 a, Rgba32 b)
    {
        return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
    }

    private static List<VisualElement> MergeOverlapping(List<VisualElement> elements)
    {
        if (elements.Count < 2)
            return elements;

        var result = new List<VisualElement>();
        var merged = new HashSet<int>();

        for (int i = 0; i < elements.Count; i++)
        {
            if (merged.Contains(i))
                continue;

            var current = elements[i];
            var currentRect = current.BoundingBox;

            for (int j = i + 1; j < elements.Count; j++)
            {
                if (merged.Contains(j))
                    continue;

                var other = elements[j];
                
                // Check for significant overlap
                var intersection = System.Drawing.Rectangle.Intersect(currentRect, other.BoundingBox);
                if (!intersection.IsEmpty)
                {
                    double overlapRatio = (double)intersection.Width * intersection.Height / 
                        Math.Min(currentRect.Width * currentRect.Height, 
                                other.BoundingBox.Width * other.BoundingBox.Height);

                    if (overlapRatio > 0.5)
                    {
                        // Merge by taking union
                        currentRect = System.Drawing.Rectangle.Union(currentRect, other.BoundingBox);
                        merged.Add(j);
                    }
                }
            }

            result.Add(new VisualElement
            {
                Type = current.Type,
                BoundingBox = currentRect,
                Confidence = current.Confidence,
                DominantColor = current.DominantColor,
                BackgroundColor = current.BackgroundColor,
                IsClickable = current.IsClickable
            });
        }

        return result;
    }

    private LayoutAnalysis AnalyzeLayoutCore(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        int width = image.Width;
        int height = image.Height;

        var regions = new List<LayoutRegion>();

        // Detect header (top area)
        var headerCandidate = DetectHeaderRegion(image);
        if (headerCandidate.HasValue)
        {
            regions.Add(new LayoutRegion
            {
                Name = "Header",
                Type = LayoutRegionType.Header,
                Bounds = headerCandidate.Value
            });
        }

        // Detect footer (bottom area)
        var footerCandidate = DetectFooterRegion(image);
        if (footerCandidate.HasValue)
        {
            regions.Add(new LayoutRegion
            {
                Name = "Footer",
                Type = LayoutRegionType.Footer,
                Bounds = footerCandidate.Value
            });
        }

        // Detect sidebar (left or right area)
        var sidebarCandidate = DetectSidebarRegion(image);
        if (sidebarCandidate.HasValue)
        {
            regions.Add(new LayoutRegion
            {
                Name = "Sidebar",
                Type = LayoutRegionType.Sidebar,
                Bounds = sidebarCandidate.Value
            });
        }

        // Calculate content area
        int contentTop = headerCandidate?.Bottom ?? 0;
        int contentBottom = footerCandidate?.Top ?? height;
        int contentLeft = sidebarCandidate.HasValue && sidebarCandidate.Value.X < width / 2 
            ? sidebarCandidate.Value.Right : 0;
        int contentRight = sidebarCandidate.HasValue && sidebarCandidate.Value.X >= width / 2 
            ? sidebarCandidate.Value.Left : width;

        var contentArea = new System.Drawing.Rectangle(
            contentLeft, contentTop,
            contentRight - contentLeft,
            contentBottom - contentTop);

        // Determine layout type
        var layoutType = DetermineLayoutType(regions, width, height);

        return new LayoutAnalysis
        {
            Regions = regions,
            DetectedLayout = layoutType,
            ContentArea = contentArea,
            HeaderArea = headerCandidate,
            FooterArea = footerCandidate,
            SidebarArea = sidebarCandidate,
            ImageSize = new System.Drawing.Size(width, height),
            Confidence = 0.7
        };
    }

    private System.Drawing.Rectangle? DetectHeaderRegion(Image<Rgba32> image)
    {
        // Look for horizontal region at top with distinct color
        int height = Math.Min(100, image.Height / 5);
        
        // Check if top region has different color than rest
        var topColor = GetAverageColor(image, new System.Drawing.Rectangle(0, 0, image.Width, height));
        var belowColor = GetAverageColor(image, new System.Drawing.Rectangle(0, height, image.Width, height));

        if (GetColorDifference(topColor, belowColor) > 30)
        {
            return new System.Drawing.Rectangle(0, 0, image.Width, height);
        }

        return null;
    }

    private System.Drawing.Rectangle? DetectFooterRegion(Image<Rgba32> image)
    {
        int height = Math.Min(60, image.Height / 10);
        int y = image.Height - height;

        var bottomColor = GetAverageColor(image, new System.Drawing.Rectangle(0, y, image.Width, height));
        var aboveColor = GetAverageColor(image, new System.Drawing.Rectangle(0, y - height, image.Width, height));

        if (GetColorDifference(bottomColor, aboveColor) > 30)
        {
            return new System.Drawing.Rectangle(0, y, image.Width, height);
        }

        return null;
    }

    private System.Drawing.Rectangle? DetectSidebarRegion(Image<Rgba32> image)
    {
        int width = Math.Min(250, image.Width / 4);

        // Check left side
        var leftColor = GetAverageColor(image, new System.Drawing.Rectangle(0, 0, width, image.Height));
        var rightOfLeftColor = GetAverageColor(image, new System.Drawing.Rectangle(width, 0, width, image.Height));

        if (GetColorDifference(leftColor, rightOfLeftColor) > 30)
        {
            return new System.Drawing.Rectangle(0, 0, width, image.Height);
        }

        // Check right side
        int rightX = image.Width - width;
        var rightColor = GetAverageColor(image, new System.Drawing.Rectangle(rightX, 0, width, image.Height));
        var leftOfRightColor = GetAverageColor(image, new System.Drawing.Rectangle(rightX - width, 0, width, image.Height));

        if (GetColorDifference(rightColor, leftOfRightColor) > 30)
        {
            return new System.Drawing.Rectangle(rightX, 0, width, image.Height);
        }

        return null;
    }

    private Rgba32 GetAverageColor(Image<Rgba32> image, System.Drawing.Rectangle region)
    {
        long r = 0, g = 0, b = 0;
        int count = 0;

        int safeX = Math.Max(0, region.X);
        int safeY = Math.Max(0, region.Y);
        int safeRight = Math.Min(image.Width, region.Right);
        int safeBottom = Math.Min(image.Height, region.Bottom);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = safeY; y < safeBottom; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = safeX; x < safeRight; x++)
                {
                    r += row[x].R;
                    g += row[x].G;
                    b += row[x].B;
                    count++;
                }
            }
        });

        if (count == 0)
            return new Rgba32(128, 128, 128, 255);

        return new Rgba32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
    }

    private static LayoutType DetermineLayoutType(
        List<LayoutRegion> regions, 
        int width, 
        int height)
    {
        bool hasHeader = regions.Any(r => r.Type == LayoutRegionType.Header);
        bool hasFooter = regions.Any(r => r.Type == LayoutRegionType.Footer);
        bool hasSidebar = regions.Any(r => r.Type == LayoutRegionType.Sidebar);

        if (width < 500 && height < 400)
            return LayoutType.Dialog;

        if (hasSidebar && hasHeader)
            return LayoutType.TwoColumn;

        if (hasHeader && hasFooter)
            return LayoutType.SingleColumn;

        if (height > width * 1.5)
            return LayoutType.Form;

        return LayoutType.Unknown;
    }
}

