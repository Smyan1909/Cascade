using System.Diagnostics;
using System.Drawing;
using Cascade.Vision.Capture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SharpImage = SixLabors.ImageSharp.Image;

namespace Cascade.Vision.Comparison;

/// <summary>
/// Detects visual changes between images using pixel comparison.
/// </summary>
public class ChangeDetector : IChangeDetector
{
    private byte[]? _baseline;

    /// <inheritdoc />
    public ComparisonOptions Options { get; set; } = new();

    /// <inheritdoc />
    public bool HasBaseline => _baseline != null;

    /// <inheritdoc />
    public Task<ChangeResult> CompareAsync(byte[] baseline, byte[] current, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CompareCore(baseline, current), cancellationToken);
    }

    /// <inheritdoc />
    public Task<ChangeResult> CompareAsync(CaptureResult baseline, CaptureResult current, CancellationToken cancellationToken = default)
    {
        return CompareAsync(baseline.ImageData, current.ImageData, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetBaselineAsync(byte[] imageData)
    {
        _baseline = imageData;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetBaselineAsync(CaptureResult capture)
    {
        return SetBaselineAsync(capture.ImageData);
    }

    /// <inheritdoc />
    public Task<ChangeResult> CompareWithBaselineAsync(byte[] current, CancellationToken cancellationToken = default)
    {
        if (_baseline == null)
            throw new InvalidOperationException("No baseline has been set.");

        return CompareAsync(_baseline, current, cancellationToken);
    }

    /// <inheritdoc />
    public void ClearBaseline()
    {
        _baseline = null;
    }

    /// <inheritdoc />
    public async Task<ChangeResult> WaitForChangeAsync(
        IScreenCapture capture,
        System.Drawing.Rectangle region,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        // Capture initial baseline
        var baselineCapture = await capture.CaptureRegionAsync(region, cancellationToken);
        var baseline = baselineCapture.ImageData;
        
        // Poll for changes
        var pollInterval = TimeSpan.FromMilliseconds(100);
        
        while (sw.Elapsed < timeout && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollInterval, cancellationToken);
            
            var currentCapture = await capture.CaptureRegionAsync(region, cancellationToken);
            var result = CompareCore(baseline, currentCapture.ImageData);
            
            if (result.HasChanges)
            {
                result.ProcessingTime = sw.Elapsed;
                return result;
            }
        }
        
        return new ChangeResult
        {
            HasChanges = false,
            ChangeType = ChangeType.None,
            ProcessingTime = sw.Elapsed
        };
    }

    /// <inheritdoc />
    public async Task<ChangeResult> WaitForStabilityAsync(
        IScreenCapture capture,
        System.Drawing.Rectangle region,
        TimeSpan stabilityDuration,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var stableSince = DateTime.UtcNow;
        byte[]? lastCapture = null;
        ChangeResult? lastResult = null;
        
        var pollInterval = TimeSpan.FromMilliseconds(50);
        
        while (sw.Elapsed < timeout && !cancellationToken.IsCancellationRequested)
        {
            var currentCapture = await capture.CaptureRegionAsync(region, cancellationToken);
            
            if (lastCapture != null)
            {
                var result = CompareCore(lastCapture, currentCapture.ImageData);
                
                if (result.HasChanges)
                {
                    // Reset stability timer
                    stableSince = DateTime.UtcNow;
                    lastResult = result;
                }
                else
                {
                    // Check if stable long enough
                    if (DateTime.UtcNow - stableSince >= stabilityDuration)
                    {
                        return lastResult ?? ChangeResult.NoChange;
                    }
                }
            }
            
            lastCapture = currentCapture.ImageData;
            await Task.Delay(pollInterval, cancellationToken);
        }
        
        return lastResult ?? ChangeResult.NoChange;
    }

    private ChangeResult CompareCore(byte[] baseline, byte[] current)
    {
        var sw = Stopwatch.StartNew();
        
        using var baselineImage = SharpImage.Load<Rgba32>(baseline);
        using var currentImage = SharpImage.Load<Rgba32>(current);
        
        // Ensure images are same size
        if (baselineImage.Width != currentImage.Width || baselineImage.Height != currentImage.Height)
        {
            // Resize current to match baseline
            currentImage.Mutate(ctx => ctx.Resize(baselineImage.Width, baselineImage.Height));
        }
        
        int width = baselineImage.Width;
        int height = baselineImage.Height;
        int totalPixels = width * height;
        int changedPixels = 0;
        
        // Track changed regions
        var changedPoints = new List<System.Drawing.Point>();
        
        // Create difference image if requested
        Image<Rgba32>? diffImage = Options.GenerateDifferenceImage 
            ? currentImage.Clone() 
            : null;
        
        var highlightColor = new Rgba32(
            Options.DifferenceHighlightColor.R,
            Options.DifferenceHighlightColor.G,
            Options.DifferenceHighlightColor.B,
            Options.DifferenceOverlayAlpha);
        
        // Compare pixels
        baselineImage.ProcessPixelRows(currentImage, (baselineAccessor, currentAccessor) =>
        {
            for (int y = 0; y < height; y++)
            {
                var baselineRow = baselineAccessor.GetRowSpan(y);
                var currentRow = currentAccessor.GetRowSpan(y);
                
                for (int x = 0; x < width; x++)
                {
                    // Check if point is in ignore region
                    if (IsInIgnoreRegion(x, y))
                        continue;
                    
                    if (!ArePixelsEqual(baselineRow[x], currentRow[x]))
                    {
                        changedPixels++;
                        changedPoints.Add(new System.Drawing.Point(x, y));
                    }
                }
            }
        });
        
        // Apply highlights to diff image
        if (diffImage != null && changedPoints.Count > 0)
        {
            diffImage.ProcessPixelRows(accessor =>
            {
                foreach (var point in changedPoints)
                {
                    if (point.Y < accessor.Height)
                    {
                        var row = accessor.GetRowSpan(point.Y);
                        if (point.X < row.Length)
                        {
                            // Blend highlight color
                            var original = row[point.X];
                            row[point.X] = BlendPixels(original, highlightColor);
                        }
                    }
                }
            });
        }
        
        double differencePercentage = (double)changedPixels / totalPixels;
        bool hasChanges = differencePercentage >= Options.ChangeThreshold;
        
        // Find changed regions
        var changedRegions = FindChangedRegions(changedPoints, width, height);
        
        sw.Stop();
        
        var result = new ChangeResult
        {
            HasChanges = hasChanges,
            DifferencePercentage = differencePercentage,
            ChangedPixelCount = changedPixels,
            TotalPixelCount = totalPixels,
            ChangedRegions = changedRegions,
            ChangeType = ClassifyChangeType(differencePercentage),
            ProcessingTime = sw.Elapsed
        };
        
        if (diffImage != null)
        {
            using var ms = new MemoryStream();
            diffImage.Save(ms, new PngEncoder());
            result.DifferenceImage = ms.ToArray();
            diffImage.Dispose();
        }
        
        return result;
    }

    private bool ArePixelsEqual(Rgba32 a, Rgba32 b)
    {
        int tolerance = Options.ColorTolerance;
        
        if (tolerance == 0)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B;
        }
        
        return Math.Abs(a.R - b.R) <= tolerance &&
               Math.Abs(a.G - b.G) <= tolerance &&
               Math.Abs(a.B - b.B) <= tolerance;
    }

    private bool IsInIgnoreRegion(int x, int y)
    {
        if (Options.IgnoreRegions == null)
            return false;
        
        foreach (var region in Options.IgnoreRegions)
        {
            if (x >= region.X && x < region.Right &&
                y >= region.Y && y < region.Bottom)
            {
                return true;
            }
        }
        
        return false;
    }

    private static Rgba32 BlendPixels(Rgba32 background, Rgba32 foreground)
    {
        float alpha = foreground.A / 255f;
        float invAlpha = 1 - alpha;
        
        return new Rgba32(
            (byte)(foreground.R * alpha + background.R * invAlpha),
            (byte)(foreground.G * alpha + background.G * invAlpha),
            (byte)(foreground.B * alpha + background.B * invAlpha),
            255);
    }

    private List<System.Drawing.Rectangle> FindChangedRegions(
        List<System.Drawing.Point> changedPoints, 
        int width, 
        int height)
    {
        if (changedPoints.Count == 0)
            return new List<System.Drawing.Rectangle>();
        
        // Simple approach: find bounding boxes of connected regions
        var regions = new List<System.Drawing.Rectangle>();
        var visited = new HashSet<System.Drawing.Point>();
        
        foreach (var point in changedPoints)
        {
            if (visited.Contains(point))
                continue;
            
            // Find connected region using flood fill
            var region = FloodFill(point, changedPoints, visited, width, height);
            
            if (region.Width >= Options.MinChangedRegionSize && 
                region.Height >= Options.MinChangedRegionSize)
            {
                regions.Add(region);
            }
        }
        
        return regions;
    }

    private static System.Drawing.Rectangle FloodFill(
        System.Drawing.Point start,
        List<System.Drawing.Point> changedPoints,
        HashSet<System.Drawing.Point> visited,
        int width,
        int height)
    {
        var changedSet = new HashSet<System.Drawing.Point>(changedPoints);
        var queue = new Queue<System.Drawing.Point>();
        queue.Enqueue(start);
        
        int minX = start.X, maxX = start.X;
        int minY = start.Y, maxY = start.Y;
        
        int tolerance = 5; // Allow small gaps
        
        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            
            if (visited.Contains(point))
                continue;
            
            visited.Add(point);
            
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
            
            // Check neighbors
            for (int dy = -tolerance; dy <= tolerance; dy++)
            {
                for (int dx = -tolerance; dx <= tolerance; dx++)
                {
                    var neighbor = new System.Drawing.Point(point.X + dx, point.Y + dy);
                    
                    if (neighbor.X >= 0 && neighbor.X < width &&
                        neighbor.Y >= 0 && neighbor.Y < height &&
                        !visited.Contains(neighbor) &&
                        changedSet.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
        
        return new System.Drawing.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static ChangeType ClassifyChangeType(double differencePercentage)
    {
        return differencePercentage switch
        {
            0 => ChangeType.None,
            < 0.05 => ChangeType.Minor,
            < 0.20 => ChangeType.Moderate,
            < 0.50 => ChangeType.Major,
            _ => ChangeType.Complete
        };
    }
}

