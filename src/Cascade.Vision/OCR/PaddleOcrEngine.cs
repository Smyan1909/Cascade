using System.Diagnostics;
using System.Drawing;
using Cascade.Vision.Capture;
using Grpc.Core;
using Grpc.Net.Client;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SharpImage = SixLabors.ImageSharp.Image;
using SharpRectangle = SixLabors.ImageSharp.Rectangle;

namespace Cascade.Vision.OCR;

/// <summary>
/// OCR engine using PaddleOCR via gRPC service.
/// This is a fallback engine with Vision Transformer-based models for difficult text recognition.
/// </summary>
public class PaddleOcrEngine : IOcrEngine, IDisposable
{
    private GrpcChannel? _channel;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Gets or sets the gRPC service endpoint.
    /// </summary>
    public string ServiceEndpoint { get; set; } = "http://localhost:50052";

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to enable retry on failure.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retries.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the PaddleOCR model to use.
    /// </summary>
    public PaddleOcrModel Model { get; set; } = PaddleOcrModel.PPOCRv4;

    /// <summary>
    /// Gets or sets whether to use GPU for inference.
    /// </summary>
    public bool UseGpu { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to use angle classification for rotated text.
    /// </summary>
    public bool UseAngleClassifier { get; set; } = true;

    /// <inheritdoc />
    public string EngineName => "PaddleOCR";

    /// <inheritdoc />
    public OcrOptions Options { get; set; } = new();

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedLanguages => new[]
    {
        "en", "ch", "chinese_cht", "japan", "korean",
        "french", "german", "arabic", "cyrillic", "latin",
        "devanagari", "tamil", "telugu", "kannada"
    };

    /// <inheritdoc />
    public bool IsAvailable => CheckServiceAvailability();

    /// <inheritdoc />
    public int Priority => 3; // Lowest priority (slowest, but most powerful)

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            EnsureChannel();
            if (_channel == null)
                return OcrResult.Empty(EngineName);

            var response = await CallRecognizeWithRetryAsync(imageData, cancellationToken);
            sw.Stop();

            if (response == null || !response.Success)
            {
                return new OcrResult
                {
                    FullText = string.Empty,
                    Confidence = 0,
                    EngineUsed = EngineName,
                    ProcessingTime = sw.Elapsed,
                    Lines = Array.Empty<OcrLine>()
                };
            }

            return ConvertResponse(response, sw.Elapsed);
        }
        catch (Exception)
        {
            sw.Stop();
            return OcrResult.Empty(EngineName);
        }
    }

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await RecognizeAsync(imageData, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OcrResult> RecognizeAsync(CaptureResult capture, CancellationToken cancellationToken = default)
    {
        return RecognizeAsync(capture.ImageData, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeRegionAsync(byte[] imageData, System.Drawing.Rectangle region, CancellationToken cancellationToken = default)
    {
        // For region recognition, we crop the image first and then send it
        // The bounding boxes in the result will need to be adjusted
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.Crop(new SharpRectangle(
            region.X, region.Y, region.Width, region.Height)));

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms, cancellationToken);
        var croppedData = ms.ToArray();

        var result = await RecognizeAsync(croppedData, cancellationToken);

        // Adjust coordinates back to original image space
        return AdjustCoordinates(result, region.X, region.Y);
    }

    private void EnsureChannel()
    {
        if (_channel != null)
            return;

        lock (_lock)
        {
            if (_channel != null)
                return;

            _channel = GrpcChannel.ForAddress(ServiceEndpoint, new GrpcChannelOptions
            {
                MaxReceiveMessageSize = 100 * 1024 * 1024, // 100MB for large images
                MaxSendMessageSize = 100 * 1024 * 1024
            });
        }
    }

    private async Task<PaddleOcrResponse?> CallRecognizeWithRetryAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        int attempts = 0;
        Exception? lastException = null;

        while (attempts < (EnableRetry ? MaxRetries : 1))
        {
            try
            {
                return await CallRecognizeAsync(imageData, cancellationToken);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable && EnableRetry)
            {
                lastException = ex;
                attempts++;
                if (attempts < MaxRetries)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        return null;
    }

    private async Task<PaddleOcrResponse> CallRecognizeAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        // Create the gRPC call
        // Note: In a real implementation, this would use generated proto classes
        // For now, we use a manual implementation that can be replaced with generated code
        
        var invoker = _channel!.CreateCallInvoker();
        var method = new Method<PaddleOcrRequest, PaddleOcrResponse>(
            MethodType.Unary,
            "cascade.vision.PaddleOcrService",
            "Recognize",
            Marshallers.Create(
                (PaddleOcrRequest request) => SerializeRequest(request),
                (byte[] data) => DeserializeRequest(data)),
            Marshallers.Create(
                (PaddleOcrResponse response) => SerializeResponse(response),
                (byte[] data) => DeserializeResponse(data)));

        var request = new PaddleOcrRequest
        {
            ImageData = imageData,
            Language = MapLanguage(Options.Language),
            Model = Model,
            UseAngleClassifier = UseAngleClassifier
        };

        var options = new CallOptions(
            deadline: DateTime.UtcNow.Add(Timeout),
            cancellationToken: cancellationToken);

        return await invoker.AsyncUnaryCall(method, null, options, request);
    }

    private static byte[] SerializeRequest(PaddleOcrRequest request)
    {
        // Binary serialization using big-endian for cross-platform compatibility
        using var ms = new MemoryStream();
        
        // Write image data length (4 bytes big-endian) and data
        var imageLen = BitConverter.GetBytes(request.ImageData.Length);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(imageLen);
        ms.Write(imageLen, 0, 4);
        ms.Write(request.ImageData, 0, request.ImageData.Length);
        
        // Write language string (2 bytes big-endian length + UTF-8 data)
        var langBytes = System.Text.Encoding.UTF8.GetBytes(request.Language);
        var langLen = BitConverter.GetBytes((ushort)langBytes.Length);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(langLen);
        ms.Write(langLen, 0, 2);
        ms.Write(langBytes, 0, langBytes.Length);
        
        // Write model type (4 bytes big-endian)
        var modelType = BitConverter.GetBytes((int)request.Model);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(modelType);
        ms.Write(modelType, 0, 4);
        
        // Write flags (1 byte)
        ms.WriteByte(request.UseAngleClassifier ? (byte)1 : (byte)0);
        
        return ms.ToArray();
    }

    private static byte[] SerializeResponse(PaddleOcrResponse response)
    {
        // This is only needed for the marshaller, but won't be called in client mode
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(response.Success);
        return ms.ToArray();
    }

    private static PaddleOcrRequest DeserializeRequest(byte[] data)
    {
        // This is only needed for the marshaller, but won't be called in client mode
        return new PaddleOcrRequest();
    }

    private static PaddleOcrResponse DeserializeResponse(byte[] data)
    {
        // Binary deserialization using big-endian for cross-platform compatibility
        int offset = 0;
        
        var response = new PaddleOcrResponse
        {
            Success = data[offset++] != 0
        };

        if (!response.Success)
        {
            response.ErrorMessage = ReadString(data, ref offset);
            return response;
        }

        response.FullText = ReadString(data, ref offset);
        response.Confidence = ReadDouble(data, ref offset);
        response.ProcessingTimeMs = ReadInt32(data, ref offset);
        response.ModelUsed = ReadString(data, ref offset);

        // Read blocks
        int blockCount = ReadInt32(data, ref offset);
        var blocks = new List<PaddleOcrTextBlock>();
        
        for (int i = 0; i < blockCount; i++)
        {
            var block = new PaddleOcrTextBlock
            {
                Text = ReadString(data, ref offset),
                Confidence = ReadDouble(data, ref offset),
                BoundingBox = new System.Drawing.Rectangle(
                    ReadInt32(data, ref offset),
                    ReadInt32(data, ref offset),
                    ReadInt32(data, ref offset),
                    ReadInt32(data, ref offset))
            };
            
            // Read words
            int wordCount = ReadInt32(data, ref offset);
            var words = new List<PaddleOcrWord>();
            
            for (int j = 0; j < wordCount; j++)
            {
                words.Add(new PaddleOcrWord
                {
                    Text = ReadString(data, ref offset),
                    Confidence = ReadDouble(data, ref offset),
                    BoundingBox = new System.Drawing.Rectangle(
                        ReadInt32(data, ref offset),
                        ReadInt32(data, ref offset),
                        ReadInt32(data, ref offset),
                        ReadInt32(data, ref offset))
                });
            }
            
            block.Words = words;
            blocks.Add(block);
        }
        
        response.Blocks = blocks;
        return response;
    }

    private static int ReadInt32(byte[] data, ref int offset)
    {
        var bytes = new byte[4];
        Array.Copy(data, offset, bytes, 0, 4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        offset += 4;
        return BitConverter.ToInt32(bytes, 0);
    }

    private static double ReadDouble(byte[] data, ref int offset)
    {
        var bytes = new byte[8];
        Array.Copy(data, offset, bytes, 0, 8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        offset += 8;
        return BitConverter.ToDouble(bytes, 0);
    }

    private static string ReadString(byte[] data, ref int offset)
    {
        var lenBytes = new byte[2];
        Array.Copy(data, offset, lenBytes, 0, 2);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lenBytes);
        int len = BitConverter.ToUInt16(lenBytes, 0);
        offset += 2;
        
        var str = System.Text.Encoding.UTF8.GetString(data, offset, len);
        offset += len;
        return str;
    }

    private OcrResult ConvertResponse(PaddleOcrResponse response, TimeSpan processingTime)
    {
        var lines = response.Blocks.Select(block => new OcrLine
        {
            Text = block.Text,
            BoundingBox = block.BoundingBox,
            Confidence = block.Confidence,
            Words = block.Words.Select(w => new OcrWord
            {
                Text = w.Text,
                BoundingBox = w.BoundingBox,
                Confidence = w.Confidence
            }).ToList()
        }).ToList();

        return new OcrResult
        {
            FullText = response.FullText,
            Confidence = response.Confidence,
            Lines = lines,
            ProcessingTime = processingTime,
            EngineUsed = $"{EngineName} ({response.ModelUsed})"
        };
    }

    private static OcrResult AdjustCoordinates(OcrResult result, int offsetX, int offsetY)
    {
        var adjustedLines = result.Lines.Select(line => new OcrLine
        {
            Text = line.Text,
            BoundingBox = new System.Drawing.Rectangle(
                line.BoundingBox.X + offsetX,
                line.BoundingBox.Y + offsetY,
                line.BoundingBox.Width,
                line.BoundingBox.Height),
            Confidence = line.Confidence,
            Words = line.Words.Select(w => new OcrWord
            {
                Text = w.Text,
                BoundingBox = new System.Drawing.Rectangle(
                    w.BoundingBox.X + offsetX,
                    w.BoundingBox.Y + offsetY,
                    w.BoundingBox.Width,
                    w.BoundingBox.Height),
                Confidence = w.Confidence
            }).ToList()
        }).ToList();

        return new OcrResult
        {
            FullText = result.FullText,
            Confidence = result.Confidence,
            Lines = adjustedLines,
            ProcessingTime = result.ProcessingTime,
            EngineUsed = result.EngineUsed
        };
    }

    private static string MapLanguage(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "en-us" or "en-gb" or "en" => "en",
            "zh-cn" or "zh" => "ch",
            "zh-tw" => "chinese_cht",
            "ja-jp" or "ja" => "japan",
            "ko-kr" or "ko" => "korean",
            "fr-fr" or "fr" => "french",
            "de-de" or "de" => "german",
            "ar-sa" or "ar" => "arabic",
            "ru-ru" or "ru" => "cyrillic",
            _ => "en"
        };
    }

    private bool CheckServiceAvailability()
    {
        try
        {
            EnsureChannel();
            // In a full implementation, we would call GetStatus RPC
            // For now, just check if the channel can be created
            return _channel != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the PaddleOCR service is ready and returns its status.
    /// </summary>
    public async Task<PaddleOcrStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureChannel();
            if (_channel == null)
                return null;

            // This would call the GetStatus RPC
            // For now, return a placeholder
            return new PaddleOcrStatus
            {
                IsReady = true,
                ModelLoaded = Model.ToString(),
                GpuAvailable = UseGpu
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Disposes of the gRPC channel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _channel?.Dispose();
            _channel = null;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// PaddleOCR model types.
/// </summary>
public enum PaddleOcrModel
{
    /// <summary>
    /// Vision Transformer for Scene Text Recognition - balanced.
    /// </summary>
    ViTSTR,

    /// <summary>
    /// Single Visual Model - faster inference.
    /// </summary>
    SVTR,

    /// <summary>
    /// Latest PP-OCR model - best accuracy (default).
    /// </summary>
    PPOCRv4
}

#region Internal Request/Response Types

internal class PaddleOcrRequest
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string Language { get; set; } = "en";
    public PaddleOcrModel Model { get; set; } = PaddleOcrModel.PPOCRv4;
    public bool UseAngleClassifier { get; set; } = true;
}

internal class PaddleOcrResponse
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int ProcessingTimeMs { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public List<PaddleOcrTextBlock> Blocks { get; set; } = new();
}

internal class PaddleOcrTextBlock
{
    public string Text { get; set; } = string.Empty;
    public System.Drawing.Rectangle BoundingBox { get; set; }
    public double Confidence { get; set; }
    public List<PaddleOcrWord> Words { get; set; } = new();
}

internal class PaddleOcrWord
{
    public string Text { get; set; } = string.Empty;
    public System.Drawing.Rectangle BoundingBox { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Status information from the PaddleOCR service.
/// </summary>
public class PaddleOcrStatus
{
    /// <summary>
    /// Whether the service is ready to process requests.
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// The currently loaded model name.
    /// </summary>
    public string ModelLoaded { get; set; } = string.Empty;

    /// <summary>
    /// Whether GPU is available for inference.
    /// </summary>
    public bool GpuAvailable { get; set; }

    /// <summary>
    /// GPU memory used in MB.
    /// </summary>
    public int GpuMemoryUsedMb { get; set; }

    /// <summary>
    /// Supported languages.
    /// </summary>
    public List<string> SupportedLanguages { get; set; } = new();
}

#endregion

