using Cascade.Body.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Cascade.Body.Tests;

public static class TestHelpers
{
    public static IOptions<T> Options<T>(T value) where T : class => Microsoft.Extensions.Options.Options.Create(value);

    public static ILogger<T> Logger<T>() => NullLogger<T>.Instance;
    
    public static ILogger<T> Logger<T>(ITestOutputHelper? output) where T : class
    {
        if (output == null) return NullLogger<T>.Instance;
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        return loggerFactory.CreateLogger<T>();
    }
}

