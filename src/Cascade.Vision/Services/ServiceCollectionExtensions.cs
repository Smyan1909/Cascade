using Cascade.Vision.Analysis;
using Cascade.Vision.Capture;
using Cascade.Vision.Comparison;
using Cascade.Vision.OCR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cascade.Vision.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCascadeVision(this IServiceCollection services, Action<VisionOptions>? configure = null)
    {
        services.AddOptions<VisionOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<ISessionFrameProvider, DesktopSessionFrameProvider>();
        services.AddSingleton<IElementAnalyzer, ElementAnalyzer>();
        services.AddSingleton<ContrastAnalyzer>();
        services.AddSingleton<IChangeDetector>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<VisionOptions>>().Value.DefaultComparisonOptions;
            return new ChangeDetector(options);
        });
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<VisionOptions>>().Value.DefaultOcrOptions;
            return new WindowsOcrEngine(options);
        });
        services.AddSingleton(provider =>
        {
            var visionOptions = provider.GetRequiredService<IOptions<VisionOptions>>().Value;
            var engine = new TesseractOcrEngine(visionOptions.DefaultOcrOptions);
            if (!string.IsNullOrWhiteSpace(visionOptions.TesseractDataPath))
            {
                engine.TessDataPath = visionOptions.TesseractDataPath;
            }

            return engine;
        });
        services.AddSingleton(provider =>
        {
            var visionOptions = provider.GetRequiredService<IOptions<VisionOptions>>().Value;
            var logger = provider.GetService<ILogger<PaddleOcrEngine>>();
            return new PaddleOcrEngine(visionOptions.PaddleOcr, visionOptions.DefaultOcrOptions, logger);
        });
        services.AddSingleton<IOcrEngine>(provider =>
        {
            var windows = provider.GetRequiredService<WindowsOcrEngine>();
            var tesseract = provider.GetRequiredService<TesseractOcrEngine>();
            var paddle = provider.GetRequiredService<PaddleOcrEngine>();
            var options = provider.GetRequiredService<IOptions<VisionOptions>>().Value.DefaultOcrOptions;
            return new CompositeOcrEngine(windows, tesseract, paddle, options);
        });
        services.AddSingleton<VisionService>();

        return services;
    }
}


