using Cascade.UIAutomation.Input;
using Cascade.UIAutomation.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Services;

internal sealed class UIAutomationServiceFactory : IUIAutomationServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public UIAutomationServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IUIAutomationService Create(SessionHandle handle, AutomationElement rootElement, VirtualInputChannel inputChannel)
    {
        if (handle is null) throw new ArgumentNullException(nameof(handle));
        if (rootElement is null) throw new ArgumentNullException(nameof(rootElement));
        if (inputChannel is null) throw new ArgumentNullException(nameof(inputChannel));

        var options = _serviceProvider.GetRequiredService<IOptions<UIAutomationOptions>>().Value;
        var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
        var context = new SessionContext(handle, inputChannel, rootElement);
        return new UIAutomationService(context, options, loggerFactory);
    }
}


