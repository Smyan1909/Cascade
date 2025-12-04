namespace Cascade.Vision.Processing;

public sealed class PreprocessingPipeline
{
    private readonly List<Func<byte[], byte[]>> _steps = new();

    public PreprocessingPipeline AddGrayscale()
    {
        _steps.Add(data => new ImageProcessor().ToGrayscale(data));
        return this;
    }

    public PreprocessingPipeline AddResize(int scale)
    {
        _steps.Add(data =>
        {
            using var image = SixLabors.ImageSharp.Image.Load(data);
            var width = Math.Max(1, image.Width * scale);
            var height = Math.Max(1, image.Height * scale);
            return new ImageProcessor().Resize(data, width, height);
        });
        return this;
    }

    public PreprocessingPipeline AddDeskew()
    {
        _steps.Add(data => new ImageProcessor().Rotate(data, 0)); // placeholder until deskew implemented
        return this;
    }

    public PreprocessingPipeline AddBinarize(int threshold)
    {
        _steps.Add(data => new ImageProcessor().Binarize(data, threshold));
        return this;
    }

    public PreprocessingPipeline AddDenoise()
    {
        _steps.Add(data => new ImageProcessor().Denoise(data));
        return this;
    }

    public PreprocessingPipeline AddSharpen()
    {
        _steps.Add(data => new ImageProcessor().Sharpen(data));
        return this;
    }

    public PreprocessingPipeline AddCustom(Func<byte[], byte[]> processor)
    {
        _steps.Add(processor);
        return this;
    }

    public byte[] Process(byte[] imageData)
    {
        return _steps.Aggregate(imageData, (current, step) => step(current));
    }

    public static PreprocessingPipeline ForScreenText => new PreprocessingPipeline()
        .AddGrayscale()
        .AddSharpen()
        .AddBinarize(140);

    public static PreprocessingPipeline ForDocument => new PreprocessingPipeline()
        .AddGrayscale()
        .AddDenoise()
        .AddBinarize(120);

    public static PreprocessingPipeline ForLowContrast => new PreprocessingPipeline()
        .AddGrayscale()
        .AddSharpen()
        .AddCustom(data => new ImageProcessor().AdjustContrast(data, 1.4f));
}


