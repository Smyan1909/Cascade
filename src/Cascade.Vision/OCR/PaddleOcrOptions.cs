namespace Cascade.Vision.OCR;

public class PaddleOcrOptions
{
    public string ServiceEndpoint { get; set; } = "http://localhost:50052";
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public PaddleOcrModel DefaultModel { get; set; } = PaddleOcrModel.PPOCRv4;
    public string DefaultLanguage { get; set; } = "en";
    public bool UseAngleClassifier { get; set; } = true;
    public double FallbackConfidenceThreshold { get; set; } = 0.7;
    public bool EnableAutoFallback { get; set; } = true;
    public bool UseGpu { get; set; }
}

public enum PaddleOcrModel
{
    ViTSTR,
    SVTR,
    PPOCRv4
}


