namespace Cascade.Grpc.Server.Startup;

/// <summary>
/// Configuration options for the gRPC server host.
/// </summary>
public sealed class GrpcServerOptions
{
    public const string SectionName = "GrpcServer";

    public int Port { get; set; } = 50051;
    public bool EnableReflection { get; set; } = true;
    public bool EnableDetailedErrors { get; set; } = false;
    public int MaxReceiveMessageSize { get; set; } = 16 * 1024 * 1024;
    public int MaxSendMessageSize { get; set; } = 16 * 1024 * 1024;
    public bool RequireAuthentication { get; set; } = false;
    public string? CertificatePath { get; set; }
    public string? CertificateKeyPath { get; set; }
    public IList<string> ApiKeys { get; set; } = new List<string>();
}

