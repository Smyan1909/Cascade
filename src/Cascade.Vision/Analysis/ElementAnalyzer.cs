using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cascade.Vision.Analysis;

public sealed class ElementAnalyzer : IElementAnalyzer
{
    private readonly ContrastAnalyzer _contrastAnalyzer;

    public ElementAnalyzer(ContrastAnalyzer? contrastAnalyzer = null)
    {
        _contrastAnalyzer = contrastAnalyzer ?? new ContrastAnalyzer();
    }

    public Task<IReadOnlyList<VisualElement>> DetectElementsAsync(Capture.CaptureResult capture, CancellationToken cancellationToken = default)
        => DetectElementsAsync(capture.ImageData, cancellationToken);

    public Task<IReadOnlyList<VisualElement>> DetectButtonsAsync(byte[] imageData, CancellationToken cancellationToken = default)
        => FilterAsync(imageData, VisualElementType.Button, cancellationToken);

    public Task<IReadOnlyList<VisualElement>> DetectTextFieldsAsync(byte[] imageData, CancellationToken cancellationToken = default)
        => FilterAsync(imageData, VisualElementType.TextBox, cancellationToken);

    public Task<IReadOnlyList<VisualElement>> DetectIconsAsync(byte[] imageData, CancellationToken cancellationToken = default)
        => FilterAsync(imageData, VisualElementType.Icon, cancellationToken);

    public async Task<IReadOnlyList<VisualElement>> DetectElementsAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = await LoadAsync(imageData, cancellationToken);
        var elements = new List<VisualElement>();
        var cellWidth = Math.Max(40, image.Width / 6);
        var cellHeight = Math.Max(40, image.Height / 6);

        var globalBrightness = _contrastAnalyzer.GetBrightness(imageData);

        for (var y = 0; y < image.Height; y += cellHeight)
        {
            for (var x = 0; x < image.Width; x += cellWidth)
            {
                var bounds = new Rectangle(x, y, Math.Min(cellWidth, image.Width - x), Math.Min(cellHeight, image.Height - y));
                var slice = image.Clone(context => context.Crop(bounds));

                var dominantColor = GetAverageColor(slice);
                var brightness = (0.299 * dominantColor.R + 0.587 * dominantColor.G + 0.114 * dominantColor.B) / 255d;
                var contrast = Math.Abs(brightness - globalBrightness);

                if (contrast < 0.05)
                {
                    continue;
                }

                var type = Classify(bounds);
                elements.Add(new VisualElement
                {
                    Type = type,
                    BoundingBox = bounds,
                    Confidence = contrast,
                    DominantColor = Color.FromArgb(dominantColor.A, dominantColor.R, dominantColor.G, dominantColor.B),
                    BackgroundColor = Color.FromArgb(255, (byte)(255 - dominantColor.R), (byte)(255 - dominantColor.G), (byte)(255 - dominantColor.B)),
                    IsClickable = type is VisualElementType.Button or VisualElementType.Icon or VisualElementType.Link
                });
            }
        }

        return elements;
    }

    public async Task<LayoutAnalysis> AnalyzeLayoutAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        var elements = await DetectElementsAsync(imageData, cancellationToken);
        using var image = await LoadAsync(imageData, cancellationToken);

        var layoutType = DetermineLayoutType(elements, image.Width, image.Height);
        var regions = BuildRegions(elements, image.Width, image.Height);

        return new LayoutAnalysis
        {
            DetectedLayout = layoutType,
            Regions = regions,
            ContentArea = new Rectangle(0, 0, image.Width, image.Height),
            HeaderArea = regions.FirstOrDefault(r => r.Type == LayoutRegionType.Header)?.Bounds,
            FooterArea = regions.FirstOrDefault(r => r.Type == LayoutRegionType.Footer)?.Bounds,
            SidebarArea = regions.FirstOrDefault(r => r.Type == LayoutRegionType.Sidebar)?.Bounds,
            NavigationArea = regions.FirstOrDefault(r => r.Type == LayoutRegionType.Navigation)?.Bounds
        };
    }

    private async Task<IReadOnlyList<VisualElement>> FilterAsync(byte[] imageData, VisualElementType type, CancellationToken cancellationToken)
    {
        var elements = await DetectElementsAsync(imageData, cancellationToken);
        return elements.Where(element => element.Type == type).ToList();
    }

    private static VisualElementType Classify(Rectangle bounds)
    {
        if (bounds.Width > bounds.Height * 3)
        {
            return VisualElementType.Toolbar;
        }

        if (bounds.Width > bounds.Height * 1.5)
        {
            return VisualElementType.Button;
        }

        if (bounds.Height > bounds.Width * 1.5)
        {
            return VisualElementType.TextBox;
        }

        return VisualElementType.Panel;
    }

    private static LayoutType DetermineLayoutType(IEnumerable<VisualElement> elements, int width, int height)
    {
        var hasSidebar = elements.Any(e => e.BoundingBox.Left < width * 0.2 || e.BoundingBox.Right > width * 0.8);
        var hasToolbar = elements.Any(e => e.Type == VisualElementType.Toolbar);

        if (hasSidebar && hasToolbar)
        {
            return LayoutType.Dashboard;
        }

        if (hasSidebar)
        {
            return LayoutType.TwoColumn;
        }

        if (elements.Count() > 20)
        {
            return LayoutType.Grid;
        }

        return LayoutType.SingleColumn;
    }

    private static IReadOnlyList<LayoutRegion> BuildRegions(IEnumerable<VisualElement> elements, int width, int height)
    {
        var regions = new List<LayoutRegion>();
        var header = elements.Where(e => e.BoundingBox.Top < height * 0.1).ToList();
        if (header.Any())
        {
            regions.Add(new LayoutRegion
            {
                Name = "Header",
                Type = LayoutRegionType.Header,
                Bounds = new Rectangle(0, 0, width, (int)(height * 0.1)),
                Elements = header
            });
        }

        var footer = elements.Where(e => e.BoundingBox.Bottom > height * 0.9).ToList();
        if (footer.Any())
        {
            regions.Add(new LayoutRegion
            {
                Name = "Footer",
                Type = LayoutRegionType.Footer,
                Bounds = new Rectangle(0, (int)(height * 0.9), width, (int)(height * 0.1)),
                Elements = footer
            });
        }

        var sidebar = elements.Where(e => e.BoundingBox.Left < width * 0.15).ToList();
        if (sidebar.Any())
        {
            regions.Add(new LayoutRegion
            {
                Name = "Sidebar",
                Type = LayoutRegionType.Sidebar,
                Bounds = new Rectangle(0, 0, (int)(width * 0.2), height),
                Elements = sidebar
            });
        }

        if (!regions.Any(r => r.Type == LayoutRegionType.Sidebar))
        {
            regions.Add(new LayoutRegion
            {
                Name = "Content",
                Type = LayoutRegionType.Content,
                Bounds = new Rectangle(0, 0, width, height),
                Elements = elements.ToList()
            });
        }

        return regions;
    }

    private static Color GetAverageColor(Image<Rgba32> image)
    {
        double r = 0, g = 0, b = 0;
        var total = image.Width * image.Height;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    r += row[x].R;
                    g += row[x].G;
                    b += row[x].B;
                }
            }
        });

        return Color.FromArgb(
            255,
            (int)(r / total),
            (int)(g / total),
            (int)(b / total));
    }

    private static async Task<Image<Rgba32>> LoadAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(imageData);
        return await Image.LoadAsync<Rgba32>(stream, cancellationToken);
    }
}


