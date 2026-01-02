using Cascade.Body.Automation;
using Cascade.Body.Configuration;
using Cascade.Body.Providers.PlaywrightProvider;
using Cascade.Body.Providers.UIA3Provider;
using Cascade.Body.Services;
using Cascade.Body.Vision;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<BodyOptions>(builder.Configuration.GetSection("Body"));
builder.Services.Configure<UIA3Options>(builder.Configuration.GetSection("UIA3"));
builder.Services.Configure<PlaywrightOptions>(builder.Configuration.GetSection("Playwright"));
builder.Services.Configure<VisionOptions>(builder.Configuration.GetSection("Vision"));
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection("Ocr"));

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddGrpcReflection();
}

builder.WebHost.ConfigureKestrel(options =>
{
    // Allow HTTP/2 without TLS for local dev; production should terminate TLS upstream.
    options.ListenAnyIP(builder.Configuration.GetValue<int?>("Kestrel:Port") ?? 50051, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// Providers and supporting services.
builder.Services.AddSingleton<IAutomationProvider, UIA3AutomationProvider>();
builder.Services.AddSingleton<IAutomationProvider, PlaywrightAutomationProvider>();
builder.Services.AddSingleton<AutomationRouter>();
builder.Services.AddSingleton<MarkerService>();
builder.Services.AddSingleton<OcrService>();

// gRPC service implementations.
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AutomationService>();
builder.Services.AddScoped<VisionService>();
builder.Services.AddSingleton<AgentCommService>();

var app = builder.Build();

app.MapGrpcService<SessionService>();
app.MapGrpcService<AutomationService>();
app.MapGrpcService<VisionService>();
app.MapGrpcService<AgentCommService>();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
    app.MapGet("/", () => "Cascade Body gRPC server (dev)");
}

app.Run();

