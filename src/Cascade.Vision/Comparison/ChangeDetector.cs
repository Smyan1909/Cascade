using System.Linq;
using Cascade.Vision.Capture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Rectangle = System.Drawing.Rectangle;

namespace Cascade.Vision.Comparison;

public sealed class ChangeDetector : IChangeDetector
{
    private readonly ComparisonOptions _options;
    private byte[]? _baseline;

    public ChangeDetector(ComparisonOptions? options = null)
    {
        _options = options ?? new ComparisonOptions();
    }

    public Task SetBaselineAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        _baseline = imageData.ToArray();
        return Task.CompletedTask;
    }

    public Task<ChangeResult> CompareWithBaselineAsync(byte[] current, CancellationToken cancellationToken = default)
    {
        if (_baseline is null)
        {
            throw new InvalidOperationException("Baseline has not been set.");
        }

        return CompareAsync(_baseline, current, cancellationToken);
    }

    public Task<ChangeResult> CompareAsync(byte[] baseline, byte[] current, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var baselineImage = LoadImage(baseline);
        using var currentImage = LoadImage(current);

        if (baselineImage.Width != currentImage.Width || baselineImage.Height != currentImage.Height)
        {
            currentImage.Mutate(ctx => ctx.Resize(baselineImage.Width, baselineImage.Height));
        }

        var changedPixels = 0;
        var totalPixels = baselineImage.Width * baselineImage.Height;
        var changedRegions = new List<Rectangle>();

        var diffImage = _options.GenerateDifferenceImage
            ? new Image<Rgba32>(baselineImage.Width, baselineImage.Height)
            : null;

        for (var y = 0; y < baselineImage.Height; y++)
        {
            var baseRow = baselineImage.DangerousGetPixelRowMemory(y).Span;
            var currentRow = currentImage.DangerousGetPixelRowMemory(y).Span;
            var diffRow = diffImage is null ? Span<Rgba32>.Empty : diffImage.DangerousGetPixelRowMemory(y).Span;

            for (var x = 0; x < baseRow.Length; x++)
            {
                if (IsIgnored(x, y))
                {
                    continue;
                }

                if (HasChanged(baseRow[x], currentRow[x]))
                {
                    changedPixels++;
                    changedRegions.Add(new Rectangle(x, y, 1, 1));
                    if (!diffRow.IsEmpty)
                    {
                        diffRow[x] = new Rgba32(_options.DifferenceHighlightColor.R, _options.DifferenceHighlightColor.G, _options.DifferenceHighlightColor.B);
                    }
                }
                else if (!diffRow.IsEmpty)
                {
                    diffRow[x] = new Rgba32(0, 0, 0, 0);
                }
            }
        }

        var differencePercentage = changedPixels / (double)totalPixels;
        var result = new ChangeResult
        {
            HasChanges = differencePercentage >= _options.ChangeThreshold,
            DifferencePercentage = differencePercentage,
            ChangedRegions = MergeRegions(changedRegions),
            ChangeType = ClassifyChange(differencePercentage),
            DifferenceImage = diffImage is null ? null : Encode(diffImage),
            HasTextChanges = false
        };

        diffImage?.Dispose();

        return Task.FromResult(result);
    }

    public Task<ChangeResult> CompareAsync(CaptureResult baseline, CaptureResult current, CancellationToken cancellationToken = default)
        => CompareAsync(baseline.ImageData, current.ImageData, cancellationToken);

    public async Task<ChangeResult> WaitForChangeAsync(IScreenCapture capture, Rectangle region, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        var baseline = await capture.CaptureRegionAsync(region, cancellationToken);
        while (DateTime.UtcNow < deadline)
        {
            var current = await capture.CaptureRegionAsync(region, cancellationToken);
            var result = await CompareAsync(baseline, current, cancellationToken);
            if (result.HasChanges)
            {
                return result;
            }

            await Task.Delay(200, cancellationToken);
        }

        return new ChangeResult { HasChanges = false };
    }

    public async Task<ChangeResult> WaitForStabilityAsync(IScreenCapture capture, Rectangle region, TimeSpan stabilityDuration, CancellationToken cancellationToken = default)
    {
        var stableUntil = DateTime.UtcNow + stabilityDuration;
        var previous = await capture.CaptureRegionAsync(region, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            var current = await capture.CaptureRegionAsync(region, cancellationToken);
            var result = await CompareAsync(previous, current, cancellationToken);
            if (result.HasChanges)
            {
                stableUntil = DateTime.UtcNow + stabilityDuration;
            }
            else if (DateTime.UtcNow >= stableUntil)
            {
                return result;
            }

            previous = current;
            await Task.Delay(200, cancellationToken);
        }

        return new ChangeResult { HasChanges = false };
    }

    private bool IsIgnored(int x, int y)
    {
        if (_options.IgnoreRegions is null)
        {
            return false;
        }

        return _options.IgnoreRegions.Any(region => region.Contains(x, y));
    }

    private bool HasChanged(Rgba32 baseline, Rgba32 current)
    {
        var delta = Math.Abs(baseline.R - current.R) +
                    Math.Abs(baseline.G - current.G) +
                    Math.Abs(baseline.B - current.B);

        if (_options.IgnoreMinorColorDifferences)
        {
            return delta > _options.ColorTolerance;
        }

        return delta > 0;
    }

    private static byte[] Encode(Image<Rgba32> diffImage)
    {
        using var stream = new MemoryStream();
        diffImage.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static ChangeType ClassifyChange(double percent) => percent switch
    {
        < 0.05 => ChangeType.Minor,
        < 0.2 => ChangeType.Moderate,
        < 0.5 => ChangeType.Major,
        _ => ChangeType.Complete
    };

    private static IReadOnlyList<Rectangle> MergeRegions(IEnumerable<Rectangle> regions)
    {
        var merged = new List<Rectangle>();
        foreach (var region in regions)
        {
            var index = merged.FindIndex(r => r.IntersectsWith(region));
            if (index >= 0)
            {
                merged[index] = Rectangle.Union(merged[index], region);
            }
            else
            {
                merged.Add(region);
            }
        }

        return merged;
    }

    private static Image<Rgba32> LoadImage(byte[] data)
        => Image.Load<Rgba32>(data);
}


