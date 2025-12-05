using Cascade.CodeGen.Extensions;
using Cascade.Database.Extensions;
using Cascade.Grpc.Server.Extensions;
using Cascade.Grpc.Server.Interceptors;
using Cascade.Grpc.Server.Services;
using Cascade.Grpc.Server.Startup;
using Cascade.UIAutomation.Services;
using Cascade.Vision.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var serverOptions = builder.Configuration
    .GetSection(GrpcServerOptions.SectionName)
    .Get<GrpcServerOptions>() ?? new GrpcServerOptions();

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

builder.Services
    .AddOptions<GrpcServerOptions>()
    .Bind(builder.Configuration.GetSection(GrpcServerOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.Port > 0, "gRPC port must be greater than zero.");

builder.Services.AddCascadeDatabase(builder.Configuration);
builder.Services.AddCascadeCodeGen();
builder.Services.AddCascadeVision();
builder.Services.AddCascadeUIAutomation();
builder.Services.AddGrpcServerInfrastructure();

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = serverOptions.EnableDetailedErrors;
    options.MaxReceiveMessageSize = serverOptions.MaxReceiveMessageSize;
    options.MaxSendMessageSize = serverOptions.MaxSendMessageSize;
    options.Interceptors.Add<ErrorHandlingInterceptor>();
    options.Interceptors.Add<LoggingInterceptor>();
    options.Interceptors.Add<AuthenticationInterceptor>();
    options.Interceptors.Add<SessionContextInterceptor>();
});

builder.Services.AddGrpcReflection();
builder.Services.AddHealthChecks();

ConfigureKestrel(builder, serverOptions);

var app = builder.Build();

var runtimeOptions = app.Services.GetRequiredService<IOptions<GrpcServerOptions>>().Value;

app.MapHealthChecks("/healthz");

if (runtimeOptions.EnableReflection || app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

app.MapGrpcService<SessionGrpcService>();
app.MapGrpcService<UIAutomationGrpcService>();
app.MapGrpcService<VisionGrpcService>();
app.MapGrpcService<CodeGenGrpcService>();
app.MapGrpcService<AgentGrpcService>();

app.MapGet("/", () => "Cascade gRPC server is running.");

app.Run();

static void ConfigureKestrel(WebApplicationBuilder builder, GrpcServerOptions options)
{
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.ListenAnyIP(options.Port, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;

            if (!string.IsNullOrWhiteSpace(options.CertificatePath) &&
                !string.IsNullOrWhiteSpace(options.CertificateKeyPath))
            {
                listenOptions.UseHttps(options.CertificatePath, options.CertificateKeyPath);
            }
        });
    });
}

