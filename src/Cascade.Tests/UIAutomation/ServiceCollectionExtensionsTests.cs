using System;
using Cascade.UIAutomation.Services;
using Cascade.UIAutomation.Session;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cascade.Tests.UIAutomation;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCascadeUIAutomation_RegistersFactoryAndAccessor()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddCascadeUIAutomation(options =>
        {
            options.DefaultTimeout = TimeSpan.FromSeconds(5);
        });

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IUIAutomationServiceFactory>().Should().NotBeNull();
        provider.GetRequiredService<ISessionContextAccessor>().Should().NotBeNull();

        var configured = provider.GetRequiredService<IOptions<UIAutomationOptions>>().Value;
        configured.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }
}

